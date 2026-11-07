@echo off
setlocal EnableExtensions
rem Rimfolk installer: double-click to copy this download into RimWorld's Mods folder.
rem
rem Use it on a copy downloaded from GitHub (Code > Download ZIP, then unzip it anywhere, e.g. Downloads).
rem It finds RimWorld by itself, on any computer: through Steam (its own folder and every Steam library it lists, on
rem any drive), the game's entry among the installed programs (Steam or GOG), and the usual game folders on every
rem drive. With more than one RimWorld it asks which one; with none found it asks for the folder. You can also drag
rem the RimWorld folder (or its Mods folder) onto this file.
rem It removes the old version from the mod folder first, then copies the mod's files over: About, 1.6, Defs,
rem Languages, Textures (and 1.7, Patches, Sounds or LoadFolders.xml if there are any). Source code, tools and notes
rem stay behind. Your mod settings and saves are kept elsewhere by RimWorld and are not touched.
rem
rem To always install into one place, put its Mods folder in MODS below, for example
rem   set "MODS=D:\SteamLibrary\steamapps\common\RimWorld\Mods"
rem Leave MODS empty to let the installer find RimWorld. FOLDER is the mod's folder name inside Mods.

set "MODS="
set "FOLDER=Rimworld Helper"

set "SOURCE=%~dp0"
if "%SOURCE:~-1%"=="\" set "SOURCE=%SOURCE:~0,-1%"
set "ELEVATED="
if /i "%~2"=="/elevated" set "ELEVATED=1"
title Install Rimfolk

echo Rimfolk installer
echo.

rem ---- Where RimWorld's Mods folder is: set above, dropped on this file, found, or asked for. ----
if defined ELEVATED set "MODS=%~1"
if defined MODS goto found
if not "%~1"=="" call :given "%~1"
if defined MODS goto found
if not "%~1"=="" echo "%~1" is not a RimWorld folder or its Mods folder, so the installer looks for RimWorld itself.
echo Looking for RimWorld...
set "COUNT=0"
call :findSteam
call :findPrograms
if %COUNT%==0 call :findDrives
if %COUNT%==0 goto askFolder
if not %COUNT%==1 goto several
set "MODS=%CAND1%\Mods"
goto found

:several
echo RimWorld is installed in more than one place:
for /l %%N in (1,1,%COUNT%) do call echo   %%N. "%%CAND%%N%%"
:pick
set "PICK="
set /p "PICK=Type the number of the one to install Rimfolk into, then press Enter (or just Enter to stop): "
if not defined PICK goto stopped
set "PICK=%PICK:"=%"
for /l %%N in (1,1,%COUNT%) do if "%PICK%"=="%%N" call set "MODS=%%CAND%%N%%\Mods"
if defined MODS goto found
echo Please type one of the numbers above.
goto pick

:askFolder
echo RimWorld wasn't found on this computer.
echo Paste the address of your RimWorld folder (the one with RimWorldWin64.exe in it) or of its Mods folder, then
echo press Enter. In Steam: right-click RimWorld, Manage, Browse local files, then copy the address bar of that window.
:askAgain
set "GIVEN="
set /p "GIVEN=Folder (or just Enter to stop): "
if not defined GIVEN goto stopped
set "GIVEN=%GIVEN:"=%"
if not defined GIVEN goto stopped
call :given "%GIVEN%"
if defined MODS goto found
echo There is no RimWorldWin64.exe in that folder, and it isn't RimWorld's Mods folder. Please try again.
goto askAgain

:found
if "%MODS:~-1%"=="\" set "MODS=%MODS:~0,-1%"
for %%I in ("%MODS%") do set "MODS=%%~fI"
set "TARGET=%MODS%\%FOLDER%"
for %%I in ("%SOURCE%\..") do set "SOURCEPARENT=%%~fI"

echo   from: "%SOURCE%"
echo   to:   "%TARGET%"
echo.

