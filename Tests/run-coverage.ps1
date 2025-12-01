$OriginalLocation = Get-Location

# Get script directory and change to Tests folder
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

try {
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "TestResults"
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "coveragereport"

    Write-Host "Running tests..." -ForegroundColor Cyan
    dotnet test --collect:"XPlat Code Coverage"

    $testExitCode = $LASTEXITCODE
    if ($testExitCode -ne 0) {
        Write-Host "Some tests failed (exit code $testExitCode)" -ForegroundColor Yellow
        Write-Host "Continuing to generate coverage report..." -ForegroundColor Cyan
    } else {
        Write-Host "All tests passed!" -ForegroundColor Green
    }

    Write-Host "`nGenerating coverage report..." -ForegroundColor Cyan
    reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Open: coveragereport\index.html" -ForegroundColor Yellow
    } else {
        Write-Host "`nFailed to generate coverage report" -ForegroundColor Red
    }
}
finally {
    # Restore original location
    Set-Location $OriginalLocation
}
