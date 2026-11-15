param([int]$Seconds=5,[string]$Label='idle')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$app=Get-Process SideWidget.App | Select-Object -First 1
if(!$app){throw 'SideWidget is not running'}
$processes=Get-CimInstance Win32_Process
$ids=[System.Collections.Generic.HashSet[int]]::new()
[void]$ids.Add($app.Id)
do{$added=0;foreach($p in $processes){if($ids.Contains([int]$p.ParentProcessId)-and $ids.Add([int]$p.ProcessId)){$added++}}}while($added -gt 0)
function Sample {
    $rows=@(foreach($id in $ids){$p=Get-Process -Id $id -ErrorAction SilentlyContinue;if($p){[PSCustomObject]@{id=$p.Id;name=$p.ProcessName;cpu=$p.TotalProcessorTime.TotalSeconds;workingSet=$p.WorkingSet64;private=$p.PrivateMemorySize64}}})
    return $rows
}
$first=Sample
Start-Sleep -Seconds $Seconds
$second=Sample
$cpu=($second|Measure-Object cpu -Sum).Sum-($first|Measure-Object cpu -Sum).Sum
$record=[ordered]@{label=$Label;utc=[DateTime]::UtcNow.ToString('o');sampleSeconds=$Seconds;cpuOneCorePercent=[math]::Round($cpu/$Seconds*100,2);workingSetMiB=[math]::Round(($second|Measure-Object workingSet -Sum).Sum/1MB,1);privateMiB=[math]::Round(($second|Measure-Object private -Sum).Sum/1MB,1);processes=$second}
$record|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $root "docs\performance-$Label.json") -Encoding utf8
$record|ConvertTo-Json -Depth 5
