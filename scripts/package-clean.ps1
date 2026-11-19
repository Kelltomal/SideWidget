param(
    [Parameter(Mandatory)][string]$ReleaseDirectory,
    [Parameter(Mandatory)][string]$OutputArchive,
    [switch]$Lite,
    [string]$PayloadArchive,
    [string]$DependencyCatalog
)

$ErrorActionPreference = 'Stop'
$release = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ReleaseDirectory)
$archive = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputArchive)
$template = if ($Lite) { '-lite-win-x64.zip' } else { '-portable-win-x64.zip' }
if (!$archive.EndsWith($template, [StringComparison]::OrdinalIgnoreCase)) {
    throw "The output archive name must end in $template"
}
if ($Lite -and $PayloadArchive) {
    throw 'Lite packaging uses the Lite publish directory.'
}
if ($Lite -and !$DependencyCatalog) {
    throw 'Generate the Lite dependency assets with package-lite-dependencies.ps1 and pass -DependencyCatalog.'
}

function Get-EntryHash($Entry) {
    $stream = $Entry.Open()
    try {
        [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))
    } finally {
        $stream.Dispose()
    }
}

$temporary = "$archive.$([Guid]::NewGuid().ToString('N')).partial"
$scratch = New-Item -ItemType Directory -Path (Join-Path ([IO.Path]::GetTempPath()) "SideWidget-package-$([Guid]::NewGuid().ToString('N'))")
$sourceZip = $null
$manifestName = 'distribution-files.json'
try {
    if (!$Lite) {
        if ($PayloadArchive) {
            $sourceArchive = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($PayloadArchive)
        } else {
            $sourceArchive = Join-Path $scratch.FullName 'SideWidget-payload-win-x64.zip'
            & (Join-Path $PSScriptRoot 'package.ps1') -ReleaseDirectory $release -OutputArchive $sourceArchive | Out-Null
        }
        if ([string]::Equals($sourceArchive, $archive, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'The source and output archives must use different paths.'
        }
        $sourceZip = [IO.Compression.ZipFile]::OpenRead($sourceArchive)
        $manifestEntry = $sourceZip.GetEntry($manifestName)
        if (!$manifestEntry) {
            throw "Source archive is missing $manifestName"
        }
        $reader = [IO.StreamReader]::new($manifestEntry.Open())
        try {
            $manifest = $reader.ReadToEnd() | ConvertFrom-Json
        } finally {
            $reader.Dispose()
        }
        $manifestHash = Get-EntryHash $manifestEntry
        $manifestLength = $manifestEntry.Length
    } else {
        $manifestPath = Join-Path $release $manifestName
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $manifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
        $manifestLength = (Get-Item -LiteralPath $manifestPath).Length
    }

    $variant = if ($Lite) { 'lite' } else { 'full' }
    $correctVariant = if ($Lite) {
        $manifest.formatVersion -eq 3 -and $manifest.variant -ceq 'lite'
    } else {
        $manifest.formatVersion -eq 2 -and $null -eq $manifest.variant
    }
    if (!$correctVariant) {
        throw "Expected a $variant distribution variant in the source manifest."
    }
    $expected = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::OrdinalIgnoreCase)
    $expected.Add("App/$manifestName", @{ Hash = $manifestHash; Length = $manifestLength })
    if (!$manifest.files -or ($sourceZip -and $sourceZip.Entries.Count -ne $manifest.files.Count + 1)) {
        throw 'Source payload does not match the distribution manifest.'
    }
    foreach ($file in $manifest.files) {
        if ($file.path -notmatch '\A[^/]+(?:/[^/]+)*\z' -or $file.path -match '[\\:*?"<>|\x00-\x1f]|[. ](/|$)' -or
            $file.sha256 -notmatch '\A[a-fA-F0-9]{64}\z' -or $file.size -le 0) {
            throw "Invalid distribution manifest entry: $($file.path)"
        }
        $expected.Add("App/$($file.path)", @{ Hash = $file.sha256; Length = $file.size })
    }
    if (!$expected.ContainsKey('App/SideWidget.App.exe')) {
        throw 'Source payload is missing SideWidget.App.exe.'
    }

    # Windows supplies .NET Framework 4.x on SideWidget's supported systems.
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    $icon = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/SideWidget.App/Assets/Icons/sidemenu.ico'
    $launcher = Join-Path $scratch.FullName 'SideWidget.exe'
    [string[]]$resources = if ($Lite) {
        $catalogPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($DependencyCatalog)
        $catalogVersion = [IO.File]::ReadLines($catalogPath) | Select-Object -First 1
        if ("SideWidget-$catalogVersion-lite-win-x64.zip" -cne [IO.Path]::GetFileName($archive)) {
            throw 'Lite archive version differs from the dependency catalog.'
        }
        "/resource:$catalogPath,SideWidgetDependencies"
    }
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll `
        /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
        "/out:$launcher" "/win32icon:$icon" @resources `
        (Join-Path $PSScriptRoot 'SideWidgetLauncher.cs') (Join-Path $PSScriptRoot 'SideWidgetPrerequisites.cs') `
        (Join-Path $PSScriptRoot 'DependencyProgress.cs')
    if ($LASTEXITCODE) {
        throw 'Could not compile the SideWidget launcher.'
    }
    $expected.Add('SideWidget.exe', @{
        Hash = (Get-FileHash -LiteralPath $launcher -Algorithm SHA256).Hash
        Length = (Get-Item -LiteralPath $launcher).Length
    })
    if ($Lite) {
        $installer = Join-Path $PSScriptRoot 'Install-Dependencies.bat'
        $expected.Add('Install-Dependencies.bat', @{
            Hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
            Length = (Get-Item -LiteralPath $installer).Length
        })
    }

    [void][IO.Directory]::CreateDirectory((Split-Path $archive -Parent))
    $compression = [IO.Compression.CompressionLevel]::Fastest
    $zip = [IO.Compression.ZipFile]::Open($temporary, 'Create')
    try {
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $launcher, 'SideWidget.exe', $compression)
        if ($Lite) {
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $installer, 'Install-Dependencies.bat', $compression)
        }
        foreach ($name in @($manifestName) + @($manifest.files.path)) {
            if ($sourceZip) {
                $entry = $sourceZip.GetEntry($name)
                if (!$entry) {
                    throw "Source archive is missing $name"
                }
                $fileStream = $entry.Open()
                try {
                    $outputStream = $zip.CreateEntry("App/$name", $compression).Open()
                    try {
                        $fileStream.CopyTo($outputStream)
                    } finally {
                        $outputStream.Dispose()
                    }
                } finally {
                    $fileStream.Dispose()
                }
            } else {
                [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $release $name), "App/$name", $compression)
            }
        }
    } finally {
        $zip.Dispose()
    }

    $zip = [IO.Compression.ZipFile]::OpenRead($temporary)
    try {
        foreach ($entry in $zip.Entries) {
            $file = $expected[$entry.FullName]
            if (!$file -or $entry.Length -ne $file.Length -or (Get-EntryHash $entry) -ne $file.Hash) {
                throw "Clean archive contains an unexpected or changed file: $($entry.FullName)"
            }
            [void]$expected.Remove($entry.FullName)
        }
        if ($expected.Count) {
            throw "Clean archive is missing: $($expected.Keys -join ', ')"
        }
    } finally {
        $zip.Dispose()
    }
    $hash = (Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($archive))" | Set-Content -LiteralPath "$temporary.sha256" -Encoding ascii
    [IO.File]::Move($temporary, $archive, $true)
    [IO.File]::Move("$temporary.sha256", "$archive.sha256", $true)
    Get-Item -LiteralPath $archive | Select-Object FullName, Length
} finally {
    if ($sourceZip) {
        $sourceZip.Dispose()
    }
    foreach ($path in @($temporary, "$temporary.sha256")) {
        if ([IO.File]::Exists($path)) { [IO.File]::Delete($path) }
    }
    # Scratch contains only files generated by the launcher compiler and legacy packager.
    Get-ChildItem -LiteralPath $scratch.FullName -File -Force | Remove-Item -Force
    Remove-Item -LiteralPath $scratch.FullName
}
