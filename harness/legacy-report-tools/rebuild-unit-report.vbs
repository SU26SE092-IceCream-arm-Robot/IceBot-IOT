Option Explicit
Dim trxPath, bookPath, xml, nodes, excel, book, example, groups, i, node, full, parts, cls
trxPath=WScript.Arguments(0): bookPath=WScript.Arguments(1)
Set xml=CreateObject("Msxml2.DOMDocument.6.0"): xml.async=False: xml.Load trxPath
If xml.parseError.errorCode<>0 Then WScript.Echo xml.parseError.reason: WScript.Quit 2
Set nodes=xml.selectNodes("//*[local-name()='UnitTestResult']")
Set groups=CreateObject("Scripting.Dictionary")
For Each node In nodes
 full=node.getAttribute("testName"): parts=Split(full,"."): cls=parts(3)
 If Not groups.Exists(cls) Then Set groups(cls)=CreateObject("System.Collections.ArrayList")
 groups(cls).Add node
Next
Set excel=CreateObject("Excel.Application"): excel.Visible=False: excel.DisplayAlerts=False
Set book=excel.Workbooks.Open(bookPath): Set example=book.Worksheets("Example")
For i=book.Worksheets.Count To 1 Step -1
 If Not KeepSheet(book.Worksheets(i).Name) Then book.Worksheets(i).Delete: WScript.Sleep 100
Next
Dim key, sheet, info, n, extra, resultRow, j, col, scenario, expected, typ, outcome
For Each key In groups.Keys
 example.Copy ,book.Worksheets(book.Worksheets.Count): WScript.Sleep 250
 Set sheet=book.Worksheets(book.Worksheets.Count): info=Info(CStr(key)): sheet.Name=info(0): n=groups(key).Count
 extra=0: If n>10 Then extra=n-10: sheet.Rows("45:"&(44+extra)).Insert
 resultRow=45+extra: sheet.Cells.ClearContents
 sheet.Range("A2")="Function Code": sheet.Range("C2")="UT-"&info(0): sheet.Range("F2")="Function Name": sheet.Range("L2")=info(1)
 sheet.Range("A3")="Created By": sheet.Range("C3")="IceBot Team": sheet.Range("F3")="Executed By": sheet.Range("L3")="Automated xUnit"
 sheet.Range("A5")="Test requirement": sheet.Range("C5")="Unit tests for "&info(1)&"."
 sheet.Range("A6")="Passed": sheet.Range("C6")="Failed": sheet.Range("F6")="Untested": sheet.Range("L6")="N/A/B": sheet.Range("O6")="Total Test Cases"
 sheet.Range("A7")=CountOutcome(groups(key),"Passed"): sheet.Range("C7")=CountOutcome(groups(key),"Failed"): sheet.Range("F7")=0: sheet.Range("L7")=CountType(groups(key),"N"): sheet.Range("M7")=CountType(groups(key),"A"): sheet.Range("N7")=CountType(groups(key),"B"): sheet.Range("O7")=n
 sheet.Range("A10")="Condition": sheet.Range("B10")="Precondition": sheet.Range("B14")="Test scenario"
 sheet.Range("A33")="Confirm": sheet.Range("B33")="Expected result"
 sheet.Cells(resultRow,1)="Result": sheet.Cells(resultRow,2)="Type (N: Normal, A: Abnormal, B: Boundary)": sheet.Cells(resultRow+1,2)="Passed/Failed": sheet.Cells(resultRow+2,2)="Executed Date"
 For j=0 To n-1
  Set node=groups(key)(j): full=node.getAttribute("testName"): col=6+j: scenario=Replace(Mid(full,InStrRev(full,".")+1),"_"," "): expected=ExpectedText(full): typ=TypeText(full): outcome=node.getAttribute("outcome")
  sheet.Cells(9,col)="UTCID"&Right("0"&(j+1),2): sheet.Cells(15+j,4)=scenario: sheet.Cells(15+j,col)="O": sheet.Cells(35+j,4)=expected: sheet.Cells(35+j,col)="O": sheet.Cells(resultRow,col)=typ: If outcome="Passed" Then sheet.Cells(resultRow+1,col)="P" Else sheet.Cells(resultRow+1,col)="F"
  sheet.Cells(resultRow+2,col)=Date
 Next
Next
book.Save: book.Close True: excel.Quit
WScript.Echo "Rebuilt Unit Test workbook: "&nodes.length&" tests in "&groups.Count&" sheets."
Function KeepSheet(n): KeepSheet=(n="Guideline" Or n="Cover" Or n="Functions" Or n="Statistics" Or n="Example"): End Function
Function Info(c)
 Select Case c
 Case "AuthenticationAndConnectivityTests": Info=Array("AUTH-NET","Authentication and NetBird validation")
 Case "ConfigSetupWizardTests": Info=Array("CONFIG-ID","Configuration identity preservation")
 Case "SiteSettingsTests": Info=Array("SITE-CFG","Site settings and device mapping")
 Case "EdgeClientCertificateProvisionerTests": Info=Array("MTLS-CERT","mTLS client certificate")
 Case "ExecutionEndpointRegistrationTests": Info=Array("ENDPOINT","Kiosk and execution endpoint contracts")
 Case "PeripheralDeviceRegistrationTests": Info=Array("DEVICE","Peripheral device registration contract")
 Case "EdgeDeploymentApiTests": Info=Array("DEPLOY-API","Full Edge deployment command contract")
 Case "FullEdgeConfigurationInstallerTests": Info=Array("LUA-INSTALL","Verified Lua bundle installation")
 Case "MachinePluginLoaderTests": Info=Array("DRIVER-DLL","Peripheral driver plugin loading")
 Case "OrderRequestTests": Info=Array("LOCAL-ORDER","Legacy local Order contract")
 Case "EdgeOrderInboxTests": Info=Array("MTLS-ORDER","mTLS ExecuteOrder validation")
 Case "EdgeOrderExecutionQueueTests": Info=Array("ORDER-QUEUE","Durable production queue")
 Case "ProductionReportOutboxTests": Info=Array("REPORT-OUT","Production report outbox")
 Case Else: Err.Raise 5,,"Missing report mapping: "&c
 End Select
End Function
Function TypeText(n): If HasAny(n,"Limit|Four|Ten|Boundary") Then TypeText="B" ElseIf HasAny(n,"Reject|Invalid|Missing|Wrong|Expired|Tampered|Unsafe|Error|Empty|Without|NonPositive") Then TypeText="A" Else TypeText="N": End If: End Function
Function ExpectedText(n): If InStr(n,"Reject")>0 Then ExpectedText="Input is rejected with the expected error." ElseIf InStr(n,"Accept")>0 Then ExpectedText="Input is accepted and required data is retained." ElseIf InStr(n,"Return")>0 Then ExpectedText="Expected value or identifier is returned." Else ExpectedText="All automated assertions pass.": End If: End Function
Function HasAny(n,list): Dim a,x: HasAny=False: a=Split(list,"|"): For Each x In a: If InStr(n,x)>0 Then HasAny=True: Exit Function
Next: End Function
Function CountOutcome(list,value): Dim x,total: total=0: For Each x In list: If x.getAttribute("outcome")=value Then total=total+1
Next: CountOutcome=total: End Function
Function CountType(list,value): Dim x,total: total=0: For Each x In list: If TypeText(x.getAttribute("testName"))=value Then total=total+1
Next: CountType=total: End Function
