[CmdletBinding()]
param(
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$scriptRoot = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptRoot)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$project = Split-Path -Parent $scriptRoot
$dist = Join-Path $project 'dist'
$artifactDir = Join-Path $project 'artifacts'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

function Quote-CscPath([string]$value) {
    return '"' + $value.Replace('"', '""') + '"'
}

if (-not (Test-Path -LiteralPath $csc)) { throw "C# compiler not found: $csc" }
if ($Clean -and (Test-Path -LiteralPath $dist)) {
    Get-ChildItem -LiteralPath $dist -File | Remove-Item -Force
}
New-Item -ItemType Directory -Path $dist -Force | Out-Null
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

$assemblySource = Get-Content -LiteralPath (Join-Path $project 'AssemblyInfo.cs') -Raw -Encoding UTF8
$fileVersion = [regex]::Match($assemblySource, 'AssemblyFileVersion\("([0-9.]+)"\)').Groups[1].Value
$assemblyVersion = [regex]::Match($assemblySource, 'AssemblyVersion\("([0-9.]+)"\)').Groups[1].Value
[xml]$manifestSource = Get-Content -LiteralPath (Join-Path $project 'app.manifest') -Raw -Encoding UTF8
$manifestVersion = $manifestSource.assembly.assemblyIdentity.version
$readmeSource = Get-Content -LiteralPath (Join-Path $project 'README.md') -Raw -Encoding UTF8
$readmeVersion = [regex]::Match($readmeSource, '\*\*v([0-9.]+)\*\*').Groups[1].Value
if (-not $fileVersion -or $assemblyVersion -ne $fileVersion -or $manifestVersion -ne $fileVersion -or
    $readmeVersion -ne ([version]$fileVersion).ToString(3)) { throw "Version sources disagree: file=$fileVersion assembly=$assemblyVersion manifest=$manifestVersion readme=$readmeVersion" }
Write-Host "PASS version-consistency version=$fileVersion"

# 300 帧进入编译前先做低成本视觉连续性门禁：尺寸、透明主体、相邻帧中心/面积/主色突变。
Write-Host 'Sprite QC begin'
& (Join-Path $scriptRoot 'verify-sprite-qc.ps1') -Strict
Write-Host 'Sprite QC end'

$references = @(
    'C:\Windows\Microsoft.NET\assembly\GAC_32\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll',
    'C:\Windows\Microsoft.NET\assembly\GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll',
    'C:\Windows\Microsoft.NET\assembly\GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll',
    'C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll'
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Microsoft.CSharp.dll'
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll'
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.dll'
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.FileSystem.dll'
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.Extensions.dll'
)
foreach ($reference in $references) {
    if (-not (Test-Path -LiteralPath $reference)) { throw "Reference assembly not found: $reference" }
}

$arguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/codepage:65001',
    '/main:WorkMatePro.Program',
    ('/out:' + (Quote-CscPath (Join-Path $dist 'WorkMate.exe'))),
    ('/win32icon:' + (Quote-CscPath (Join-Path $project 'assets\WorkMatePro.ico'))),
    ('/win32manifest:' + (Quote-CscPath (Join-Path $project 'app.manifest')))
)
foreach ($reference in $references) { $arguments += ('/reference:' + (Quote-CscPath $reference)) }

$petIds = @('01-cat', '03-penguin', '05-rabbit', '07-shiba', '09-hamster', '11-cockatiel')
$actions = @('idle', 'typing', 'happy', 'sleep')
foreach ($petId in $petIds) {
    foreach ($action in $actions) {
        $asset = Join-Path $project "assets\sprites\$petId\$action.png"
        if (-not (Test-Path -LiteralPath $asset)) { throw "Sprite not found: $asset" }
        $arguments += ('/resource:' + (Quote-CscPath $asset) + ",WorkMate.Assets.$petId.$action.png")
    }
}
$animationNames = @('dock-right', 'typing', 'carry-file', 'meeting', 'stretch', 'idle-life')
$animatedPetIds = @('01-cat', '03-penguin')
foreach ($animatedPetId in $animatedPetIds) {
    foreach ($animationName in $animationNames) {
        for ($frame = 0; $frame -lt 25; $frame++) {
            $frameName = $frame.ToString('00')
            $asset = Join-Path $project "assets\animations\$animatedPetId\frames\$animationName\$frameName.png"
            if (-not (Test-Path -LiteralPath $asset)) { throw "Animation frame not found: $asset" }
            $arguments += ('/resource:' + (Quote-CscPath $asset) + ",WorkMate.Frames.$animatedPetId.$animationName.$frameName.png")
        }
    }
}

$ocrScriptTool = Join-Path $project 'tools\workmate-ocr.ps1'
if (-not (Test-Path -LiteralPath $ocrScriptTool)) { throw "Tool not found: $ocrScriptTool" }
$arguments += ('/resource:' + (Quote-CscPath $ocrScriptTool) + ',WorkMate.Tools.workmate-ocr.ps1')

# 内嵌能力工具随源码一起变化。把期望哈希生成到同一次构建里，
# 运行时解压才校验的正是本次实际嵌入的资源。
$ocrScriptHash = (Get-FileHash -LiteralPath $ocrScriptTool -Algorithm SHA256).Hash
$generatedHashSource = Join-Path $artifactDir 'EmbeddedToolHashes.g.cs'
$generatedHashCode = @"
namespace WorkMatePro
{
    public sealed partial class EmbeddedToolManager
    {
        public const string OcrScriptSha256 = "$ocrScriptHash";
    }
}
"@
[System.IO.File]::WriteAllText($generatedHashSource, $generatedHashCode, [System.Text.UTF8Encoding]::new($false))

$sources = @(Get-ChildItem -LiteralPath $project -Filter '*.cs' -File | Sort-Object Name)
$sources += Get-Item -LiteralPath $generatedHashSource
foreach ($source in $sources) { $arguments += (Quote-CscPath $source.FullName) }

# 300 帧资源会超过 Windows CreateProcess 命令行长度；csc 原生 response file
# 让每个参数独占一行，仍然生成单 EXE，不引入运行时文件。
Write-Host "Compiler arguments: type=$($arguments.GetType().FullName) count=$($arguments.Count) first=[$($arguments[0])] last=[$($arguments[$arguments.Count - 1])]"
$responseFile = Join-Path $artifactDir 'csc-build.rsp'
$arguments | Out-File -LiteralPath $responseFile -Encoding utf8
$output = & $csc ('@' + $responseFile) 2>&1
$exitCode = $LASTEXITCODE
$logLines = @(
    "Build time: $(Get-Date -Format o)",
    "Compiler: $csc",
    "Sources: $($sources.Count)",
    "Embedded sprites: $($petIds.Count * $actions.Count)",
    "Embedded animation frames: $($animatedPetIds.Count * $animationNames.Count * 25)",
    'Embedded capability tools: 1',
    $output
)
[System.IO.File]::WriteAllLines((Join-Path $artifactDir 'build.log'), [string[]]$logLines, [System.Text.UTF8Encoding]::new($false))
if ($exitCode -ne 0) {
    $output | ForEach-Object { Write-Host $_ }
    throw "Build failed with exit code $exitCode"
}

$exe = Get-Item -LiteralPath (Join-Path $dist 'WorkMate.exe')
$hash = Get-FileHash -LiteralPath $exe.FullName -Algorithm SHA256
Write-Host "Built: $($exe.FullName)"
Write-Host "Bytes: $($exe.Length)"
Write-Host "SHA256: $($hash.Hash)"
