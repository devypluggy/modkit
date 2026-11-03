param(
    [string]$OutDir = (Join-Path $PSScriptRoot "..\out")
)

$ErrorActionPreference = "Stop"

$root  = Split-Path -Parent $PSScriptRoot          # ...\modkit
$csc   = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v3.5\csc.exe"
$fw2   = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v2.0.50727"
$res   = Join-Path $root "doorstop"
$built = Join-Path $root "out"
$ico   = Join-Path $root "assets\icon.ico"

foreach ($p in @($csc, (Join-Path $res "winhttp.dll"), (Join-Path $built "Doorstop.dll"))) {
    if (-not (Test-Path $p)) { throw "Not found: $p" }
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$exe = Join-Path $OutDir "ForestModLoader.exe"

& $csc /nologo /noconfig /target:winexe /codepage:65001 /platform:anycpu `
    /out:"$exe" `
    /win32icon:"$ico" `
    /win32manifest:"$PSScriptRoot\app.manifest" `
    /r:"$fw2\System.dll" /r:"$fw2\System.Drawing.dll" /r:"$fw2\System.Windows.Forms.dll" `
    /resource:"$res\winhttp.dll",payload.winhttp.dll `
    /resource:"$res\doorstop_config.ini",payload.doorstop_config.ini `
    /resource:"$built\Doorstop.dll",payload.Doorstop.dll `
    /resource:"$built\SampleCheats.dll",payload.SampleCheats.dll `
    "$PSScriptRoot\Version.cs" "$PSScriptRoot\Launcher.cs"

if ($LASTEXITCODE -ne 0) { throw "Build failed" }

Get-Item $exe | Select-Object FullName, Length
