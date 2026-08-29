param([string]$TrxPath,[string]$WorkbookPath)
$source=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'rebuild-unit-report-v2.ps1') -Raw
$source=$source.Substring($source.IndexOf("`n")+1)
$old='$s.Range(''F9:IV60'').ClearContents();$s.Range(''B14:D32'').ClearContents();$s.Range((''B34:D''+(44+$extra))).ClearContents();$s.Range(''B14'').Value2=''Test scenario'';$s.Range(''B33'').Value2=''Confirm'';$s.Range(''B34'').Value2=''Expected result'''
$new='$s.Cells.ClearContents();$s.Range(''A10'').Value2=''Condition'';$s.Range(''B10'').Value2=''Precondition'';$s.Range(''B14'').Value2=''Test scenario'';$s.Range(''A33'').Value2=''Confirm'';$s.Range(''B33'').Value2=''Expected result'';$s.Cells.Item($result,1).Value2=''Result'';$s.Cells.Item($result,2).Value2=''Type (N: Normal, A: Abnormal, B: Boundary)'';$s.Cells.Item($result+1,2).Value2=''Passed/Failed'';$s.Cells.Item($result+2,2).Value2=''Executed Date'''
$source=$source.Replace($old,$new).Replace('$s.Delete()','$s.Delete();Start-Sleep -Milliseconds 150').Replace('$example.Copy([Type]::Missing,$book.Worksheets.Item($book.Worksheets.Count));','$example.Copy([Type]::Missing,$book.Worksheets.Item($book.Worksheets.Count));Start-Sleep -Milliseconds 300;')
Invoke-Expression $source
