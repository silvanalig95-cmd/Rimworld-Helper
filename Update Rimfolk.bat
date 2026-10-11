@echo off
setlocal
rem Rimfolk updater: double-click to bring this folder up to date with GitHub.
rem
rem Works when this folder was cloned from GitHub (see "Installing" in README.md). It needs Git: either
rem GitHub Desktop (its built-in Git is used) or Git for Windows.
rem Anything changed by hand inside this folder is replaced by the GitHub version. Your mod settings and
rem saves are kept elsewhere by RimWorld and are not touched.

cd /d "%~dp0"

set "GIT="
where git >nul 2>nul && set "GIT=git"
if not defined GIT (
  for /d %%D in ("%LOCALAPPDATA%\GitHubDesktop\app-*") do (
    if exist "%%D\resources\app\git\cmd\git.exe" set "GIT=%%D\resources\app\git\cmd\git.exe"
  )
)
if not defined GIT (
  echo Git wasn't found. Install GitHub Desktop from https://desktop.github.com and try again.
  pause
  exit /b 1
)
if not exist ".git" (
  echo This folder was not cloned from GitHub, so it can't update itself.
  echo See "Installing" in README.md to set it up once.
  pause
  exit /b 1
)

set "BRANCH="
for /f "delims=" %%B in ('call "%GIT%" rev-parse --abbrev-ref HEAD') do set "BRANCH=%%B"
if not defined BRANCH goto failed

echo Updating Rimfolk (%BRANCH%)...
"%GIT%" fetch origin
if errorlevel 1 goto failed
"%GIT%" reset --hard "origin/%BRANCH%"
if errorlevel 1 goto failed

echo.
"%GIT%" log -1 --format="Now at: %%s (%%cr)"
echo.
echo Done. If RimWorld is running, restart it to load the new version.
pause
exit /b 0

:failed
echo.
echo The update didn't work (see the messages above). Check your internet connection and try again.
pause
exit /b 1