rem ---- Safety checks: never delete anything unless all of these pass. ----
if /i "%SOURCE%"=="%TARGET%" goto insideMods
if /i "%SOURCEPARENT%"=="%MODS%" goto insideMods
if not exist "%SOURCE%\About\About.xml" goto notMod
if not exist "%SOURCE%\Defs\" goto notMod
if not exist "%MODS%\" goto noMods
if "%FOLDER%"=="" goto badFolder
set "OTHER="
for /f "delims=" %%D in ('dir /b /ad "%MODS%" 2^>nul') do call :otherCopy "%MODS%\%%D"
if defined OTHER goto otherCopyFound

tasklist /fi "imagename eq RimWorldWin64.exe" 2>nul | find /i "RimWorldWin64.exe" >nul
if not errorlevel 1 goto gameRunning

rem ---- Program Files may need administrator rights: ask for them if a test file can't be written. ----
set "PROBE=%MODS%\rimfolk_write_test.tmp"
(echo test> "%PROBE%") 2>nul
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

rem ===================== Finding RimWorld =====================

rem Steam, from the registry: every library listed in its libraryfolders.vdf (written as on disk), then its own folder.
:findSteam
for /f "tokens=2,*" %%A in ('reg query "HKCU\Software\Valve\Steam" /v SteamPath 2^>nul ^| find "REG_"') do call :steamRoot "%%B"
for /f "tokens=2,*" %%A in ('reg query "HKLM\SOFTWARE\WOW6432Node\Valve\Steam" /v InstallPath 2^>nul ^| find "REG_"') do call :steamRoot "%%B"
for /f "tokens=2,*" %%A in ('reg query "HKLM\SOFTWARE\Valve\Steam" /v InstallPath 2^>nul ^| find "REG_"') do call :steamRoot "%%B"
exit /b 0

:steamRoot
set "ROOT=%~1"
if not defined ROOT exit /b 0
set "ROOT=%ROOT:/=\%"
if exist "%ROOT%\steamapps\libraryfolders.vdf" call :libraries "%ROOT%\steamapps\libraryfolders.vdf"
if exist "%ROOT%\config\libraryfolders.vdf" call :libraries "%ROOT%\config\libraryfolders.vdf"
call :add "%ROOT%\steamapps\common\RimWorld"
exit /b 0

rem Each line of the file is "key" "value"; a library's value is its folder, written with doubled backslashes.
:libraries
for /f "usebackq tokens=1,*" %%A in ("%~1") do call :library %%B
exit /b 0

:library
set "LIB=%~1"
if not defined LIB exit /b 0
set "LIB=%LIB:\\=\%"
call :add "%LIB%\steamapps\common\RimWorld"
exit /b 0

rem The installed programs' folders (Steam's "Steam App 294100", GOG's installer and GOG Galaxy's own list).
:findPrograms
for %%K in ("HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall" "HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall") do call :programs "%%~K" InstallLocation
call :programs "HKLM\SOFTWARE\WOW6432Node\GOG.com\Games" path
call :programs "HKLM\SOFTWARE\GOG.com\Games" path
exit /b 0

:programs
for /f "tokens=1,2,*" %%A in ('reg query "%~1" /s /v %2 2^>nul ^| find "REG_SZ"') do if /i "%%A"=="%2" call :add "%%C"
exit /b 0

rem Last, the usual game folders on every drive.
:findDrives
for %%D in (C D E F G H I J K L M N O P Q R S T U V W X Y Z) do call :drive %%D
exit /b 0

:drive
for %%P in ("Program Files (x86)\Steam" "Program Files\Steam" "Steam" "SteamLibrary" "Games\Steam" "Games\SteamLibrary" "Spiele\Steam" "Spiele\SteamLibrary") do call :add "%1:\%%~P\steamapps\common\RimWorld"
for %%P in ("GOG Games\RimWorld" "Program Files (x86)\GOG Galaxy\Games\RimWorld" "Program Files\GOG Galaxy\Games\RimWorld" "Games\RimWorld" "RimWorld") do call :add "%1:\%%~P"
exit /b 0

