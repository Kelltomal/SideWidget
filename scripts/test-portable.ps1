param([string]$ReleaseDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$release=if($ReleaseDirectory){[IO.Path]::GetFullPath($ReleaseDirectory)}else{Join-Path $root 'artifacts/release'}
$testRoot=Join-Path $root ('artifacts/verification/portable-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
$exports=Join-Path $testRoot 'exports'
$unpacked=Join-Path $testRoot 'unpacked'
$emptyRuntime=Join-Path $testRoot 'empty-dotnet'
New-Item -ItemType Directory -Path $exports,$emptyRuntime -Force | Out-Null
function Run-App {
    param([string]$Executable,[string[]]$Arguments)
    $start=[Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute=$false
    $start.CreateNoWindow=$true
    $start.WorkingDirectory=Split-Path $Executable -Parent
    foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
    $start.Environment['DOTNET_ROOT']=$emptyRuntime
    $start.Environment['DOTNET_ROOT_X64']=$emptyRuntime
    $start.Environment['DOTNET_MULTILEVEL_LOOKUP']='0'
    $start.Environment['DOTNET_HOST_TRACE']='1'
    $start.Environment['DOTNET_HOST_TRACEFILE']=Join-Path $testRoot 'dotnet-host-trace.log'
    $start.Environment['LOCALAPPDATA']=Join-Path $testRoot 'profile'
    $process=[Diagnostics.Process]::Start($start)
    if(!$process.WaitForExit(60000)){$process.Kill($true);throw 'Portable verification timed out'}
    if($process.ExitCode -ne 0){throw "Portable app failed: $($process.ExitCode). See $testRoot"}
}
# This is the exact same DistributionArchive.Create method called by the Share button.
Run-App (Join-Path $release 'SideWidget.App.exe') @('--share-portable',$exports)
$archives=@(Get-ChildItem -LiteralPath $exports -Filter '*.zip' -File)
if($archives.Count -ne 1){throw 'Share must produce exactly one archive'}
$archive=$archives[0]
[IO.Compression.ZipFile]::ExtractToDirectory($archive.FullName,$unpacked)
$report=Join-Path $testRoot 'portable-report.json'
Run-App (Join-Path $unpacked 'SideWidget.App.exe') @('--verify-portable',$report)
$result=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
if(!$result.passed){throw "Portable verification failed: $($result.error)"}
if(!$result.runtime.StartsWith($unpacked+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'System .NET was used'}
if(!$result.browser.StartsWith($unpacked+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'System WebView2 was used'}
# A recipient must also be able to share the extracted distribution.
$reshared=Join-Path $testRoot 'reshared'
Run-App (Join-Path $unpacked 'SideWidget.App.exe') @('--share-portable',$reshared)
if(!(Get-ChildItem -LiteralPath $reshared -Filter '*.zip' -File)){throw 'Unpacked app cannot share again'}
Write-Host "PASS real apphost: ZIP roundtrip, local .NET/WinUI/WebView2/SQLite/VC++, recipient resharing. Report: $report"
