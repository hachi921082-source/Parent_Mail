@echo off
rem Parent_Mail launcher: builds the app (quiet) and starts it.
rem Safe to run from any folder, including paths with non-ASCII characters.
setlocal
set "ROOT=%~dp0"
set "EXE=%ROOT%src\ParentMail.App\bin\Debug\net10.0-windows\Parent_Mail.exe"

tasklist /FI "IMAGENAME eq Parent_Mail.exe" 2>nul | find /I "Parent_Mail.exe" >nul
if not errorlevel 1 (
    echo Parent_Mail is already running. Close it first to rebuild.
    pause
    exit /b 1
)

echo Building...
dotnet build "%ROOT%src\ParentMail.App\ParentMail.App.csproj" -v q --nologo
if errorlevel 1 (
    echo Build failed.
    pause
    exit /b 1
)

start "" "%EXE%"
endlocal
