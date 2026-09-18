$ErrorActionPreference = 'Stop'
$exe = Join-Path (Split-Path $PSScriptRoot -Parent) 'src\Nodilume.Desktop\bin\Release\net10.0-windows\Nodilume.exe'
if (!(Test-Path $exe)) { throw 'Run scripts/build.ps1 first.' }
Start-Process $exe
