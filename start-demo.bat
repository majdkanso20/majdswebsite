@echo off
title Majd's Platform - demo launcher
cd /d "%~dp0"

echo.
echo  Starting Majd's Platform for a demo...
echo  (two windows will open: the API and the web app - leave both open)
echo.

start "Majd API" cmd /k "dotnet run --project src\MajdsApp.Api --launch-profile http"
start "Majd Web" cmd /k "cd /d src\majds-app-web && npm start"

echo  Waiting until both are ready (the first start can take up to a minute)...
:wait
timeout /t 3 /nobreak >nul
powershell -NoProfile -Command "try { Invoke-WebRequest http://localhost:5156/health/ready -UseBasicParsing -TimeoutSec 2 | Out-Null; Invoke-WebRequest http://localhost:4200 -UseBasicParsing -TimeoutSec 2 | Out-Null; exit 0 } catch { exit 1 }"
if errorlevel 1 goto wait

echo.
echo  Ready. Opening http://localhost:4200
start http://localhost:4200
echo.
echo  Demo admin:  demo.admin@example.com   /   Demo1234!
echo  Close the two windows ("Majd API" and "Majd Web") when you are finished.
echo.
pause
