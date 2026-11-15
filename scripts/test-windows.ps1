param(
    [switch]$PublicOnline,
    [switch]$NoRestore,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'These checks require Windows.' }
$projectRoot = Split-Path $PSScriptRoot -Parent
$dotnetPath = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $dotnetPath)) {
    $dotnetCommand = Get-Command dotnet -ErrorAction Stop
    $dotnetPath = $dotnetCommand.Source
}
$runArguments = @(
    'run', '--project', 'tests/SideWidget.Windows.Tests/SideWidget.Windows.Tests.csproj',
    '--configuration', $Configuration, '--no-launch-profile'
)
if ($NoRestore) { $runArguments += '--no-restore' }
$runArguments += '--'
if ($PublicOnline) { $runArguments += '--public-online' }

Push-Location $projectRoot
try {
    & $dotnetPath @runArguments
    if ($LASTEXITCODE -ne 0) { throw "Windows checks failed (exit $LASTEXITCODE)." }
}
finally { Pop-Location }
