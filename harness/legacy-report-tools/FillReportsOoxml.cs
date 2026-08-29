using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

internal sealed class ReportTest
{
    public string ClassName, Code, Function, Area, Scenario, Expected, Type, Outcome, Duration;
}

internal sealed class OoxmlBook : IDisposable
{
    private readonly string source;
    private readonly string directory;
    private readonly XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private readonly Dictionary<string, Tuple<XDocument,string>> sheets = new Dictionary<string, Tuple<XDocument,string>>(StringComparer.OrdinalIgnoreCase);

    public OoxmlBook(string path)
    {
        source = Path.GetFullPath(path);
        directory = Path.Combine(Path.GetTempPath(), "icebot-report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        ZipFile.ExtractToDirectory(source, directory);
        LoadSheets();
    }

    private void LoadSheets()
    {
        XNamespace relDoc = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace pkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";
        var workbook = XDocument.Load(Path.Combine(directory,"xl","workbook.xml"));
        var relationships = XDocument.Load(Path.Combine(directory,"xl","_rels","workbook.xml.rels"));
        foreach (var sheet in workbook.Descendants(main+"sheet"))
        {
            string id=(string)sheet.Attribute(relDoc+"id");
            var relationship=relationships.Descendants(pkgRel+"Relationship").Single(x=>(string)x.Attribute("Id")==id);
            string target=((string)relationship.Attribute("Target")).Replace('/',Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            string path=Path.Combine(directory,"xl",target);
            sheets[(string)sheet.Attribute("name")]=Tuple.Create(XDocument.Load(path,LoadOptions.PreserveWhitespace),path);
        }
    }

    public void Clear(string sheetName, int minRow, int maxRow, int minCol, int maxCol)
    {
        var doc=sheets[sheetName].Item1;
        foreach(var cell in doc.Descendants(main+"c").ToList())
        {
            int row,col; ParseReference((string)cell.Attribute("r"),out row,out col);
            if(row<minRow||row>maxRow||col<minCol||col>maxCol) continue;
            cell.Elements().Where(x=>x.Name==main+"v"||x.Name==main+"is"||x.Name==main+"f").Remove();
            var typeAttribute=cell.Attribute("t");
            if(typeAttribute!=null) typeAttribute.Remove();
        }
    }

    public void Set(string sheetName, int rowNumber, int columnNumber, object value)
    {
        var doc=sheets[sheetName].Item1;
        var sheetData=doc.Descendants(main+"sheetData").Single();
        var row=sheetData.Elements(main+"row").FirstOrDefault(x=>(int?)x.Attribute("r")==rowNumber);
        if(row==null)
        {
            row=new XElement(main+"row",new XAttribute("r",rowNumber));
            var next=sheetData.Elements(main+"row").FirstOrDefault(x=>(int)x.Attribute("r")>rowNumber);
            if(next==null) sheetData.Add(row); else next.AddBeforeSelf(row);
        }
        string reference=ColumnName(columnNumber)+rowNumber;
        var cell=row.Elements(main+"c").FirstOrDefault(x=>string.Equals((string)x.Attribute("r"),reference,StringComparison.OrdinalIgnoreCase));
        if(cell==null)
        {
            cell=new XElement(main+"c",new XAttribute("r",reference));
            var styleSource=doc.Descendants(main+"c").FirstOrDefault(x=>CellRow((string)x.Attribute("r"))==rowNumber && CellColumn((string)x.Attribute("r"))==Math.Min(columnNumber,6));
            if(styleSource!=null && styleSource.Attribute("s")!=null) cell.SetAttributeValue("s",(string)styleSource.Attribute("s"));
            var next=row.Elements(main+"c").FirstOrDefault(x=>CellColumn((string)x.Attribute("r"))>columnNumber);
            if(next==null) row.Add(cell); else next.AddBeforeSelf(cell);
        }
        cell.Elements().Where(x=>x.Name==main+"v"||x.Name==main+"is"||x.Name==main+"f").Remove();
        cell.SetAttributeValue("t","inlineStr");
        cell.Add(new XElement(main+"is",new XElement(main+"t",new XAttribute(XNamespace.Xml+"space","preserve"),Convert.ToString(value)??"")));
        UpdateDimension(doc,rowNumber,columnNumber);
    }

    private void UpdateDimension(XDocument doc,int row,int col)
    {
        var dimension=doc.Root.Element(main+"dimension");
        if(dimension==null) return;
        string current=(string)dimension.Attribute("ref")??"A1";
        string end=current.Contains(":")?current.Split(':')[1]:current;
        int oldRow,oldCol;ParseReference(end,out oldRow,out oldCol);
        if(row>oldRow||col>oldCol) dimension.SetAttributeValue("ref","A1:"+ColumnName(Math.Max(col,oldCol))+Math.Max(row,oldRow));
    }

    public void Save()
    {
        foreach(var item in sheets.Values) item.Item1.Save(item.Item2,SaveOptions.DisableFormatting);
        string temporary=source+".new";
        if(File.Exists(temporary)) File.Delete(temporary);
        ZipFile.CreateFromDirectory(directory,temporary,CompressionLevel.Optimal,false);
        File.Replace(temporary,source,null);
    }

    public void Dispose(){try{if(Directory.Exists(directory))Directory.Delete(directory,true);}catch{}}
    private static int CellRow(string reference){int r,c;ParseReference(reference,out r,out c);return r;}
    private static int CellColumn(string reference){int r,c;ParseReference(reference,out r,out c);return c;}
    private static void ParseReference(string value,out int row,out int col){col=0;int i=0;while(i<value.Length&&char.IsLetter(value[i])){col=col*26+(char.ToUpperInvariant(value[i])-'A'+1);i++;}int.TryParse(value.Substring(i),out row);}
    private static string ColumnName(int col){string s="";while(col>0){col--;s=(char)('A'+col%26)+s;col/=26;}return s;}
}

internal static class FillReportsOoxml
{
    private static readonly Dictionary<string,string[]> Catalog=new Dictionary<string,string[]>{
        {"AuthenticationAndConnectivityTests",new[]{"AUTH-NET","Authentication and NetBird validation","Foundation"}},
        {"ConfigSetupWizardTests",new[]{"CONFIG-ID","Configuration identity preservation","Foundation"}},
        {"SiteSettingsTests",new[]{"SITE-CFG","Site settings and device mapping","Foundation"}},
        {"EdgeClientCertificateProvisionerTests",new[]{"MTLS-CERT","mTLS client certificate","Foundation"}},
        {"ExecutionEndpointRegistrationTests",new[]{"ENDPOINT","Kiosk and execution endpoint contracts","Foundation"}},
        {"PeripheralDeviceRegistrationTests",new[]{"DEVICE","Peripheral device registration contract","Foundation"}},
        {"EdgeDeploymentApiTests",new[]{"DEPLOY-API","Full Edge deployment command contract","Deployment"}},
        {"FullEdgeConfigurationInstallerTests",new[]{"LUA-INSTALL","Verified Lua bundle installation","Deployment"}},
        {"MachinePluginLoaderTests",new[]{"DRIVER-DLL","Peripheral driver plugin loading","Deployment"}},
        {"OrderRequestTests",new[]{"LOCAL-ORDER","Legacy local Order contract","Order Runtime"}},
        {"EdgeOrderInboxTests",new[]{"MTLS-ORDER","mTLS ExecuteOrder validation","Order Runtime"}},
        {"EdgeOrderExecutionQueueTests",new[]{"ORDER-QUEUE","Durable production queue","Order Runtime"}},
        {"ProductionReportOutboxTests",new[]{"REPORT-OUT","Production report outbox","Order Runtime"}}
    };

    public static int Main(string[] args)
    {
        if(args.Length<1||args.Length>2){Console.Error.WriteLine("Usage: FillReportsOoxml <testing-dir> [unit-xlsx]");return 2;}
        string dir=Path.GetFullPath(args[0]);var tests=Read(Path.Combine(dir,"results","IceBot.UnitTests.Fresh.trx"));
        using(var report=new OoxmlBook(Path.Combine(dir,"Report5_Test Report.xlsx"))){FillTestReport(report,tests);report.Save();}
        if(args.Length==2)using(var unit=new OoxmlBook(Path.GetFullPath(args[1]))){FillUnitReport(unit,tests);unit.Save();}
        Console.WriteLine("OOXML reports filled: "+tests.Count+" tests, "+tests.Count(x=>x.Outcome=="Passed")+" passed.");return 0;
    }

    private static List<ReportTest> Read(string path)
    {
        return XDocument.Load(path).Descendants().Where(x=>x.Name.LocalName=="UnitTestResult").OrderBy(x=>(string)x.Attribute("testName")).Select(x=>{
            string full=(string)x.Attribute("testName");string[] p=full.Split('.');string cls=p[3];string[] m=Catalog[cls];string leaf=full.Substring(full.LastIndexOf('.')+1).Replace('_',' ');
            return new ReportTest{ClassName=cls,Code=m[0],Function=m[1],Area=m[2],Scenario=leaf,Expected=Expected(full),Type=Type(full),Outcome=(string)x.Attribute("outcome"),Duration=(string)x.Attribute("duration")};
        }).ToList();
    }
    private static string Type(string n){if(Has(n,"Limit","Four","Ten","Boundary"))return"B";if(Has(n,"Reject","Invalid","Missing","Wrong","Expired","Tampered","Unsafe","Error","Empty","Without","NonPositive"))return"A";return"N";}
    private static bool Has(string n,params string[] v){return v.Any(n.Contains);}
    private static string Expected(string n){if(n.Contains("Reject"))return"Invalid or unsafe input is rejected with the expected error.";if(n.Contains("Accept"))return"Valid input is accepted and all required data is retained.";if(n.Contains("Return"))return"The expected value or backend identifier is returned.";if(n.Contains("Create"))return"The expected durable object or file is created exactly once.";if(n.Contains("Preserve"))return"Existing backend identity is preserved safely.";return"All automated assertions pass.";}
    private static int Pass(IEnumerable<ReportTest>x){return x.Count(t=>t.Outcome=="Passed");}private static int Fail(IEnumerable<ReportTest>x){return x.Count(t=>t.Outcome=="Failed");}
    private static string Pct(int v,int t){return t==0?"0.00":(100.0*v/t).ToString("0.00");}

    private static void FillTestReport(OoxmlBook b,List<ReportTest> tests)
    {
        string d=DateTime.Today.ToString("yyyy-MM-dd");
        b.Set("Cover",3,2,"IceBot-IOT");b.Set("Cover",3,6,"IceBot Team");b.Set("Cover",4,2,"ICEBOT-IOT");b.Set("Cover",4,6,d);b.Set("Cover",5,2,"ICEBOT-IOT_TEST_REPORT_v1.0");b.Set("Cover",5,6,"1.0");b.Set("Cover",10,1,d);b.Set("Cover",10,2,"1.0");b.Set("Cover",10,3,"All");b.Set("Cover",10,4,"A");b.Set("Cover",10,5,"Initial report generated from freshly executed IceBot unit tests.");b.Set("Cover",10,6,"IceBot.UnitTests.Fresh.trx");
        b.Set("Test Cases",3,3,"IceBot-IOT");b.Set("Test Cases",4,3,"ICEBOT-IOT");b.Set("Test Cases",5,3,".NET Framework 4.7.2; xUnit 2.9.2; Windows; isolated storage; no live BE, robot or RS485.");b.Clear("Test Cases",9,200,1,5);
        var groups=tests.GroupBy(x=>x.ClassName).OrderBy(x=>x.First().Area).ThenBy(x=>x.First().Code).ToList();for(int i=0;i<groups.Count;i++){var t=groups[i].First();int r=9+i;b.Set("Test Cases",r,1,i+1);b.Set("Test Cases",r,2,t.Function);b.Set("Test Cases",r,3,t.Code);b.Set("Test Cases",r,4,"Automated unit tests for "+t.Function+".");b.Set("Test Cases",r,5,"No live hardware/network required.");}
        string[] sn={"Feature 1","Feature 2"};string[] names={"Edge initialization, identity and connectivity","Deployment, driver and Order execution"};for(int a=0;a<2;a++){var items=tests.Where(x=>(x.Area=="Foundation")== (a==0)).ToList();b.Set(sn[a],2,2,names[a]);b.Set(sn[a],3,2,"Verify completed deterministic functions of the current IceBot Edge application.");b.Set(sn[a],4,2,items.Count);b.Set(sn[a],6,2,Pass(items));b.Set(sn[a],6,3,Fail(items));b.Set(sn[a],6,4,0);b.Set(sn[a],6,5,0);b.Clear(sn[a],10,200,1,15);for(int i=0;i<items.Count;i++){var t=items[i];int r=10+i;b.Set(sn[a],r,1,t.Code+"-TC"+(i+1).ToString("00"));b.Set(sn[a],r,2,t.Scenario);b.Set(sn[a],r,3,"Arrange isolated input; invoke the tested function; evaluate all assertions.");b.Set(sn[a],r,4,t.Expected);b.Set(sn[a],r,5,"No live BE/hardware required.");b.Set(sn[a],r,6,t.Outcome);b.Set(sn[a],r,7,d);b.Set(sn[a],r,8,"Automated xUnit");b.Set(sn[a],r,9,"N/A");b.Set(sn[a],r,12,"N/A");b.Set(sn[a],r,15,"Type="+t.Type+"; Duration="+t.Duration);}}
        b.Set("Test Statistics",3,3,"IceBot-IOT");b.Set("Test Statistics",3,6,"IceBot Team");b.Set("Test Statistics",4,3,"ICEBOT-IOT");b.Set("Test Statistics",4,6,"Project Supervisor");b.Set("Test Statistics",5,3,"ICEBOT-IOT_TEST_REPORT_v1.0");b.Set("Test Statistics",5,6,d);b.Set("Test Statistics",6,3,"Current deterministic Edge logic; live hardware/network checks excluded.");b.Clear("Test Statistics",11,100,2,8);var f=tests.Where(x=>x.Area=="Foundation").ToList();var rtm=tests.Where(x=>x.Area!="Foundation").ToList();var sets=new[]{f,rtm};var codes=new[]{"FOUNDATION","RUNTIME"};for(int i=0;i<2;i++){b.Set("Test Statistics",11+i,2,i+1);b.Set("Test Statistics",11+i,3,codes[i]);b.Set("Test Statistics",11+i,4,Pass(sets[i]));b.Set("Test Statistics",11+i,5,Fail(sets[i]));b.Set("Test Statistics",11+i,6,0);b.Set("Test Statistics",11+i,7,0);b.Set("Test Statistics",11+i,8,sets[i].Count);}b.Set("Test Statistics",14,3,"Sub total");b.Set("Test Statistics",14,4,Pass(tests));b.Set("Test Statistics",14,5,Fail(tests));b.Set("Test Statistics",14,6,0);b.Set("Test Statistics",14,7,0);b.Set("Test Statistics",14,8,tests.Count);b.Set("Test Statistics",16,3,"Test coverage");b.Set("Test Statistics",16,5,"100.00");b.Set("Test Statistics",16,6,"%");b.Set("Test Statistics",17,3,"Test successful coverage");b.Set("Test Statistics",17,5,Pct(Pass(tests),tests.Count));b.Set("Test Statistics",17,6,"%");
    }

    private static void FillUnitReport(OoxmlBook b,List<ReportTest> tests)
    {
        string d=DateTime.Today.ToString("yyyy-MM-dd");b.Set("Cover",3,2,"IceBot-IOT");b.Set("Cover",3,6,"IceBot Team");b.Set("Cover",4,2,"ICEBOT-IOT");b.Set("Cover",4,6,d);b.Set("Cover",5,2,"ICEBOT-IOT_UNIT_TEST_v1.0");b.Set("Cover",5,6,"1.0");b.Set("Cover",10,1,d);b.Set("Cover",10,2,"1.0");b.Set("Cover",10,3,"All");b.Set("Cover",10,4,"A");b.Set("Cover",10,5,"Initial document generated from freshly executed IceBot unit tests.");b.Set("Cover",10,6,"IceBot.UnitTests.Fresh.trx");
        string[] areas={"Foundation","Deployment","Order Runtime"};string[] sheets={"Function 1","Function 2","Function3"};for(int a=0;a<3;a++){var items=tests.Where(x=>x.Area==areas[a]).ToList();string s=sheets[a];b.Set(s,2,3,"UT-"+areas[a].ToUpperInvariant().Replace(' ','-'));b.Set(s,2,12,areas[a]);b.Set(s,3,3,"IceBot Team");b.Set(s,3,12,"Automated xUnit");b.Set(s,4,3,"N/A");b.Set(s,4,12,0);b.Set(s,5,3,"Unit tests for "+areas[a]+" functions in the current IceBot application.");b.Set(s,7,1,Pass(items));b.Set(s,7,3,Fail(items));b.Set(s,7,6,0);b.Set(s,7,12,items.Count(x=>x.Type=="N"));b.Set(s,7,13,items.Count(x=>x.Type=="A"));b.Set(s,7,14,items.Count(x=>x.Type=="B"));b.Set(s,7,15,items.Count);b.Clear(s,9,48,6,52);for(int i=0;i<items.Count;i++){var t=items[i];int c=6+i;b.Set(s,9,c,"UTCID"+(i+1).ToString("00"));b.Set(s,11,c,"O");b.Set(s,15,c,t.Scenario);b.Set(s,34,c,t.Expected);b.Set(s,45,c,t.Type);b.Set(s,46,c,t.Outcome=="Passed"?"P":t.Outcome=="Failed"?"F":"U");b.Set(s,47,c,d);if(t.Outcome=="Failed")b.Set(s,48,c,"See TRX");}}
        b.Set("Functions",3,5,"IceBot-IOT");b.Set("Functions",4,5,"ICEBOT-IOT");b.Set("Functions",5,5,"N/A - risk-based test design");b.Set("Functions",6,5,".NET Framework 4.7.2; xUnit 2.9.2; Windows; isolated storage.");b.Clear("Functions",10,100,1,8);for(int a=0;a<3;a++){b.Set("Functions",10+a,1,a+1);b.Set("Functions",10+a,5,"UT-"+areas[a].ToUpperInvariant().Replace(' ','-'));b.Set("Functions",10+a,6,sheets[a]);b.Set("Functions",10+a,7,"Unit tests for "+areas[a]);b.Set("Functions",10+a,8,"No live hardware or BE required.");}
        b.Set("Statistics",3,2,"IceBot-IOT");b.Set("Statistics",3,5,"IceBot Team");b.Set("Statistics",4,2,"ICEBOT-IOT");b.Set("Statistics",4,5,"Project Supervisor");b.Set("Statistics",5,2,"ICEBOT-IOT_UNIT_TEST_v1.0");b.Set("Statistics",5,5,d);b.Set("Statistics",6,2,tests.Count+" freshly executed automated unit tests.");b.Clear("Statistics",11,100,1,9);for(int a=0;a<3;a++){var items=tests.Where(x=>x.Area==areas[a]).ToList();int r=11+a;b.Set("Statistics",r,1,a+1);b.Set("Statistics",r,2,"UT-"+areas[a].ToUpperInvariant().Replace(' ','-'));b.Set("Statistics",r,3,Pass(items));b.Set("Statistics",r,4,Fail(items));b.Set("Statistics",r,5,0);b.Set("Statistics",r,6,items.Count(x=>x.Type=="N"));b.Set("Statistics",r,7,items.Count(x=>x.Type=="A"));b.Set("Statistics",r,8,items.Count(x=>x.Type=="B"));b.Set("Statistics",r,9,items.Count);}b.Set("Statistics",14,2,"Sub total");b.Set("Statistics",14,3,Pass(tests));b.Set("Statistics",14,4,Fail(tests));b.Set("Statistics",14,5,0);b.Set("Statistics",14,6,tests.Count(x=>x.Type=="N"));b.Set("Statistics",14,7,tests.Count(x=>x.Type=="A"));b.Set("Statistics",14,8,tests.Count(x=>x.Type=="B"));b.Set("Statistics",14,9,tests.Count);b.Set("Statistics",16,2,"Test coverage");b.Set("Statistics",16,4,"100.00");b.Set("Statistics",16,5,"%");b.Set("Statistics",17,2,"Test successful coverage");b.Set("Statistics",17,4,Pct(Pass(tests),tests.Count));b.Set("Statistics",17,5,"%");b.Set("Statistics",18,2,"Normal case");b.Set("Statistics",18,4,Pct(tests.Count(x=>x.Type=="N"),tests.Count));b.Set("Statistics",18,5,"%");b.Set("Statistics",19,2,"Abnormal case");b.Set("Statistics",19,4,Pct(tests.Count(x=>x.Type=="A"),tests.Count));b.Set("Statistics",19,5,"%");b.Set("Statistics",20,2,"Boundary case");b.Set("Statistics",20,4,Pct(tests.Count(x=>x.Type=="B"),tests.Count));b.Set("Statistics",20,5,"%");
    }
}
