param(
    [Parameter(Mandatory)][string]$LiteDirectory,
    [Parameter(Mandatory)][string]$FullDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '\A\d+\.\d+\.\d+\z') { throw 'Invalid release version.' }
$root = Split-Path $PSScriptRoot -Parent
$sdk = Join-Path $root '.tools/dotnet'
$output = [IO.Path]::GetFullPath($OutputDirectory)
$lite = [IO.Path]::GetFullPath($LiteDirectory)
$full = [IO.Path]::GetFullPath($FullDirectory)
$work = Join-Path $output ".dependencies-$([Guid]::NewGuid().ToString('N'))"
$desktop = Join-Path $work 'desktop'
[void][IO.Directory]::CreateDirectory($work)
$catalog = [Collections.Generic.List[string]]::new()
$catalog.Add($Version)

function Add-Pack($Name, $Files, $Shared = @()) {
    $assetName = "SideWidget-$Version-deps-${Name}-win-x64.zip"
    $temporary = Join-Path $work $assetName
    $zip = [IO.Compression.ZipFile]::Open($temporary, 'Create')
    try {
        foreach ($file in $Files) {
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $file.Source, $file.Path, [IO.Compression.CompressionLevel]::Fastest)
        }
        if ($Shared.Count) {
            $writer = [IO.StreamWriter]::new($zip.CreateEntry('reuse.txt').Open())
            try { foreach ($path in $Shared) { $writer.WriteLine($path) } }
            finally { $writer.Dispose() }
        }
    } finally { $zip.Dispose() }
    $hash = (Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash
    $size = (Get-Item -LiteralPath $temporary).Length
    $catalog.Add("$Name $size $hash")
    Move-Item -LiteralPath $temporary -Destination (Join-Path $output $assetName) -Force
    "$($hash.ToLowerInvariant())  $assetName" | Set-Content -LiteralPath (Join-Path $output "$assetName.sha256") -Encoding ascii
}

function Get-Payload($Directory, $Prefix = '') {
    Get-ChildItem -LiteralPath $Directory -File -Recurse | ForEach-Object {
        @{
            Source = $_.FullName
            Path = $Prefix + [IO.Path]::GetRelativePath($Directory, $_.FullName).Replace('\', '/')
        }
    }
}

function Latest-Framework($Directory) {
    Get-ChildItem -LiteralPath $Directory -Directory | Where-Object Name -Match '^10\.0\.\d+$' |
        Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
}

try {
    $hostFxr = Latest-Framework (Join-Path $sdk 'host/fxr')
    $netCore = Latest-Framework (Join-Path $sdk 'shared/Microsoft.NETCore.App')
    $aspNet = Latest-Framework (Join-Path $sdk 'shared/Microsoft.AspNetCore.App')
    if (!$hostFxr -or !$netCore -or !$aspNet) { throw 'The local SDK must contain .NET and ASP.NET Core 10 x64.' }
    $netFiles = @(
        Get-Payload $hostFxr.FullName "host/fxr/$($hostFxr.Name)/"
        Get-Payload $netCore.FullName "shared/Microsoft.NETCore.App/$($netCore.Name)/"
        Get-Payload $aspNet.FullName "shared/Microsoft.AspNetCore.App/$($aspNet.Name)/"
        Get-ChildItem -LiteralPath $sdk -File -Filter '*.txt' | ForEach-Object {
            @{ Source = $_.FullName; Path = $_.Name }
        }
    )
    Add-Pack 'dotnet' $netFiles

    & (Join-Path $sdk 'dotnet.exe') publish (Join-Path $root 'src/SideWidget.App') -c Release -r win-x64 `
        -p:SideWidgetLiteDesktop=true --artifacts-path (Join-Path $work 'build') -o $desktop | Out-Host
    if ($LASTEXITCODE) { throw 'Lite local desktop publish failed.' }
    foreach ($name in 'vcruntime140.dll','vcruntime140_1.dll','msvcp140.dll','msvcp140_codecvt_ids.dll','concrt140.dll','vccorlib140.dll') {
        Copy-Item -LiteralPath (Join-Path $full $name) -Destination $desktop -Force
    }
    $liteFiles = @{}
    (Get-Content -LiteralPath (Join-Path $lite 'distribution-files.json') -Raw | ConvertFrom-Json).files |
        ForEach-Object { $liteFiles[$_.path] = $_.sha256 }
    $desktopFiles = [Collections.Generic.List[object]]::new()
    $shared = [Collections.Generic.List[string]]::new()
    foreach ($file in (Get-Payload $desktop | Where-Object { $_.Path -notlike '*.pdb' })) {
        if ($liteFiles.ContainsKey($file.Path) -and (Get-FileHash -LiteralPath $file.Source -Algorithm SHA256).Hash -eq $liteFiles[$file.Path]) {
            $shared.Add($file.Path)
        } else {
            $desktopFiles.Add($file)
        }
    }
    Add-Pack 'desktop' $desktopFiles $shared
    Add-Pack 'webview2' (Get-Payload (Join-Path $full 'WebView2Runtime'))
    $catalogPath = Join-Path $output "SideWidget-$Version-dependencies.txt"
    [IO.File]::WriteAllLines($catalogPath, $catalog)
    $catalogPath
} finally {
    if (![IO.Path]::GetFullPath($work).StartsWith($output.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Unsafe dependency packaging cleanup path.'
    }
    Remove-Item -LiteralPath $work -Recurse -Force
}
