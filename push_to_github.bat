@echo off
setlocal
echo ========================================
echo   Kindle to PDF - GitHub へプッシュ
echo ========================================
echo.
echo 初回のみ GitHub ログインが必要です:
echo   gh auth login
echo.
echo ログイン後、このバッチを再実行してください。
echo.

where gh >nul 2>&1
if errorlevel 1 (
  echo ERROR: GitHub CLI ^(gh^) が見つかりません。
  echo winget install GitHub.cli でインストールできます。
  pause
  exit /b 1
)

gh auth status >nul 2>&1
if errorlevel 1 (
  echo GitHub に未ログインです。次を実行してください:
  echo   gh auth login
  pause
  exit /b 1
)

cd /d "%~dp0"

echo リポジトリを GitHub に作成してプッシュします...
gh repo create kindle-to-pdf --public --source=. --remote=origin --push --description "Kindle viewer screen capture to PDF with optional OCR"

if errorlevel 1 (
  echo.
  echo リポジトリが既に存在する場合は次を実行:
  echo   git remote add origin https://github.com/YOUR_USERNAME/kindle-to-pdf.git
  echo   git push -u origin main
)

echo.
pause
endlocal
