@echo off
setlocal
cd /d "%~dp0"

where gh >nul 2>&1
if errorlevel 1 (
  echo ERROR: gh not found. Install with: winget install GitHub.cli
  pause
  exit /b 1
)

gh auth status >nul 2>&1
if errorlevel 1 (
  echo ERROR: Not logged in. Run: gh auth login
  pause
  exit /b 1
)

echo Creating GitHub repo and pushing...
gh repo create kindle-to-pdf --public --source=. --remote=origin --push --description "Kindle viewer screen capture to PDF with optional OCR"
if errorlevel 1 (
  echo.
  echo If the repo already exists, run:
  echo   git remote add origin https://github.com/AkihikoTsurugi/kindle-to-pdf.git
  echo   git push -u origin main
  pause
  exit /b 1
)

echo.
echo Done.
pause
endlocal
