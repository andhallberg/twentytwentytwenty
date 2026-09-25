# Builds TwentyTwentyTwenty, installs it for the current user and makes it start on Windows logon.
# Run again after pulling changes to update the installed binary.

$ErrorActionPreference = 'Stop'

$appName = 'TwentyTwentyTwenty'
$installDir = Join-Path $env:LOCALAPPDATA "Programs\$appName"
$exePath = Join-Path $installDir "$appName.exe"
$project = Join-Path $PSScriptRoot "$appName\$appName.csproj"
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$legacyShortcut = Join-Path ([Environment]::GetFolderPath('Startup')) "$appName.lnk"

# the running exe is locked, so stop it before overwriting
Get-Process -Name $appName -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

dotnet publish $project -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o $installDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

# the Run key is more reliable than the Startup folder, so use it and drop the old shortcut
Set-ItemProperty -Path $runKey -Name $appName -Value "`"$exePath`""
if (Test-Path $legacyShortcut) { Remove-Item $legacyShortcut }

Start-Process -FilePath $exePath -WorkingDirectory $installDir

Write-Output "Installed to $exePath and registered to start on logon."
