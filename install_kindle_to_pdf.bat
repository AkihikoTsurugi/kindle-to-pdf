@echo off
setlocal
set "APP_DIR=%~dp0"
set "APP_DIR=%APP_DIR:~0,-1%"
set "EXE=%APP_DIR%\KindleToPdf.exe"

echo ========================================
echo   Kindle to PDF - ショートカットのインストール
echo ========================================
echo.

call "%APP_DIR%\build_kindle_to_pdf.bat"
if errorlevel 1 (
  echo.
  echo ERROR: ビルドに失敗しました。
  pause
  exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%APP_DIR%\install_shortcuts.ps1" -AppDir "%APP_DIR%" -ExePath "%EXE%"
if errorlevel 1 (
  echo.
  echo ERROR: ショートカットの作成に失敗しました。
  pause
  exit /b 1
)

echo.
echo インストール完了。
echo スタートメニューで「Kindle to PDF」と検索して起動できます。
echo.
pause
endlocal
