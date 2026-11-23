param([string]$NsisCompiler)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not (Test-Path -LiteralPath "$root\dist-native\Moo-Thumbnail.exe")) { throw 'Run build-native.ps1 first.' }
if (-not $NsisCompiler) {
    $candidates = @("${env:ProgramFiles(x86)}\NSIS\makensis.exe", "$env:ProgramFiles\NSIS\makensis.exe")
    $NsisCompiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $NsisCompiler) { $command = Get-Command makensis.exe -ErrorAction SilentlyContinue; if ($command) { $NsisCompiler = $command.Source } }
if (-not $NsisCompiler) { throw 'Install NSIS from https://nsis.sourceforge.io or pass -NsisCompiler with its full path.' }
& $NsisCompiler '/INPUTCHARSET' 'UTF8' "$root\native\installer.nsi"
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
