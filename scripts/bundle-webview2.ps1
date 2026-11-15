param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$version='156.0.4314.58'
$expected='857dcfd6f7035508a29645fa2978c3a6a841f220df77cc366d318894e19f4792'
$url='https://msedge.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/b7919cc9-680d-4253-bf32-732988e68aa6/Microsoft.WebView2.FixedVersionRuntime.156.0.4314.58.x64.cab'
$cache=Join-Path $root '.tools/webview2'
$cab=Join-Path $cache 'runtime.cab'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
if(!(Test-Path $cab) -or (Get-FileHash $cab -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected){
    Invoke-WebRequest -Uri $url -OutFile "$cab.download"
    if((Get-FileHash "$cab.download" -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected){throw 'WebView2 CAB checksum mismatch'}
    Move-Item -LiteralPath "$cab.download" -Destination $cab -Force
}
$expandDirectory=[IO.Path]::GetFullPath((Join-Path $cache ('expand-'+[Guid]::NewGuid().ToString('N'))))
$extracted=Join-Path $expandDirectory "Microsoft.WebView2.FixedVersionRuntime.$version.x64"
try {
    # Re-extract the hash-verified CAB: a partially removed cache is not a valid runtime.
    New-Item -ItemType Directory -Path $expandDirectory -Force | Out-Null
    & expand.exe $cab '-F:*' $expandDirectory | Out-Null
    if($LASTEXITCODE){throw 'WebView2 CAB extraction failed'}
    $signature=Get-AuthenticodeSignature (Join-Path $extracted 'msedgewebview2.exe')
    if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike '*O=Microsoft Corporation,*'){throw 'WebView2 Microsoft signature verification failed'}
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    # These Copilot executables ship in the upstream CAB. SideWidget uses the WebView2 browser.
    & robocopy.exe $extracted $Destination /E /XF copilot_setup.exe mscopilot.exe /R:2 /W:1 /NJH /NJS /NFL /NDL /NP
    if($LASTEXITCODE -ge 8){throw "WebView2 copy failed (robocopy exit code $LASTEXITCODE)"}
    $global:LASTEXITCODE=0
    $copilot=Get-ChildItem -LiteralPath $Destination -File -Recurse |
        Where-Object Name -In 'copilot_setup.exe','mscopilot.exe' | Select-Object -First 1
    if($copilot){throw "Unexpected Copilot executable in WebView2 payload: $($copilot.Name)"}
} finally {
    $cacheRoot=[IO.Path]::GetFullPath($cache)+'\'
    if(!$expandDirectory.StartsWith($cacheRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe extraction cleanup path'}
    if(Test-Path -LiteralPath $expandDirectory){Remove-Item -LiteralPath $expandDirectory -Recurse -Force}
}
Write-Host "Bundled Microsoft WebView2 $version x64"
