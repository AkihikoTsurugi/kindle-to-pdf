@echo off
setlocal
set PY=%LOCALAPPDATA%\Programs\Python\Python312\python.exe
set "OCR=%~dp0..\ndlocr-lite\src\ocr.py"
set IMAGES=%~1
set OUT=%~2

if "%IMAGES%"=="" (
  echo ERROR: Missing image folder argument.
  exit /b 1
)

if "%OUT%"=="" (
  echo ERROR: Missing OCR output folder argument.
  exit /b 1
)

if not exist "%PY%" (
  echo ERROR: Python was not found at "%PY%".
  exit /b 1
)

if not exist "%OCR%" (
  echo ERROR: NDLOCR-Lite ocr.py was not found at "%OCR%".
  echo Clone ndlocr-lite next to this project: Documents\Codex\ndlocr-lite
  exit /b 1
)

"%PY%" "%OCR%" --sourcedir "%IMAGES%" --output "%OUT%"
exit /b %ERRORLEVEL%
