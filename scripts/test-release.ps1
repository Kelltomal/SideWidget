param([switch]$PublicOnline)

# Run only in the final integration slot, after source edits have stopped.
# Builds tests and web dist; does not launch SideWidget, publish, package or use Git.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$dotnetPath = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $dotnetPath)) { throw 'Run scripts/bootstrap.ps1 before release checks.' }
if ($env:OS -ne 'Windows_NT') { throw 'Final release checks require Windows.' }

$logDirectory = Join-Path $projectRoot ('artifacts/verification/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$results = [System.Collections.Generic.List[object]]::new()

function Invoke-ReleaseCheck {
    param([string]$Name, [string]$Executable, [string[]]$Arguments)
    $started = [DateTimeOffset]::UtcNow
    & $Executable @Arguments 2>&1 | Tee-Object -FilePath (Join-Path $logDirectory "$Name.log") | Out-Host
    $exitCode = $LASTEXITCODE
    $results.Add([pscustomobject]@{name=$Name; exitCode=$exitCode; startedUtc=$started; finishedUtc=[DateTimeOffset]::UtcNow})
    if ($exitCode -ne 0) { throw "$Name failed (exit $exitCode). See $logDirectory" }
}

Push-Location $projectRoot
try {
    Invoke-ReleaseCheck 'core' $dotnetPath @('run','--project','tests/SideWidget.Tests','-c','Release')
    Invoke-ReleaseCheck 'web-tests' 'npm.cmd' @('--prefix','src/SideWidget.Web','test','--','--run')
    Invoke-ReleaseCheck 'web-build' 'npm.cmd' @('--prefix','src/SideWidget.Web','run','build')
    Invoke-ReleaseCheck 'windows' $dotnetPath @('run','--project','tests/SideWidget.Windows.Tests','-c','Release','--no-launch-profile')
    if ($PublicOnline) {
        Invoke-ReleaseCheck 'public-online' $dotnetPath @('run','--project','tests/SideWidget.Tests','-c','Release','--','--public-online')
    }
    Write-Host "Automated checks passed. Native publish and GUI smoke still required. Logs: $logDirectory"
}
finally {
    $results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $logDirectory 'checks.json') -Encoding utf8
    Pop-Location
}
