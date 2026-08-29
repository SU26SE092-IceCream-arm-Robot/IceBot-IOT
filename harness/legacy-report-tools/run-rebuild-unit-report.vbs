Option Explicit
Dim fso, source
Set fso=CreateObject("Scripting.FileSystemObject")
source=fso.OpenTextFile(fso.BuildPath(fso.GetParentFolderName(WScript.ScriptFullName),"rebuild-unit-report.vbs"),1).ReadAll
source=Replace(source,"info","meta",1,-1,1)
ExecuteGlobal source
