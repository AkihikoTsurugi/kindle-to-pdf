@echo off
setlocal
set PY=%LOCALAPPDATA%\Programs\Python\Python312\python.exe
set OCR=%USERPROFILE%\Documents\Codex\ndlocr-lite\src\ocr.py

echo Python: "%PY%"
echo OCR: "%OCR%"
echo.

if not exist "%PY%" (
  echo ERROR: Python was not found at the expected path.
  pause
  exit /b 1
)

if not exist "%OCR%" (
  echo ERROR: NDLOCR-Lite ocr.py was not found.
  pause
  exit /b 1
)

"%PY%" "%OCR%" --help
echo.
pause
endlocal
