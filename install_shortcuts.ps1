param(
    [Parameter(Mandatory = $true)]
    [string]$AppDir,

    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [string]$AppName = "Kindle to PDF"
)

$ErrorActionPreference = "Stop"

$startFolder = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$AppName"
$desktop = [Environment]::GetFolderPath("Desktop")
$startLink = Join-Path $startFolder "$AppName.lnk"
$desktopLink = Join-Path $desktop "$AppName.lnk"

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "Executable not found: $ExePath"
}

New-Item -ItemType Directory -Path $startFolder -Force | Out-Null

$shell = New-Object -ComObject WScript.Shell

function New-AppShortcut {
    param([string]$LinkPath, [string]$Description)
    $shortcut = $shell.CreateShortcut($LinkPath)
    $shortcut.TargetPath = $ExePath
    $shortcut.WorkingDirectory = $AppDir
    $shortcut.Description = $Description
    $shortcut.Save()
}

New-AppShortcut -LinkPath $startLink -Description $AppName
New-AppShortcut -LinkPath $desktopLink -Description $AppName

Write-Host "Start Menu: $startLink"
Write-Host "Desktop:    $desktopLink"
