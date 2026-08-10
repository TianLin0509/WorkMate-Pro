[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildEnvironment = Join-Path $projectRoot '.venv-build'
$buildPython = Join-Path $buildEnvironment 'Scripts\python.exe'
$entryPoint = Join-Path $projectRoot 'src\main.py'
$sourcePath = Join-Path $projectRoot 'src'
$outputPath = Join-Path $projectRoot 'output'
$workPath = Join-Path $projectRoot 'build\pyinstaller-work'
$specPath = Join-Path $projectRoot 'build\pyinstaller-spec'

if (-not (Test-Path -LiteralPath $buildPython)) {
    if (Get-Command py -ErrorAction SilentlyContinue) {
        & py -3.12 -m venv $buildEnvironment
    }
    elseif (Get-Command python -ErrorAction SilentlyContinue) {
        & python -m venv $buildEnvironment
    }
    else {
        throw 'Python 3.12 was not found. Install Python or make python.exe available on PATH.'
    }
    if ($LASTEXITCODE -ne 0) { throw "Creating the Python build environment failed with exit code $LASTEXITCODE" }
}

& $buildPython -m pip install --disable-pip-version-check -r (Join-Path $projectRoot 'requirements-build.txt')
if ($LASTEXITCODE -ne 0) { throw "Installing AutoPageCapture build dependencies failed with exit code $LASTEXITCODE" }

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
New-Item -ItemType Directory -Path $workPath -Force | Out-Null
New-Item -ItemType Directory -Path $specPath -Force | Out-Null

$executable = Join-Path $outputPath 'AutoPageCapture.exe'
if (Test-Path -LiteralPath $executable) {
    Remove-Item -LiteralPath $executable -Force
}

& $buildPython -m PyInstaller `
    --noconfirm `
    --clean `
    --onefile `
    --windowed `
    --noupx `
    --name 'AutoPageCapture' `
    --paths $sourcePath `
    --distpath $outputPath `
    --workpath $workPath `
    --specpath $specPath `
    $entryPoint
if ($LASTEXITCODE -ne 0) { throw "PyInstaller failed with exit code $LASTEXITCODE" }

if (-not (Test-Path -LiteralPath $executable)) {
    throw "Build completed without the expected executable: $executable"
}

$item = Get-Item -LiteralPath $executable
Write-Host "Built: $($item.FullName)"
Write-Host "Size:  $([Math]::Round($item.Length / 1MB, 2)) MB"
