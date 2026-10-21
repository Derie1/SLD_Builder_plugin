@echo off
rem SLD Builder installer. Run from the unpacked release folder (next to SldBuilder.bundle.zip).
rem   INSTALL.BAT              - interactive: asks for the language, then for the AutoCAD version
rem   INSTALL.BAT 2026 [RU|EN] - install for AutoCAD 2026 without questions (also: A = all supported, U = uninstall)
rem The file is UTF-8 without BOM: the first command switches the console to UTF-8.
rem Messages must not contain parentheses: they are expanded inside ( ) blocks.
chcp 65001 >nul
setlocal EnableExtensions DisableDelayedExpansion

rem ---- AutoCAD year -> release series. Add new versions here and to installer\PackageContents. ----
set "SERIES_2019=R23.0"
set "SERIES_2020=R23.1"
set "SERIES_2021=R24.0"
set "SERIES_2022=R24.1"
set "SERIES_2023=R24.2"
set "SERIES_2024=R24.3"
set "SERIES_2025=R25.0"
set "SERIES_2026=R25.1"
set "SERIES_2027=R26.0"
set "KNOWN_YEARS=2019 2020 2021 2022 2023 2024 2025 2026 2027"
set "MIN_YEAR=2025"

set "PACKAGE=%~dp0SldBuilder.bundle.zip"
rem SLDB_TARGET overrides the install folder (used for testing).
if defined SLDB_TARGET (set "TARGET=%SLDB_TARGET%") else set "TARGET=%APPDATA%\Autodesk\ApplicationPlugins\SldBuilder.bundle"
set "INTERACTIVE=1"
if not "%~1"=="" set "INTERACTIVE=0"
set "RC=0"

title SLD Builder

rem ---------------------------------------------------------------- language
set "UI_LANG=%~2"
if "%INTERACTIVE%"=="0" goto :lang_selected
echo.
echo   1 - Русский
echo   2 - English
set "UI_LANG="
set /p "UI_LANG=Выберите язык / Select language [1/2]: "
if "%UI_LANG%"=="2" set "UI_LANG=EN"
:lang_selected
if /i "%UI_LANG%"=="EN" (call :messages_en) else call :messages_ru

rem ---------------------------------------------------------------- version
echo.
echo ================================================================
echo   %M_TITLE%
echo ================================================================
echo.
echo %M_FOUND_HEADER%
set "FOUND="
for %%Y in (%KNOWN_YEARS%) do call :detect %%Y
if not defined FOUND echo   %M_NONE_FOUND%
echo.
echo %M_SUPPORTS%
if exist "%TARGET%\versions.txt" call :show_installed

set "CHOICE=%~1"
if "%INTERACTIVE%"=="0" goto :dispatch
echo.
echo %M_PROMPT_YEAR%
echo   A - %M_PROMPT_ALL%
echo   U - %M_PROMPT_UNINSTALL%
echo   Q - %M_PROMPT_QUIT%
set /p "CHOICE=%M_PROMPT_CHOICE% "

:dispatch
if /i "%CHOICE%"=="Q" goto :end
if /i "%CHOICE%"=="U" goto :uninstall
if /i "%CHOICE%"=="A" (
    set "SERIES=ALL"
    goto :install
)

echo %CHOICE%| findstr /r /x "20[0-9][0-9]" >nul || goto :bad_input
if %CHOICE% LSS %MIN_YEAR% goto :unsupported
if not defined SERIES_%CHOICE% goto :unknown_version
call set "SERIES=%%SERIES_%CHOICE%%%"

reg query "HKLM\SOFTWARE\Autodesk\AutoCAD\%SERIES%" >nul 2>&1 && goto :install
echo.
call echo %M_NOT_FOUND%
if "%INTERACTIVE%"=="0" goto :install
call set "CONFIRM=%M_CONFIRM%"
choice /c YN /m "%CONFIRM%"
if errorlevel 2 goto :end
goto :install

rem ---------------------------------------------------------------- install
:install
call :check_acad_closed || goto :fail
if not exist "%PACKAGE%" (
    echo.
    echo %M_NO_PACKAGE_1%
    echo %M_NO_PACKAGE_2%
    goto :fail
)

rem Versions installed earlier are kept: running the installer for 2025 and then for 2026 gives both.
set "LIST="
if exist "%TARGET%\versions.txt" set /p LIST=<"%TARGET%\versions.txt"
if "%SERIES%"=="ALL" set "LIST=ALL"
if "%LIST%"=="ALL" goto :list_ready
if not defined LIST (
    set "LIST=%SERIES%"
    goto :list_ready
)
echo  %LIST% | findstr /c:" %SERIES% " >nul || set "LIST=%LIST% %SERIES%"
:list_ready

