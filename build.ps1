param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\The Forest",
    [string]$ManagedDir = "",
    [switch]$Install,
    [switch]$X86
)

$ErrorActionPreference = "Stop"

$csc     = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v3.5\csc.exe"
$src     = Join-Path $PSScriptRoot "src"
$out     = Join-Path $PSScriptRoot "out"
$sys     = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v2.0.50727\System.dll"

if (-not (Test-Path $csc)) { throw "csc.exe (.NET 3.5) not found: $csc" }

# Ищем игровые сборки: явный -ManagedDir, затем установленная игра,
# затем локальная папка game\Managed (если репозиторий лежит внутри game).
if ([string]::IsNullOrWhiteSpace($ManagedDir)) {
    $candidates = @(
        (Join-Path $GameDir "TheForest_Data\Managed"),
        (Join-Path (Split-Path -Parent $PSScriptRoot) "Managed")
    )
    foreach ($c in $candidates) {
        if (Test-Path (Join-Path $c "UnityEngine.dll")) { $ManagedDir = $c; break }
    }
}

if (-not $ManagedDir -or -not (Test-Path (Join-Path $ManagedDir "UnityEngine.dll"))) {
    throw "Managed folder not found. Pass -ManagedDir '<game>\TheForest_Data\Managed'."
}

New-Item -ItemType Directory -Force -Path $out | Out-Null

Write-Host "Using Managed: $ManagedDir"

Write-Host "Building Doorstop.dll ..."
& $csc /nologo /noconfig /codepage:65001 /target:library /out:"$out\Doorstop.dll" `
    /r:"$sys" /r:"$ManagedDir\UnityEngine.dll" "$src\Doorstop.cs"
if ($LASTEXITCODE -ne 0) { throw "Doorstop.dll build failed" }

Write-Host "Building SampleCheats.dll ..."
& $csc /nologo /noconfig /codepage:65001 /target:library /out:"$out\SampleCheats.dll" `
    /r:"$sys" /r:"$ManagedDir\UnityEngine.dll" /r:"$ManagedDir\Assembly-CSharp.dll" /r:"$ManagedDir\TheForest.Modding.dll" `
    "$src\SampleCheats.cs"
if ($LASTEXITCODE -ne 0) { throw "SampleCheats.dll build failed" }

Write-Host "Built:"
Get-ChildItem $out -File | Select-Object Name, Length

if ($Install) {
    if (-not (Test-Path $GameDir)) { throw "Game folder not found: $GameDir" }

    $proxy = if ($X86) { Join-Path $PSScriptRoot "doorstop\winhttp_x86.dll" } else { Join-Path $PSScriptRoot "doorstop\winhttp.dll" }
    if (-not (Test-Path $proxy)) { throw "Proxy not found: $proxy" }

    Write-Host "Installing to $GameDir ..."
    Copy-Item $proxy                                       (Join-Path $GameDir "winhttp.dll")         -Force
    Copy-Item "$PSScriptRoot\doorstop\doorstop_config.ini" (Join-Path $GameDir "doorstop_config.ini") -Force
    Copy-Item "$out\Doorstop.dll"                          (Join-Path $GameDir "Doorstop.dll")        -Force
    New-Item -ItemType Directory -Force -Path (Join-Path $GameDir "DllMods") | Out-Null
    Copy-Item "$out\SampleCheats.dll"                      (Join-Path $GameDir "DllMods\SampleCheats.dll") -Force
    Write-Host "Installed."
}
