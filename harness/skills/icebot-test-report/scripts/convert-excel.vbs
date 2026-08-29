Option Explicit
If WScript.Arguments.Count <> 3 Then WScript.Quit 2
Dim inputPath, outputPath, formatCode, excel, book
inputPath = WScript.Arguments(0)
outputPath = WScript.Arguments(1)
formatCode = CInt(WScript.Arguments(2))
Set excel = CreateObject("Excel.Application")
excel.Visible = False
excel.DisplayAlerts = False
On Error Resume Next
Set book = excel.Workbooks.Open(inputPath, 0, True)
If Err.Number <> 0 Then
  Err.Clear
  Set book = excel.Workbooks.Open(inputPath, 0, True, , , , True, , , False, False, , False, True, 1)
End If
If Err.Number <> 0 Or book Is Nothing Then
  WScript.Echo "Open failed: " & Err.Description
  excel.Quit
  WScript.Quit 3
End If
On Error GoTo 0
book.SaveAs outputPath, formatCode
book.Close False
excel.Quit
Set book = Nothing
Set excel = Nothing
WScript.Echo "Converted: " & outputPath
