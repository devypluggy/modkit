param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\The Forest"
)

$ErrorActionPreference = "Stop"

# Проверяем запущенную игру: иначе Remove-Item на занятом winhttp.dll
# выдаст невнятную ошибку или оставит лоадер в полуудалённом состоянии.
$running = Get-Process -Name TheForest, TheForest32, TheForestVR -ErrorAction SilentlyContinue
if ($running) {
    throw "The Forest запущена. Закрой игру и повтори."
}

$files = @("winhttp.dll", "doorstop_config.ini", "Doorstop.dll", "doorstop_loader.log", "doorstop_loader.log.old")
foreach ($f in $files) {
    $p = Join-Path $GameDir $f
    if (Test-Path $p) { Remove-Item $p -Force; Write-Host "removed $f" }
}

# Чужие моды не трогаем — удаляем только файл из комплекта.
$mods = Join-Path $GameDir "DllMods"
foreach ($f in @("SampleCheats.dll", "SampleCheats.dll.bak", "SampleCheats.dll.tmp")) {
    $p = Join-Path $mods $f
    if (Test-Path $p) { Remove-Item $p -Force; Write-Host "removed DllMods\$f" }
}

if ((Test-Path $mods) -and ((Get-ChildItem $mods -Force | Measure-Object).Count -eq 0)) {
    Remove-Item $mods -Force
    Write-Host "removed empty DllMods\"
}

Write-Host "Done. Game files and user mods are untouched."
