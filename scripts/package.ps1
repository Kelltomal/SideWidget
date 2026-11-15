param([string]$ReleaseDirectory, [string]$OutputArchive)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$release=if($ReleaseDirectory){[IO.Path]::GetFullPath($ReleaseDirectory)}else{Join-Path $root 'artifacts/release'}
foreach($file in @('SideWidget.App.exe','Web/index.html','Web/fonts/MedeoSansVariable.woff2','Web/fonts/MedeoMonoVariable.woff2','Assets/Fonts/MedeoSans.ttf','Assets/Icons/sidemenu.ico','Licenses/Medeo-OFL.txt','Everything64.dll','Licenses/Everything-SDK-LICENSE.txt')) {
    if(!(Test-Path -LiteralPath (Join-Path $release $file))) {throw "Release incomplete: $file. Run scripts/build.ps1."}
}
$version=([xml](Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$archive=if($OutputArchive){[IO.Path]::GetFullPath($OutputArchive)}else{Join-Path $root "artifacts/SideWidget-$version-win-x64.zip"}
$dotnet=Join-Path $root '.tools/dotnet/dotnet.exe'
$output=& $dotnet run --project (Join-Path $root 'tools/SideWidget.Distribution') -c Release -- share $release (Split-Path $archive -Parent)
if($LASTEXITCODE){throw 'Portable archive verification failed'}
$created=($output | Select-Object -Last 1).Trim()
if(!(Test-Path -LiteralPath $created)){throw 'Archive was not created'}
Move-Item -LiteralPath $created -Destination $archive -Force
$hash=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($archive))" | Set-Content -LiteralPath "$archive.sha256" -Encoding ascii
Get-Item -LiteralPath $archive | Select-Object FullName,Length

