param([string]$TrxPath,[string]$WorkbookPath)
$source=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'rebuild-unit-report-final.ps1') -Raw
$source=$source.Substring($source.IndexOf("`n")+1)
$source=$source.Replace('$s.Delete()','$s.Delete();Start-Sleep -Milliseconds 150')
$source=$source.Replace('$example.Copy([Type]::Missing,$book.Worksheets.Item($book.Worksheets.Count));','$example.Copy([Type]::Missing,$book.Worksheets.Item($book.Worksheets.Count));Start-Sleep -Milliseconds 250;')
Invoke-Expression $source
