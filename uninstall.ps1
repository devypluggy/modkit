param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\The Forest"
)

$files = @("winhttp.dll", "doorstop_config.ini", "Doorstop.dll", "doorstop_loader.log")
foreach ($f in $files) {
    $p = Join-Path $GameDir $f
    if (Test-Path $p) { Remove-Item $p -Force; Write-Host "removed $f" }
}
$mods = Join-Path $GameDir "DllMods"
if (Test-Path $mods) { Remove-Item $mods -Recurse -Force; Write-Host "removed DllMods\" }
Write-Host "Done. Game files are untouched."
