param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\The Forest",
    [switch]$Install
)

$ErrorActionPreference = "Stop"

$csc     = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v3.5\csc.exe"
$src     = Join-Path $PSScriptRoot "src"
$out     = Join-Path $PSScriptRoot "out"
$managed = Join-Path (Split-Path -Parent $PSScriptRoot) "Managed"
$sys     = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v2.0.50727\System.dll"

if (-not (Test-Path $csc))     { throw "csc.exe (.NET 3.5) not found: $csc" }
if (-not (Test-Path $managed)) { throw "Managed folder not found: $managed" }

New-Item -ItemType Directory -Force -Path $out | Out-Null

Write-Host "Building Doorstop.dll ..."
& $csc /nologo /noconfig /target:library /out:"$out\Doorstop.dll" `
    /r:"$sys" /r:"$managed\UnityEngine.dll" "$src\Doorstop.cs"
if ($LASTEXITCODE -ne 0) { throw "Doorstop.dll build failed" }

Write-Host "Building SampleCheats.dll ..."
& $csc /nologo /noconfig /target:library /out:"$out\SampleCheats.dll" `
    /r:"$sys" /r:"$managed\UnityEngine.dll" /r:"$managed\Assembly-CSharp.dll" /r:"$managed\TheForest.Modding.dll" `
    "$src\SampleCheats.cs"
if ($LASTEXITCODE -ne 0) { throw "SampleCheats.dll build failed" }

Write-Host "Built:"
Get-ChildItem $out | Select-Object Name, Length

if ($Install) {
    if (-not (Test-Path $GameDir)) { throw "Game folder not found: $GameDir" }
    Write-Host "Installing to $GameDir ..."
    Copy-Item "$PSScriptRoot\doorstop\winhttp.dll"         (Join-Path $GameDir "winhttp.dll")         -Force
    Copy-Item "$PSScriptRoot\doorstop\doorstop_config.ini" (Join-Path $GameDir "doorstop_config.ini") -Force
    Copy-Item "$out\Doorstop.dll"                          (Join-Path $GameDir "Doorstop.dll")        -Force
    New-Item -ItemType Directory -Force -Path (Join-Path $GameDir "DllMods") | Out-Null
    Copy-Item "$out\SampleCheats.dll"                      (Join-Path $GameDir "DllMods\SampleCheats.dll") -Force
    Write-Host "Installed."
}