set "WORK=%TEMP%\SldBuilder_%RANDOM%%RANDOM%"
mkdir "%WORK%" >nul 2>&1
echo.
echo %M_EXTRACTING%
rem Expand-Archive first: Windows tar.exe skips the Cyrillic template file name. tar is only a fallback.
powershell -NoProfile -Command "Expand-Archive -LiteralPath '%PACKAGE%' -DestinationPath '%WORK%' -Force" >nul 2>&1
if errorlevel 1 tar -xf "%PACKAGE%" -C "%WORK%" >nul 2>&1
if not exist "%WORK%\Contents\Win64\SldBuilder.Acad.dll" (
    echo %M_EXTRACT_FAILED% %PACKAGE%
    rmdir /s /q "%WORK%" >nul 2>&1
    goto :fail
)
if not exist "%WORK%\Contents\Win64\Templates\DB_имя щита_(как хотите)_v1.5.xlsx" echo %M_RU_TEMPLATE_MISSING%

echo %M_COPYING% %TARGET%
robocopy "%WORK%" "%TARGET%" /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS /NP >nul
set "COPY_RC=%ERRORLEVEL%"
rmdir /s /q "%WORK%" >nul 2>&1
if %COPY_RC% GEQ 8 (
    echo %M_COPY_FAILED% %COPY_RC%
    goto :fail
)

rem PackageContents.xml = head + one Components block per selected version + tail.
set "PARTS=%TARGET%\Installer\PackageContents"
set "MANIFEST=%TARGET%\PackageContents.xml"
copy /y /b "%PARTS%\head.xml" "%MANIFEST%" >nul
for %%S in (%LIST%) do copy /y /b "%MANIFEST%"+"%PARTS%\%%S.xml" "%MANIFEST%" >nul
copy /y /b "%MANIFEST%"+"%PARTS%\tail.xml" "%MANIFEST%" >nul
>"%TARGET%\versions.txt" echo %LIST%

echo.
echo %M_DONE%
echo   %TARGET%
call :show_installed
echo %M_RUN_HINT%
goto :end

rem ---------------------------------------------------------------- uninstall
:uninstall
call :check_acad_closed || goto :fail
if not exist "%TARGET%" (
    echo.
    echo %M_NOT_INSTALLED%
    goto :end
)
rmdir /s /q "%TARGET%"
if exist "%TARGET%" (
    echo %M_REMOVE_FAILED% %TARGET%
    goto :fail
)
echo.
echo %M_REMOVED%
goto :end

rem ---------------------------------------------------------------- errors
:unsupported
echo.
call echo %M_UNSUPPORTED_1%
call echo %M_UNSUPPORTED_2%
call echo %M_UNSUPPORTED_3%
goto :fail

:unknown_version
echo.
call echo %M_UNKNOWN_1%
echo %M_UNKNOWN_2%
goto :fail

:bad_input
echo.
call echo %M_BAD_INPUT%
goto :fail

:fail
set "RC=1"
goto :end

:end
echo.
if "%INTERACTIVE%"=="0" exit /b %RC%
echo %M_PRESS_KEY%
pause >nul
exit /b %RC%

rem ---------------------------------------------------------------- subroutines
:detect
call set "S=%%SERIES_%1%%"
reg query "HKLM\SOFTWARE\Autodesk\AutoCAD\%S%" >nul 2>&1 || exit /b 0
set "FOUND=1"
if %1 GEQ %MIN_YEAR% (echo   AutoCAD %1  - %M_SUPPORTED%) else echo   AutoCAD %1  - %M_NOT_SUPPORTED%
exit /b 0

:show_installed
set "INSTALLED="
set /p INSTALLED=<"%TARGET%\versions.txt"
if "%INSTALLED%"=="ALL" (
    echo %M_LOADS_ALL%
    exit /b 0
)
set "YEARS="
for %%Y in (%KNOWN_YEARS%) do call :year_if_installed %%Y
echo %M_LOADS_IN%%YEARS%
exit /b 0

:year_if_installed
call set "S=%%SERIES_%1%%"
echo  %INSTALLED% | findstr /c:" %S% " >nul && set "YEARS=%YEARS% %1"
exit /b 0

:check_acad_closed
tasklist /fi "imagename eq acad.exe" 2>nul | find /i "acad.exe" >nul || exit /b 0
echo.
echo %M_CLOSE_ACAD%
exit /b 1

rem ---------------------------------------------------------------- messages
rem %%CHOICE%% is stored as %CHOICE% and expanded later by "call echo".