rem A RimWorld folder (it has RimWorldWin64.exe) joins the list once, however it was found.
:add
set "P=%~1"
if not defined P exit /b 0
if "%P:~-1%"=="\" set "P=%P:~0,-1%"
if not exist "%P%\RimWorldWin64.exe" exit /b 0
for %%I in ("%P%") do set "P=%%~fI"
set "SAME="
for /l %%N in (1,1,%COUNT%) do call :sameAs %%N
if defined SAME exit /b 0
set /a COUNT+=1
set "CAND%COUNT%=%P%"
exit /b 0

:sameAs
call set "C=%%CAND%1%%"
if /i "%C%"=="%P%" set "SAME=1"
exit /b 0

rem A folder dropped on this file or typed in: RimWorld's own folder or its Mods folder (or RimWorldWin64.exe itself).
:given
set "G=%~1"
if not defined G exit /b 0
if /i "%~nx1"=="RimWorldWin64.exe" if exist "%~1" set "G=%~dp1"
if "%G:~-1%"=="\" set "G=%G:~0,-1%"
if not exist "%G%\RimWorldWin64.exe" goto givenMods
set "MODS=%G%\Mods"
exit /b 0
:givenMods
if exist "%G%\..\RimWorldWin64.exe" if exist "%G%\" set "MODS=%G%"
exit /b 0

rem Another folder in Mods holding Rimfolk (its package id in About.xml): two copies would clash.
:otherCopy
if /i "%~nx1"=="%FOLDER%" exit /b 0
if not exist "%~1\About\About.xml" exit /b 0
find "<packageId>silvanalig.colonycouncil</packageId>" "%~1\About\About.xml" >nul 2>nul
if not errorlevel 1 set "OTHER=%~1"
exit /b 0

rem Copies one folder of the mod; robocopy exit codes below 8 mean success.
:copyDir
if not exist "%SOURCE%\%~1\" exit /b 0
robocopy "%SOURCE%\%~1" "%TARGET%\%~1" /E /R:2 /W:1 /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 set "FAILED=1"
exit /b 0

rem ===================== Asking for rights, and what went wrong =====================

:elevate
if defined ELEVATED goto noWrite
echo The mod folder needs administrator rights. Asking for them...
set "SELFQ=%~f0"
set "SELFQ=%SELFQ:'=''%"
set "MODSQ=%MODS:'=''%"
powershell -NoProfile -Command "Start-Process -FilePath '%SELFQ%' -ArgumentList '\"%MODSQ%\" /elevated' -Verb RunAs"
if errorlevel 1 goto noAdmin
exit /b 0

:insideMods
echo This copy is already inside RimWorld's Mods folder, so there is nothing to install.
echo Run this file from a copy you downloaded and unzipped somewhere else (e.g. Downloads). A copy cloned with
echo GitHub Desktop updates itself with Update Rimfolk.bat instead.
goto fail

:notMod
echo This doesn't look like a full Rimfolk download: About\About.xml or Defs is missing next to this file.
echo Unzip the whole download first, then run this file from inside the unzipped folder.
goto fail

:noMods
echo RimWorld's Mods folder wasn't found at:
echo   "%MODS%"
echo If MODS is set at the top of this file, correct it or empty it to let the installer find RimWorld.
echo Otherwise make a folder named Mods next to RimWorldWin64.exe, and try again.
goto fail

:badFolder
echo FOLDER is empty. Open this file in Notepad and set FOLDER to the mod's folder name.
goto fail

:otherCopyFound
echo Another copy of Rimfolk is already in the Mods folder:
echo   "%OTHER%"
echo Two copies would clash in RimWorld's mod list. Delete that folder first, then run this file again. If it was
echo cloned with GitHub Desktop, keep it instead and update it with its own Update Rimfolk.bat.
goto fail

:gameRunning
echo RimWorld is running. Close it first, then run this file again.
goto fail

:noWrite
echo The Mods folder can't be written to, even with administrator rights:
echo   "%MODS%"
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

:stopped
echo Stopped; nothing was changed.
goto fail

:fail
echo.
pause
exit /b 1
