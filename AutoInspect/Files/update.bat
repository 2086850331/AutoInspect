@echo off
setlocal enabledelayedexpansion

::------------------------------------
:: fixed variable
::------------------------------------
for %%I in ("%~dp0\.") do set TARGET_DIR=%%~dpI
set TARGET_EXE=%TARGET_DIR%AutoInspect.exe
set FILES_DIR=%TARGET_DIR%Files
set UPDATE_DIR=%FILES_DIR%\Update
set LOG_FILE=%FILES_DIR%\update.log

echo [%date% %time%] Start Update >> "%LOG_FILE%"

::------------------------------------
:: UA role
::------------------------------------
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo request ua role...
    powershell -Command "Start-Process '%~f0' -Verb runAs"
    exit /b
)

::------------------------------------
:: wait main exit
::------------------------------------
echo [%date% %time%] wait main exit... >> "%LOG_FILE%"
:waitloop
tasklist | find /i "AutoInspect.exe" >nul
if %errorLevel%==0 (
    timeout /t 2 >nul
    goto waitloop
)
echo [%date% %time%] main exited. >> "%LOG_FILE%"

::------------------------------------
:: replace file
::------------------------------------
if not exist "%UPDATE_DIR%" (
    echo [%date% %time%] error: Not exists the update path!>> "%LOG_FILE%"
    exit /b
)

echo [%date% %time%] start replace file... >> "%LOG_FILE%"
pushd "%UPDATE_DIR%"
for %%F in (*) do (
    set "SRC=%%F"
    set "DST=%TARGET_DIR%%%F"
    echo replace: !SRC! → !DST! >> "%LOG_FILE%"
    if exist "!DST!" attrib -r "!DST!" >nul 2>&1
    copy /y "!SRC!" "!DST!" >nul
)
popd
echo [%date% %time%] replace comeplated. >> "%LOG_FILE%"

::------------------------------------
:: run new exe
::------------------------------------
echo [%date% %time%] run new exe version... >> "%LOG_FILE%"
start "" /D "%TARGET_DIR%" "%TARGET_EXE%"

::------------------------------------
:: Clear origin path
::------------------------------------
echo [%date% %time%] Clear origin path... >> "%LOG_FILE%"
rmdir /s /q "%UPDATE_DIR%" 2>>"%LOG_FILE%"

echo [%date% %time%] update comeplated. >> "%LOG_FILE%"
exit /b
