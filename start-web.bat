@echo off
setlocal
set "ROOT=%~dp0"

start "DatingApp API" powershell.exe -NoExit -Command "Set-Location -LiteralPath '%ROOT%'; dotnet run --project backend/DatingApp.Api --launch-profile http"
start "DatingApp Frontend" powershell.exe -NoExit -Command "Set-Location -LiteralPath '%ROOT%frontend'; if (-not (Test-Path 'node_modules')) { npm.cmd ci }; npm.cmd run dev"

timeout /t 4 /nobreak >nul
start "" "http://localhost:5173/"
endlocal
