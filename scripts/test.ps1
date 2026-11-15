$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    & ./.tools/dotnet/dotnet.exe run --project tests/SideWidget.Tests
    if($LASTEXITCODE){throw 'C# checks failed'}
    & ./.tools/dotnet/dotnet.exe run --project tests/SideWidget.Remote.Tests
    if($LASTEXITCODE){throw 'Phone remote checks failed'}
    Push-Location src/SideWidget.Web
    try{& npm test;if($LASTEXITCODE){throw 'Web checks failed'};& npm run build;if($LASTEXITCODE){throw 'Web build failed'}}finally{Pop-Location}
}finally{Pop-Location}
