$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    New-Item -ItemType Directory -Force .tools | Out-Null
    if(!(Test-Path '.tools/dotnet/dotnet.exe')) {
        Write-Host 'Downloading local .NET SDK 10.0.403. No system installation.'
        Invoke-WebRequest 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-win-x64.zip' -OutFile '.tools/dotnet.zip'
        if((Get-FileHash '.tools/dotnet.zip' -Algorithm SHA512).Hash -ne '7a114c318fa7f1d9822991b534b2814d0e78386ceda816b371f5141acefe741d87ccb606ede06cadac8f9ac5070e32b708a67bbd1619ff5e5a7858a27874ef48'){throw 'SDK hash mismatch'}
        Expand-Archive '.tools/dotnet.zip' '.tools/dotnet' -Force
    }
} finally {Pop-Location}

