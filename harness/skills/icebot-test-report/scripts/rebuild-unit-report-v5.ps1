param([string]$TrxPath,[string]$WorkbookPath)
$source=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'rebuild-unit-report-v2.ps1') -Raw
$source=$source.Substring($source.IndexOf("`n")+1)
$old='$s.Range(''F9:IV60'').ClearContents();$s.Range(''B14:D32'').ClearContents();$s.Range((''B34:D''+(44+$extra))).ClearContents()'
$new='for($rr=9;$rr -le (48+$extra);$rr++){for($cc=2;$cc -le 30;$cc++){$cell=$s.Cells.Item($rr,$cc);if($rr -eq 9 -or ($rr -ge 14 -and $rr -le (44+$extra)) -or [string]$cell.Text -eq ''O''){$cell.MergeArea.ClearContents()}}}'
Invoke-Expression ($source.Replace($old,$new))
