param([string]$TrxPath,[string]$WorkbookPath)
$source=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'rebuild-unit-report-v2.ps1') -Raw
$source=$source.Substring($source.IndexOf("`n")+1)
$old='$s.Range(''B14:D32'').ClearContents();$s.Range((''B34:D''+(44+$extra))).ClearContents()'
$new='$s.Range(''D14:D32'').ClearContents();$s.Range((''D34:D''+(44+$extra))).ClearContents()'
Invoke-Expression ($source.Replace($old,$new))
