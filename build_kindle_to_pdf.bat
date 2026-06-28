@echo off
setlocal
set SCRIPT_DIR=%~dp0
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
  echo ERROR: csc.exe was not found at "%CSC%"
  exit /b 1
)

"%CSC%" /nologo /target:winexe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /out:"%SCRIPT_DIR%KindleToPdf.exe" "%SCRIPT_DIR%ScreenScanToPdf.cs"
if errorlevel 1 exit /b 1

echo Built: "%SCRIPT_DIR%KindleToPdf.exe"
endlocal
