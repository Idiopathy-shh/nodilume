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
    dotnet restore tests/Nodilume.Smoke --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed' }
    dotnet build tests/Nodilume.Smoke -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed' }
} finally { Pop-Location }
