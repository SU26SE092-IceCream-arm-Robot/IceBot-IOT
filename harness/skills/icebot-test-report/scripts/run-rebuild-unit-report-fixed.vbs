Option Explicit
Dim fso, source
Set fso=CreateObject("Scripting.FileSystemObject")
source=fso.OpenTextFile(fso.BuildPath(fso.GetParentFolderName(WScript.ScriptFullName),"rebuild-unit-report.vbs"),1).ReadAll
source=Replace(source,"Dim key, sheet, info,","Dim key, sheet, details,")
source=Replace(source,"info=Info(CStr(key))","details=Info(CStr(key))")
source=Replace(source,"info(0)","details(0)")
source=Replace(source,"info(1)","details(1)")
ExecuteGlobal source
