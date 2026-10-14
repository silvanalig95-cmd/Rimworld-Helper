@echo off
setlocal EnableExtensions
rem Rimfolk installer: double-click to copy this download into RimWorld's Mods folder.
rem
rem Use it on a copy downloaded from GitHub (Code > Download ZIP, then unzip it anywhere, e.g. Downloads).
rem It removes the old version from the mod folder first, then copies the mod's files over: About, 1.6, Defs,
rem Languages, Textures (and 1.7, Patches, Sounds or LoadFolders.xml if there are any). Source code, tools and notes
rem stay behind. Your mod settings and saves are kept elsewhere by RimWorld and are not touched.
rem
rem To install somewhere else, change MODS and FOLDER below.

set "MODS=C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods"
set "FOLDER=Rimworld Helper"

set "TARGET=%MODS%\%FOLDER%"
set "SOURCE=%~dp0"
if "%SOURCE:~-1%"=="\" set "SOURCE=%SOURCE:~0,-1%"
title Install Rimfolk

echo Rimfolk installer
echo   from: "%SOURCE%"
echo   to:   "%TARGET%"
echo.

rem ---- Safety checks: never delete anything unless all of these pass. ----
if /i "%SOURCE%"=="%TARGET%" goto insideTarget
if not exist "%SOURCE%\About\About.xml" goto notMod
if not exist "%SOURCE%\Defs\" goto notMod
if not exist "%MODS%\" goto noMods
if "%FOLDER%"=="" goto badFolder

tasklist /fi "imagename eq RimWorldWin64.exe" 2>nul | find /i "RimWorldWin64.exe" >nul
if not errorlevel 1 goto gameRunning

rem ---- Program Files may need administrator rights: ask for them if a test file can't be written. ----
set "PROBE=%MODS%\rimfolk_write_test.tmp"
echo test> "%PROBE%" 2>nul
if not exist "%PROBE%" goto elevate
del "%PROBE%" >nul 2>nul

rem ---- Remove the old version. ----
if not exist "%TARGET%\" goto copy
echo Removing the old version...
rmdir /s /q "%TARGET%"
if exist "%TARGET%\" goto removeFailed

:copy
echo Copying the mod files...
mkdir "%TARGET%"
if errorlevel 1 goto copyFailed
for %%D in (About 1.6 1.7 Defs Languages Textures Patches Sounds) do call :copyDir "%%D"
if defined FAILED goto copyFailed
if exist "%SOURCE%\LoadFolders.xml" copy /y "%SOURCE%\LoadFolders.xml" "%TARGET%\" >nul

echo.
echo Done. Rimfolk is installed in:
echo   "%TARGET%"
echo Start RimWorld and make sure Rimfolk is active in the mod list (after Harmony).
echo.
pause
exit /b 0

rem Copies one folder of the mod; robocopy exit codes below 8 mean success.
:copyDir
if not exist "%SOURCE%\%~1\" exit /b 0
robocopy "%SOURCE%\%~1" "%TARGET%\%~1" /E /R:2 /W:1 /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 set "FAILED=1"
exit /b 0

:elevate
echo The mod folder needs administrator rights. Asking for them...
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
if errorlevel 1 goto noAdmin
exit /b 0

:insideTarget
echo This file is inside the mod folder itself. Run it from the copy you downloaded and unzipped instead.
goto fail

:notMod
echo This doesn't look like a full Rimfolk download: About\About.xml or Defs is missing next to this file.
echo Unzip the whole download first, then run this file from inside the unzipped folder.
goto fail

:noMods
echo RimWorld's Mods folder wasn't found at:
echo   "%MODS%"
echo Open this file in Notepad and change MODS to where RimWorld is installed.
goto fail

:badFolder
echo FOLDER is empty. Open this file in Notepad and set FOLDER to the mod's folder name.
goto fail

:gameRunning
echo RimWorld is running. Close it first, then run this file again.
goto fail

:removeFailed
echo The old version couldn't be removed completely. Close RimWorld and any window showing that folder, then try again.
goto fail

:copyFailed
echo Copying failed. Check that the disk isn't full and that nothing has those files open, then try again.
goto fail

:noAdmin
echo Administrator rights were refused, so nothing was changed.
goto fail

:fail
echo.
pause
exit /b 1
