param(
    [string]$ExePath,
    [string]$OutputDirectory,
    [string]$DataDirectory,
    [int]$SequentialSelfTests = 12,
    [int]$ParallelSelfTests = 6
)

$ErrorActionPreference = 'Stop'
$scriptRoot = $PSScriptRoot
$projectRoot = Split-Path -Parent $scriptRoot
if ([string]::IsNullOrWhiteSpace($ExePath)) { $ExePath = Join-Path $projectRoot 'dist\WorkMate.exe' }
if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) { throw "WorkMate executable not found: $ExePath" }
$ExePath = (Resolve-Path -LiteralPath $ExePath).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $projectRoot 'artifacts\v124-stress' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path

if ([string]::IsNullOrWhiteSpace($DataDirectory)) { $DataDirectory = Join-Path ([IO.Path]::GetTempPath()) ('wm-' + [guid]::NewGuid().ToString('N').Substring(0,8)) }
New-Item -ItemType Directory -Path $DataDirectory -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'data-root.txt'), $DataDirectory, [Text.UTF8Encoding]::new($false))
function New-IsolatedRoot([string]$Prefix) {
    $path = Join-Path $DataDirectory ($Prefix + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $path | Out-Null
    return $path
}

function Start-IsolatedAndWait([string]$Argument, [string]$Root) {
    $env:WORKMATE_TEST_DIR = $Root
    try { return Start-Process -FilePath $ExePath -ArgumentList $Argument -WindowStyle Hidden -PassThru -Wait }
    finally { Remove-Item Env:WORKMATE_TEST_DIR -ErrorAction SilentlyContinue }
}

$stressRoot = New-IsolatedRoot 'engine-stress'
$stress = Start-IsolatedAndWait '--stress-test' $stressRoot
if ($stress.ExitCode -ne 0) { throw "Stress test failed with exit $($stress.ExitCode)" }
$stressLog = Join-Path $stressRoot 'stress-test.log'
if (-not (Test-Path -LiteralPath $stressLog)) { throw 'Stress log missing.' }
Get-Content -LiteralPath $stressLog -Encoding UTF8
Write-Host 'PASS deterministic-engine-and-persistence-stress'

$sequentialPassed = 0
for ($i = 0; $i -lt $SequentialSelfTests; $i++) {
    $root = New-IsolatedRoot ('selftest-seq-' + $i.ToString('00'))
    $run = Start-IsolatedAndWait '--self-test' $root
    $log = Join-Path $root 'self-test.log'
    $selfTestFailed = $run.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $log) -or
        -not (Select-String -LiteralPath $log -Pattern '^RESULT failures=0$' -Quiet)
    if ($selfTestFailed) {
        throw "Sequential self-test $i failed."
    }
    $sequentialPassed++
}
Write-Host "PASS sequential-clean-selftests count=$sequentialPassed"

$parallel = @()
for ($i = 0; $i -lt $ParallelSelfTests; $i++) {
    $root = New-IsolatedRoot ('selftest-par-' + $i.ToString('00'))
    $env:WORKMATE_TEST_DIR = $root
    $process = Start-Process -FilePath $ExePath -ArgumentList '--self-test' -WindowStyle Hidden -PassThru
    $parallel += [pscustomobject]@{ Index = $i; Root = $root; Process = $process }
}
Remove-Item Env:WORKMATE_TEST_DIR -ErrorAction SilentlyContinue
foreach ($item in $parallel) {
    if (-not $item.Process.WaitForExit(120000)) {
        try { $item.Process.Kill() } catch { }
        throw "Parallel self-test $($item.Index) timed out."
    }
    $log = Join-Path $item.Root 'self-test.log'
    $parallelFailed = $item.Process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $log) -or
        -not (Select-String -LiteralPath $log -Pattern '^RESULT failures=0$' -Quiet)
    if ($parallelFailed) {
        throw "Parallel self-test $($item.Index) failed."
    }
}
Write-Host "PASS parallel-isolated-selftests count=$ParallelSelfTests"

$corruptRoot = New-IsolatedRoot 'corrupt-recovery'
[IO.File]::WriteAllText((Join-Path $corruptRoot 'data.json'), '{this is not json', [Text.UTF8Encoding]::new($false))
$corrupt = Start-IsolatedAndWait '--self-test' $corruptRoot
$corruptBackups = @(Get-ChildItem -LiteralPath $corruptRoot -Filter 'data.json.corrupt-*' -File)
if ($corrupt.ExitCode -ne 0 -or $corruptBackups.Count -ne 1) { throw 'Corrupt JSON recovery gate failed.' }
Write-Host 'PASS corrupt-json-is-backed-up-and-recovers'

$stormRoot = New-IsolatedRoot 'runtime-storm'
$env:WORKMATE_TEST_DIR = $stormRoot
$main = Start-Process -FilePath $ExePath -ArgumentList '--settings' -WindowStyle Hidden -PassThru
try {
    Start-Sleep -Seconds 3
    if ($main.HasExited) { throw 'Isolated runtime exited during startup.' }
    $commands = @('build-start','build-ok','dock-right','refresh-attention-demo','quiet-demo','focus-ritual-demo','pat-key-cancel-demo','open-capabilities')
    for ($i = 0; $i -lt 40; $i++) {
        $command = $commands[$i % $commands.Count]
        $sender = Start-Process -FilePath $ExePath -ArgumentList ('tell ' + $command) -PassThru -Wait -WindowStyle Hidden
        if ($sender.ExitCode -ne 0) { throw "Event storm sender failed: $command exit=$($sender.ExitCode)" }
    }
    Start-Sleep -Seconds 4
    if ($main.HasExited) { throw 'Isolated runtime crashed during event storm.' }
    $errorLog = Join-Path $stormRoot 'error.log'
    if ((Test-Path -LiteralPath $errorLog) -and (Get-Item -LiteralPath $errorLog).Length -gt 0) {
        throw "Event storm produced runtime errors: $errorLog"
    }
    Write-Host 'PASS rapid-event-preemption-storm sends=40 errorLog=empty'
}
finally {
    if ($null -ne $main -and -not $main.HasExited) {
        try {
            $sender = Start-Process -FilePath $ExePath -ArgumentList 'tell shutdown-demo' -PassThru -Wait -WindowStyle Hidden
            [void]$sender.ExitCode
            if (-not $main.WaitForExit(5000)) { $main.Kill() }
        }
        catch { if (-not $main.HasExited) { $main.Kill() } }
    }
    Remove-Item Env:WORKMATE_TEST_DIR -ErrorAction SilentlyContinue
}

Write-Host "RESULT stress-suite=PASS sequential=$SequentialSelfTests parallel=$ParallelSelfTests"
