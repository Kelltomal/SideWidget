param([switch]$SkipWeb)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $dotnet=Join-Path $root '.tools/dotnet/dotnet.exe'
    if(!(Test-Path -LiteralPath $dotnet)){$dotnet='dotnet'}
    if(!$SkipWeb){Push-Location src/SideWidget.Web;try{& npm ci --no-fund;if($LASTEXITCODE){throw 'npm ci failed'};& npm run build;if($LASTEXITCODE){throw 'Web build failed'}}finally{Pop-Location}}
    $destination=[IO.Path]::GetFullPath((Join-Path $root 'artifacts/release'))
    if(Get-Process SideWidget.App -ErrorAction SilentlyContinue | Where-Object {$_.Path -like "$destination\*"}){throw 'Close SideWidget from its tray menu before publishing.'}
    # A fresh directory prevents stale binaries and user-added files entering the manifest.
    $release=[IO.Path]::GetFullPath((Join-Path $root ('artifacts/publish-'+[Guid]::NewGuid().ToString('N'))))
    $buildArtifacts=Join-Path $root ('artifacts/build-'+[Guid]::NewGuid().ToString('N'))
    & $dotnet publish src/SideWidget.App -c Release -r win-x64 --self-contained true --artifacts-path $buildArtifacts -o $release
    if($LASTEXITCODE){throw 'Native publish failed'}
    # Publish does not remove old hashed web bundles. Archive generated leftovers.
    $webAssets=Join-Path $release 'Web/assets'
    $currentAssets=@(Get-ChildItem -LiteralPath (Join-Path $root 'src/SideWidget.Web/dist/assets') -File | Select-Object -ExpandProperty Name)
    $obsolete=@(Get-ChildItem -LiteralPath $webAssets -File | Where-Object {$_.Name -notin $currentAssets})
    if($obsolete.Count){
        $archiveAssets=[IO.Path]::GetFullPath((Join-Path $root ('artifacts/reset-backup/web-assets/'+[Guid]::NewGuid().ToString('N'))))
        if(!$archiveAssets.StartsWith($root+'\')){throw 'Unsafe archive path'}
        New-Item -ItemType Directory -Path $archiveAssets -Force | Out-Null
        foreach($file in $obsolete){if(!$file.FullName.StartsWith($release+'\')){throw 'Unsafe asset path'};Move-Item -LiteralPath $file.FullName -Destination $archiveAssets}
    }
    if(Test-Path -LiteralPath (Join-Path $release 'Python')){throw 'Unexpected Python runtime in new release'}
    # The app's AfterTargets=Publish hook bundles runtimes and writes/validates the manifest.
    if(Get-Process SideWidget.App -ErrorAction SilentlyContinue | Where-Object {$_.Path -like "$destination\*"}){throw 'Release started during publishing; verified candidate retained in '+$release}
    if(Test-Path -LiteralPath $destination){
        $previous=[IO.Path]::GetFullPath((Join-Path $root ('artifacts/reset-backup/release-'+[Guid]::NewGuid().ToString('N'))))
        if(!$destination.StartsWith($root+'\') -or !$previous.StartsWith($root+'\')){throw 'Unsafe output path'}
        New-Item -ItemType Directory -Path (Split-Path $previous -Parent) -Force | Out-Null
        Move-Item -LiteralPath $destination -Destination $previous
    }
    if(!$release.StartsWith($root+'\') -or !$destination.StartsWith($root+'\')){throw 'Unsafe output path'}
    Move-Item -LiteralPath $release -Destination $destination
    Write-Host 'Release: artifacts/release/SideWidget.App.exe (no Python)'
}finally{Pop-Location}
