param(
    [string]$PackagePath = (Join-Path $PSScriptRoot 'PlcSourceExporter.V17.addin'),
    [string]$InstallPath = 'C:\Program Files\Siemens\Automation\Portal V17\AddIns\PlcSourceExporter\PlcSourceExporter.V17.addin',
    [string]$HelperSourcePath = (Join-Path $PSScriptRoot '..\src\PlcSourceExporter.ExportAnalyzer\bin\Debug\net48'),
    [string]$HelperInstallPath = 'C:\Program Files\Siemens\Automation\Portal V17\AddIns\PlcSourceExporter\ExportAnalyzer'
)

$ErrorActionPreference = 'Stop'

$statusPath = Join-Path $PSScriptRoot 'PlcSourceExporter.V17.install-status.txt'

try {
    $installDirectory = Split-Path -Parent $InstallPath
    New-Item -ItemType Directory -Force $installDirectory | Out-Null
    Copy-Item -LiteralPath $PackagePath -Destination $InstallPath -Force
    New-Item -ItemType Directory -Force $HelperInstallPath | Out-Null
    Copy-Item -Path (Join-Path $HelperSourcePath '*') -Destination $HelperInstallPath -Recurse -Force
    $hash = Get-FileHash -LiteralPath $InstallPath -Algorithm SHA256
    "OK $($hash.Hash)" | Set-Content -LiteralPath $statusPath
    $hash
}
catch {
    "ERROR $($_.Exception.Message)" | Set-Content -LiteralPath $statusPath
    throw
}
