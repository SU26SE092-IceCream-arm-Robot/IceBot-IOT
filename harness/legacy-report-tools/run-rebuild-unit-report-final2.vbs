Option Explicit
Dim fso, source, re
Set fso=CreateObject("Scripting.FileSystemObject")
source=fso.OpenTextFile(fso.BuildPath(fso.GetParentFolderName(WScript.ScriptFullName),"rebuild-unit-report.vbs"),1).ReadAll
source=Replace(source,"Option Explicit"&vbCrLf,"")
source=Replace(source,"Dim key, sheet, info,","Dim key, sheet, details,")
source=Replace(source,"info=Info(CStr(key))","details=Info(CStr(key))")
source=Replace(source,"info(0)","details(0)"): source=Replace(source,"info(1)","details(1)")
Set re=New RegExp: re.Global=True
re.Pattern="sheet\.Range\(([^\r\n]*?)\)=" : source=re.Replace(source,"sheet.Range($1).Value=")
re.Pattern="sheet\.Cells\(([^\r\n]*?)\)=" : source=re.Replace(source,"sheet.Cells($1).Value=")
ExecuteGlobal source
