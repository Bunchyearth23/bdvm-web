$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$manifest = Get-Content -Raw (Join-Path $projectRoot 'module.json') | ConvertFrom-Json
$stage = Join-Path $projectRoot ("artifacts\BDVM.Web-{0}" -f $manifest.version)
dotnet build (Join-Path $projectRoot 'BDVM.Web.csproj') -c Release
dotnet run --project (Join-Path $projectRoot 'tests\BDVM.Web.Tests.csproj') -c Release
node --test (Join-Path $projectRoot 'tests\shell.test.cjs')
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item -Force (Join-Path $projectRoot 'bin\Release\net48\BDVM.Web.dll'), (Join-Path $projectRoot 'module.json'), (Join-Path $projectRoot 'README.md'), (Join-Path $projectRoot 'LICENSE') $stage
Copy-Item -Recurse -Force (Join-Path $projectRoot 'Assets') $stage
Compress-Archive -Force (Join-Path $stage '*') ("$stage.zip")
Write-Output ("Packaged {0}" -f "$stage.zip")
