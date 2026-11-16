param([string]$ReleaseDirectory, [switch]$Lite, [string]$DotnetRoot, [string]$Archive, [string]$DependencyCatalog)
$ErrorActionPreference='Stop'
if ($DependencyCatalog -and (!$Lite -or !$Archive)) { throw '-DependencyCatalog requires a clean Lite archive.' }
$root=Split-Path $PSScriptRoot -Parent
$releaseName=if($Lite){'release-lite'}else{'release'}
$release=if($ReleaseDirectory){[IO.Path]::GetFullPath($ReleaseDirectory)}else{Join-Path $root "artifacts/$releaseName"}
$testRoot=Join-Path $root ('artifacts/verification/portable-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
$exports=Join-Path $testRoot 'exports'
$unpacked=Join-Path $testRoot 'unpacked'
$emptyRuntime=Join-Path $testRoot 'empty-dotnet'
New-Item -ItemType Directory -Path $exports,$emptyRuntime -Force | Out-Null
$runtime=if(!$Lite){$emptyRuntime}elseif($DotnetRoot){[IO.Path]::GetFullPath($DotnetRoot)}
function Run-App {
    param([string]$Executable,[string[]]$Arguments)
    $start=[Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute=$false
    $start.CreateNoWindow=$true
    $start.WorkingDirectory=Split-Path $Executable -Parent
    foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
    if($runtime){
        $start.Environment['DOTNET_ROOT']=$runtime
        $start.Environment['DOTNET_ROOT_X64']=$runtime
    }
    $start.Environment['DOTNET_MULTILEVEL_LOOKUP']='0'
    $start.Environment['DOTNET_HOST_TRACE']='1'
    $start.Environment['DOTNET_HOST_TRACEFILE']=Join-Path $testRoot 'dotnet-host-trace.log'
    $start.Environment['LOCALAPPDATA']=Join-Path $testRoot 'profile'
    $process=[Diagnostics.Process]::Start($start)
    if(!$process.WaitForExit(60000)){$process.Kill($true);throw 'Portable verification timed out'}
    if($process.ExitCode -ne 0){
        $failure=if($Arguments[0] -eq '--verify-portable' -and (Test-Path -LiteralPath $Arguments[1])){Get-Content -LiteralPath $Arguments[1] -Raw}
        throw "Distribution app failed: $($process.ExitCode). See $testRoot. $failure"
    }
}
if($Archive){
    $zipFile=Get-Item -LiteralPath $Archive
    $executable='SideWidget.exe'
    $application=Join-Path $unpacked 'App'
}else{
    # Exercise the same DistributionArchive.Create method used by the Share button.
    Run-App (Join-Path $release 'SideWidget.App.exe') @('--share-portable',$exports)
    $archives=@(Get-ChildItem -LiteralPath $exports -Filter '*.zip' -File)
    if($archives.Count -ne 1){throw 'Share must produce exactly one archive'}
    $zipFile=$archives[0]
    $executable='SideWidget.App.exe'
    $application=$unpacked
}
if($Lite -and ($zipFile.Name -notlike '*-lite-win-x64.zip' -or $zipFile.Length -gt 32MB)){
    throw "Unexpected Lite ZIP name or size: $($zipFile.Name), $($zipFile.Length) bytes"
}
[IO.Compression.ZipFile]::ExtractToDirectory($zipFile.FullName,$unpacked)
if ($DependencyCatalog) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    $fixture = Join-Path $root 'tests/LiteDependencyPackChecks.cs'
    $check = Join-Path $testRoot 'LiteDependencyPackChecks.exe'
    & $compiler /nologo /target:exe /platform:x64 /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
        "/out:$check" $fixture (Join-Path $PSScriptRoot 'SideWidgetPrerequisites.cs') (Join-Path $PSScriptRoot 'DependencyProgress.cs')
    if ($LASTEXITCODE) { throw 'Could not compile the Lite dependency checks.' }
    & $check $unpacked ([IO.Path]::GetFullPath($DependencyCatalog))
    if ($LASTEXITCODE) { throw 'Lite dependency installation failed.' }
}
if(!$Lite -and (Get-ChildItem -LiteralPath (Join-Path $application 'WebView2Runtime') -Recurse -File |
    Where-Object { $_.Name -in @('copilot_setup.exe','mscopilot.exe') })){
    throw 'Unexpected Copilot executable in the WebView2 runtime'
}
$report=Join-Path $testRoot 'portable-report.json'
Run-App (Join-Path $unpacked $executable) @('--verify-portable',$report)
$result=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
if(!$result.passed){throw "Portable verification failed: $($result.error)"}
if($result.lite -ne [bool]$Lite){throw 'Unexpected distribution variant'}
if($Lite){
    if($DependencyCatalog){
        $version = Get-Content -LiteralPath $DependencyCatalog -TotalCount 1
        $dependencies = Join-Path $unpacked "Dependencies/$version"
        if (!$result.runtime.StartsWith((Join-Path $dependencies 'dotnet')+'\',[StringComparison]::OrdinalIgnoreCase) -or
            !$result.aspNet.StartsWith((Join-Path $dependencies 'dotnet')+'\',[StringComparison]::OrdinalIgnoreCase) -or
            !$result.winUi.StartsWith((Join-Path $dependencies 'desktop')+'\',[StringComparison]::OrdinalIgnoreCase) -or
            $result.browser -ne (Join-Path $dependencies 'webview2') -or !$result.browserVersion) {
            throw 'Lite did not load all verified local runtimes.'
        }
    }elseif($result.browser -or !$result.browserVersion){throw 'Evergreen WebView2 was not found'}
}else{
    if(!$result.runtime.StartsWith($application+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'System .NET was used'}
    if(!$result.browser.StartsWith($application+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'System WebView2 was used'}
}
if(!$Archive -or $DependencyCatalog){
    $reshared=Join-Path $testRoot 'reshared'
    Run-App (Join-Path $unpacked $executable) @('--share-portable',$reshared)
    if(!(Get-ChildItem -LiteralPath $reshared -Filter '*.zip' -File)){throw 'Unpacked app cannot share again'}
    if($DependencyCatalog){
        $shared = [IO.Compression.ZipFile]::OpenRead((Get-ChildItem -LiteralPath $reshared -Filter '*.zip' -File).FullName)
        try {
            if (!$shared.GetEntry('SideWidget.exe') -or !$shared.GetEntry('App/distribution-files.json') -or
                ($shared.Entries | Where-Object FullName -Like 'Dependencies/*')) {
                throw 'Sharing an installed Lite build must include its launcher and exclude local runtime packages.'
            }
        } finally { $shared.Dispose() }
    }
}
Write-Host "PASS $($zipFile.Name): $executable loaded .NET/ASP.NET Core/WinUI/WebView2/SQLite/VC++. Report: $report"
