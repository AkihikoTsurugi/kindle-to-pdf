@echo off
setlocal
set "SCRIPT_DIR=%~dp0"

if not exist "%SCRIPT_DIR%KindleToPdf.exe" (
  call "%SCRIPT_DIR%build_kindle_to_pdf.bat"
  if errorlevel 1 (
    echo ERROR: Failed to build KindleToPdf.exe
    pause
    exit /b 1
  )
)

start "" "%SCRIPT_DIR%KindleToPdf.exe"
endlocal
