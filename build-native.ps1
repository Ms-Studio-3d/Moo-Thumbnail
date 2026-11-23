param([switch]$SkipTests, [switch]$LiveTests)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Install .NET Framework 4.8 from Microsoft first.' }
$out = Join-Path $root 'dist-native'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$sources = @('Core.cs','Program.cs','Theme.cs','MainForm.cs') | ForEach-Object { Join-Path $root "native\$_" }
$common = @('/nologo','/optimize+','/langversion:5','/codepage:65001','/platform:anycpu',
    '/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Net.Http.dll',
    "/win32icon:$root\assets\icon.ico", "/win32manifest:$root\native\app.manifest", "/resource:$root\assets\icon.png,MooThumbnail.Logo")
& $compiler @common '/target:winexe' "/out:$out\Moo-Thumbnail.exe" @sources
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed' }
& $compiler @common '/target:exe' '/main:MooThumbnail.Tests' "/out:$out\Moo-Thumbnail-Tests.exe" @sources "$root\native\Tests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Tests compilation failed' }
& $compiler @common '/target:winexe' '/main:MooThumbnail.LiveRunner' "/out:$out\Moo-Thumbnail-LiveTest.exe" @sources "$root\native\Tests.cs" "$root\native\LiveRunner.cs"
if ($LASTEXITCODE -ne 0) { throw 'Live test compilation failed' }
if (-not $SkipTests) {
    & "$out\Moo-Thumbnail-Tests.exe" '--core' "$root\.test-data\native"
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed' }
    & "$out\Moo-Thumbnail-Tests.exe" '--ui' "$root\.test-data\native"
    if ($LASTEXITCODE -ne 0) { throw 'UI tests failed' }
}
if ($LiveTests) {
    & "$out\Moo-Thumbnail-Tests.exe" '--live' "$root\.test-data\native"
    if ($LASTEXITCODE -ne 0) { throw 'Live network tests failed' }
}
Write-Host "Built: $out\Moo-Thumbnail.exe"
