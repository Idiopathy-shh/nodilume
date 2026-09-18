$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    Push-Location src\Nodilume.Viewer
    try {
        npm.cmd ci --ignore-scripts
        if ($LASTEXITCODE -ne 0) { throw 'npm ci failed' }
        npm.cmd test
        if ($LASTEXITCODE -ne 0) { throw 'Viewer tests failed' }
        npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'Viewer build failed' }
    } finally { Pop-Location }
    dotnet restore tests/Nodilume.Tests --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked test restore failed' }
    dotnet build tests/Nodilume.Tests -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Domain/integration test build failed' }
    dotnet run --project tests/Nodilume.Tests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Domain/integration tests failed' }

    dotnet restore tests/Nodilume.Smoke --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked desktop restore failed' }
    dotnet build tests/Nodilume.Smoke -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed' }

    dotnet restore tests/Nodilume.ScaleSmoke --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked scale smoke restore failed' }
    dotnet build tests/Nodilume.ScaleSmoke -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Scale smoke build failed' }

    dotnet restore tools/Nodilume.Benchmarks --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked benchmark restore failed' }
    dotnet build tools/Nodilume.Benchmarks -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Benchmark tool build failed' }
} finally { Pop-Location }
