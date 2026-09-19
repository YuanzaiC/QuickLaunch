$ErrorActionPreference = 'Stop'
dotnet publish .\QuickLaunch.csproj -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
Write-Host 'Publish complete.' -ForegroundColor Green
