$ErrorActionPreference = 'Stop'
dotnet restore
dotnet build .\QuickLaunch.sln -c Release
Write-Host 'Build complete.' -ForegroundColor Green
