param([switch]$SkipWeb, [switch]$Lite)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $dotnet=Join-Path $root '.tools/dotnet/dotnet.exe'
    if(!(Test-Path -LiteralPath $dotnet)){$dotnet='dotnet'}
    if(!$SkipWeb){Push-Location src/SideWidget.Web;try{& npm ci --no-fund;if($LASTEXITCODE){throw 'npm ci failed'};& npm run build;if($LASTEXITCODE){throw 'Web build failed'}}finally{Pop-Location}}
    $releaseName=if($Lite){'release-lite'}else{'release'}
    $destination=[IO.Path]::GetFullPath((Join-Path $root "artifacts/$releaseName"))
    if(Get-Process SideWidget.App -ErrorAction SilentlyContinue | Where-Object {$_.Path -like "$destination\*"}){throw 'Close SideWidget from its tray menu before publishing.'}
    # A fresh directory prevents stale binaries and user-added files entering the manifest.
    $release=[IO.Path]::GetFullPath((Join-Path $root ('artifacts/publish-'+[Guid]::NewGuid().ToString('N'))))
    $buildArtifacts=Join-Path $root ('artifacts/build-'+[Guid]::NewGuid().ToString('N'))
    [string[]]$variantArguments=if($Lite){'-p:SideWidgetLite=true'}else{'--self-contained','true'}
    & $dotnet publish src/SideWidget.App -c Release -r win-x64 @variantArguments --artifacts-path $buildArtifacts -o $release
    if($LASTEXITCODE){throw 'Native publish failed'}
    if(Test-Path -LiteralPath (Join-Path $release 'Python')){throw 'Unexpected Python runtime in new release'}
    # The app's AfterTargets=Publish hook bundles runtimes and writes/validates the manifest.
    if(Get-Process SideWidget.App -ErrorAction SilentlyContinue | Where-Object {$_.Path -like "$destination\*"}){throw 'Release started during publishing; verified candidate retained in '+$release}
    if(Test-Path -LiteralPath $destination){
        $previous=[IO.Path]::GetFullPath((Join-Path $root ("artifacts/reset-backup/$releaseName-"+[Guid]::NewGuid().ToString('N'))))
        if(!$destination.StartsWith($root+'\') -or !$previous.StartsWith($root+'\')){throw 'Unsafe output path'}
        New-Item -ItemType Directory -Path (Split-Path $previous -Parent) -Force | Out-Null
        Move-Item -LiteralPath $destination -Destination $previous
    }
    if(!$release.StartsWith($root+'\') -or !$destination.StartsWith($root+'\')){throw 'Unsafe output path'}
    Move-Item -LiteralPath $release -Destination $destination
    Write-Host "Release: artifacts/$releaseName/SideWidget.App.exe"
}finally{Pop-Location}
