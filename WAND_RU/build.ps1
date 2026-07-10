<#
.SYNOPSIS
  Сборка Wand-RU-Installer.
.EXAMPLE
  ./build.ps1                 # Release build
  ./build.ps1 -Test           # + прогон тестов
  ./build.ps1 -Publish        # + self-contained single-file .exe в ./publish
#>
param(
    [switch]$Test,
    [switch]$Publish,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root  = $PSScriptRoot
$proj  = Join-Path $root "Installer\WandRuInstaller.csproj"
$tests = Join-Path $root "Installer.Tests\WandRuInstaller.Tests.csproj"

Write-Host "==> Build ($Configuration)" -ForegroundColor Cyan
dotnet build $proj -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "build failed" }

if ($Test) {
    Write-Host "==> Test" -ForegroundColor Cyan
    dotnet test $tests -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw "tests failed" }
}

if ($Publish) {
    Write-Host "==> Publish (self-contained single-file win-x64)" -ForegroundColor Cyan
    $out = Join-Path $root "publish"
    dotnet publish $proj -c $Configuration -r win-x64 --self-contained `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $out --nologo
    if ($LASTEXITCODE -ne 0) { throw "publish failed" }
    Write-Host "OK: $out\WandRuInstaller.exe" -ForegroundColor Green
}

Write-Host "Done." -ForegroundColor Green