:messages_ru
set "M_TITLE=SLD Builder - установка плагина для AutoCAD"
set "M_FOUND_HEADER=Найденные на компьютере версии AutoCAD:"
set "M_NONE_FOUND=не найдено ни одной"
set "M_SUPPORTED=поддерживается"
set "M_NOT_SUPPORTED=не поддерживается"
set "M_SUPPORTS=Плагин поддерживает AutoCAD %MIN_YEAR% и новее."
set "M_PROMPT_YEAR=Введите год версии AutoCAD, например 2026."
set "M_PROMPT_ALL=для всех поддерживаемых версий"
set "M_PROMPT_UNINSTALL=удалить плагин"
set "M_PROMPT_QUIT=выход"
set "M_PROMPT_CHOICE=Ваш выбор:"
set "M_NOT_FOUND=ВНИМАНИЕ: AutoCAD %%CHOICE%% не найдена на этом компьютере."
set "M_CONFIRM=Всё равно установить для AutoCAD %%CHOICE%%"
set "M_NO_PACKAGE_1=ОШИБКА: рядом с INSTALL.BAT нет файла SldBuilder.bundle.zip."
set "M_NO_PACKAGE_2=Распакуйте весь архив с релизом в папку и запустите INSTALL.BAT оттуда."
set "M_EXTRACTING=Распаковка..."
set "M_EXTRACT_FAILED=ОШИБКА: не удалось распаковать"
set "M_RU_TEMPLATE_MISSING=ВНИМАНИЕ: русский шаблон Excel не распакован, доступен только английский."
set "M_COPYING=Копирование в"
set "M_COPY_FAILED=ОШИБКА: не удалось скопировать файлы, код robocopy"
set "M_DONE=Готово. Плагин установлен в:"
set "M_RUN_HINT=Запустите AutoCAD и введите команду SLDBUILD."
set "M_NOT_INSTALLED=Плагин не установлен."
set "M_REMOVE_FAILED=ОШИБКА: не удалось удалить"
set "M_REMOVED=Плагин удалён."
set "M_UNSUPPORTED_1=AutoCAD %%CHOICE%% не поддерживается."
set "M_UNSUPPORTED_2=SLD Builder работает в AutoCAD %MIN_YEAR% и новее: плагину нужна платформа .NET 8,"
set "M_UNSUPPORTED_3=а AutoCAD %%CHOICE%% использует .NET Framework."
set "M_UNKNOWN_1=AutoCAD %%CHOICE%% ещё не проверялся с этой версией установщика."
set "M_UNKNOWN_2=Выберите A - тогда плагин будет загружаться во всех версиях начиная с %MIN_YEAR%."
set "M_BAD_INPUT=Не понял ответ "%%CHOICE%%". Введите год, например 2026, или A, U, Q."
set "M_LOADS_ALL=Плагин загружается во всех версиях AutoCAD начиная с %MIN_YEAR%."
set "M_LOADS_IN=Плагин загружается в AutoCAD:"
set "M_CLOSE_ACAD=Закройте AutoCAD и запустите установку снова: файлы плагина заняты запущенным AutoCAD."
set "M_PRESS_KEY=Нажмите любую клавишу для выхода..."
exit /b 0

:messages_en
set "M_TITLE=SLD Builder - AutoCAD plugin setup"
set "M_FOUND_HEADER=AutoCAD versions found on this computer:"
set "M_NONE_FOUND=none found"
set "M_SUPPORTED=supported"
set "M_NOT_SUPPORTED=not supported"
set "M_SUPPORTS=The plugin supports AutoCAD %MIN_YEAR% and newer."
set "M_PROMPT_YEAR=Enter the AutoCAD version year, for example 2026."
set "M_PROMPT_ALL=all supported versions"
set "M_PROMPT_UNINSTALL=uninstall the plugin"
set "M_PROMPT_QUIT=quit"
set "M_PROMPT_CHOICE=Your choice:"
set "M_NOT_FOUND=WARNING: AutoCAD %%CHOICE%% was not found on this computer."
set "M_CONFIRM=Install for AutoCAD %%CHOICE%% anyway"
set "M_NO_PACKAGE_1=ERROR: SldBuilder.bundle.zip was not found next to INSTALL.BAT."
set "M_NO_PACKAGE_2=Unpack the whole release archive to a folder and run INSTALL.BAT from there."
set "M_EXTRACTING=Extracting..."
set "M_EXTRACT_FAILED=ERROR: could not extract"
set "M_RU_TEMPLATE_MISSING=WARNING: the Russian Excel template was not extracted, only the English one is available."
set "M_COPYING=Copying to"
set "M_COPY_FAILED=ERROR: could not copy the files, robocopy code"
set "M_DONE=Done. The plugin is installed in:"
set "M_RUN_HINT=Start AutoCAD and run the SLDBUILD command."
set "M_NOT_INSTALLED=The plugin is not installed."
set "M_REMOVE_FAILED=ERROR: could not remove"
set "M_REMOVED=The plugin has been removed."
set "M_UNSUPPORTED_1=AutoCAD %%CHOICE%% is not supported."
set "M_UNSUPPORTED_2=SLD Builder runs in AutoCAD %MIN_YEAR% and newer: the plugin needs .NET 8,"
set "M_UNSUPPORTED_3=while AutoCAD %%CHOICE%% uses .NET Framework."
set "M_UNKNOWN_1=AutoCAD %%CHOICE%% has not been checked with this version of the installer yet."
set "M_UNKNOWN_2=Choose A - then the plugin will load in all versions starting from %MIN_YEAR%."
set "M_BAD_INPUT=Unexpected answer "%%CHOICE%%". Enter a year, for example 2026, or A, U, Q."
set "M_LOADS_ALL=The plugin loads in all AutoCAD versions starting from %MIN_YEAR%."
set "M_LOADS_IN=The plugin loads in AutoCAD:"
set "M_CLOSE_ACAD=Close AutoCAD and run the setup again: the plugin files are in use by AutoCAD."
set "M_PRESS_KEY=Press any key to exit..."
exit /b 0
