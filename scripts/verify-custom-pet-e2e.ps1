[CmdletBinding()]
param(
    [string]$ExePath,
    [string]$OutputDirectory,
    [string]$DataDirectory
)

$ErrorActionPreference = 'Stop'
$scriptRoot = $PSScriptRoot
$projectRoot = Split-Path -Parent $scriptRoot
if ([string]::IsNullOrWhiteSpace($ExePath)) { $ExePath = Join-Path $projectRoot 'dist\WorkMate.exe' }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $projectRoot 'artifacts\custom-pet-ui-e2e' }
$ExePath = [System.IO.Path]::GetFullPath($ExePath)
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) { throw "WorkMate executable not found: $ExePath" }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
if ([string]::IsNullOrWhiteSpace($DataDirectory)) { $DataDirectory = Join-Path ([IO.Path]::GetTempPath()) ('wm-ui-' + [guid]::NewGuid().ToString('N').Substring(0,8)) }
$dataRoot = $DataDirectory
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'data-root.txt'), $dataRoot, [Text.UTF8Encoding]::new($false))
New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class WorkMateE2ENative {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr handle, IntPtr dc, uint flags);
}
'@

function Wait-CustomPetStep($Process, [int]$Step, [string]$Phase) {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    $processCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $Process.Id)
    $stepCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, "custom-pet-step-$Step")
    $nextCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'custom-pet-next')
    $diagnostic = [System.Collections.Generic.List[string]]::new()
    $diagnostic.Add("phase=$Phase pid=$($Process.Id) expectedStep=$Step")
    do {
        $Process.Refresh()
        if ($Process.HasExited) { $diagnostic.Add("process exited: $($Process.ExitCode)"); break }
        $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
            [System.Windows.Automation.TreeScope]::Children, $processCondition)
        foreach ($window in $windows) {
            try {
                $stepElement = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $stepCondition)
                $nextElement = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nextCondition)
                if ($null -ne $stepElement -and $null -ne $nextElement -and $nextElement.Current.IsEnabled -and
                    $nextElement.Current.BoundingRectangle.Width -gt 0) {
                    Write-Host "READY phase=$Phase pid=$($Process.Id) step=$Step handle=$($window.Current.NativeWindowHandle)"
                    return [pscustomobject]@{ Root=$window; Next=$nextElement; Handle=[IntPtr]$window.Current.NativeWindowHandle }
                }
                $line = "window=$($window.Current.NativeWindowHandle) title=$($window.Current.Name) expectedStep=$($null -ne $stepElement) next=$($null -ne $nextElement)"
                if (-not $diagnostic.Contains($line)) { $diagnostic.Add($line) }
            } catch {
                $line = "UIA window read: $($_.Exception.GetType().Name): $($_.Exception.Message)"
                if (-not $diagnostic.Contains($line)) { $diagnostic.Add($line) }
            }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    $path = Join-Path $OutputDirectory ("custom-pet-$Phase-readiness.txt")
    [IO.File]::WriteAllText($path, ($diagnostic -join [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
    throw "Custom-pet step $Step did not become ready during $Phase within 20 seconds. Diagnostics: $path"
}

function Write-AutomationTree($Root, [string]$Path, [int]$ProcessId, [int]$Step) {
    # A header makes the diagnostic useful even when UIA yields no named children;
    # never pass a pipeline-produced null array to WriteAllLines.
    $tree = [System.Collections.Generic.List[string]]::new()
    $tree.Add("pid=$ProcessId expectedStep=$Step handle=$($Root.Current.NativeWindowHandle)")
    $elements = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    $tree.Add("elementCount=$($elements.Count)")
    $namedCount = 0
    foreach ($element in $elements) {
        try {
            if (-not [string]::IsNullOrWhiteSpace($element.Current.Name) -or -not [string]::IsNullOrWhiteSpace($element.Current.AutomationId)) {
                $tree.Add($element.Current.AutomationId + '|' + $element.Current.Name)
                $namedCount++
            }
        } catch { $tree.Add("UIA unavailable during diagnostic: " + $_.Exception.Message) }
    }
    $tree.Add("namedElementCount=$namedCount empty=$($namedCount -eq 0)")
    [IO.File]::WriteAllText($Path, ($tree -join [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
    if (-not ($tree | Where-Object { $_ -like "custom-pet-step-$Step|*" }) -or
        -not ($tree | Where-Object { $_ -like 'custom-pet-next|*' })) {
        throw "Ready page lost its stable step/action IDs during capture. Diagnostic: $Path"
    }
}

$oldTestRoot = [Environment]::GetEnvironmentVariable('WORKMATE_TEST_DIR')
$oldE2E = [Environment]::GetEnvironmentVariable('WORKMATE_CUSTOM_PET_E2E')
$screens = @()
try {
    [Environment]::SetEnvironmentVariable('WORKMATE_TEST_DIR', $dataRoot)
    [Environment]::SetEnvironmentVariable('WORKMATE_CUSTOM_PET_E2E', '1')
    $fixtureProcess = Start-Process -FilePath $ExePath -ArgumentList '--custom-pet-e2e-prepare' -PassThru -Wait -WindowStyle Hidden
    if ($fixtureProcess.ExitCode -ne 0) { throw "Fixture preparation failed with exit code $($fixtureProcess.ExitCode)" }
    $projectPath = (Get-Content -LiteralPath (Join-Path $dataRoot 'custom-pet-e2e-project.txt') -Raw -Encoding UTF8).Trim()
    if (-not (Test-Path -LiteralPath $projectPath -PathType Container)) { throw "Fixture project not found: $projectPath" }

    foreach ($step in 1..4) {
        $testProcess = Start-Process -FilePath $ExePath -ArgumentList @(
            '--custom-pet', '--custom-pet-e2e-project', ('"' + $projectPath + '"'), '--custom-pet-e2e-step', $step
        ) -WindowStyle Hidden -PassThru
        try {
            $ready = Wait-CustomPetStep $testProcess $step "step$step-start"
            $root = $ready.Root
            $handle = $ready.Handle
            $nextElement = $ready.Next
            $treePath = Join-Path $OutputDirectory ("custom-pet-step{0}-automation.txt" -f $step)
            Write-AutomationTree $root $treePath $testProcess.Id $step

            $rect = New-Object WorkMateE2ENative+RECT
            if (-not [WorkMateE2ENative]::GetWindowRect($handle, [ref]$rect)) { throw "GetWindowRect failed at step $step." }
            $width = $rect.Right - $rect.Left
            $height = $rect.Bottom - $rect.Top
            if ($width -lt 900 -or $height -lt 620) { throw "Unexpected Workbench dimensions at step ${step}: ${width}x${height}" }
            $bitmap = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $dc = $graphics.GetHdc()
                try {
                    if (-not [WorkMateE2ENative]::PrintWindow($handle, $dc, 2)) { throw "PrintWindow failed at step $step." }
                } finally { $graphics.ReleaseHdc($dc) }
            } finally { $graphics.Dispose() }
            try {
                $colors = [System.Collections.Generic.HashSet[int]]::new()
                $strideX = [Math]::Max(1, [int]($width / 40))
                $strideY = [Math]::Max(1, [int]($height / 30))
                for ($x = 0; $x -lt $width; $x += $strideX) {
                    for ($y = 0; $y -lt $height; $y += $strideY) { $null = $colors.Add($bitmap.GetPixel($x, $y).ToArgb()) }
                }
                if ($colors.Count -lt 12) { throw "Step $step screenshot appears blank (colors=$($colors.Count))." }
                $imagePath = Join-Path $OutputDirectory ("custom-pet-step{0}.png" -f $step)
                $bitmap.Save($imagePath, [System.Drawing.Imaging.ImageFormat]::Png)
                $screens += [pscustomobject]@{ step = $step; path = $imagePath; width = $width; height = $height; colors = $colors.Count }
            } finally { $bitmap.Dispose() }

            $invoke = [System.Windows.Automation.InvokePattern]$nextElement.GetCurrentPattern(
                [System.Windows.Automation.InvokePattern]::Pattern)
            $invoke.Invoke()
            $transitionDeadline = [DateTime]::UtcNow.AddSeconds(20)
            if ($step -lt 4) {
                $null = Wait-CustomPetStep $testProcess ($step + 1) "step$step-next"
            }
            else {
                $manifestPath = Join-Path $projectPath 'manifest.json'
                $dataPath = Join-Path $dataRoot 'data.json'
                do {
                    Start-Sleep -Milliseconds 250
                    $manifestReady = $false
                    $petSelected = $false
                    try { $manifestReady = ((Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json).Status -eq 'ready') } catch { }
                    try { $petSelected = ((Get-Content -LiteralPath $dataPath -Raw -Encoding UTF8 | ConvertFrom-Json).PetId -eq (Split-Path -Leaf $projectPath)) } catch { }
                } while ((-not $manifestReady -or -not $petSelected) -and [DateTime]::UtcNow -lt $transitionDeadline)
                if (-not $manifestReady -or -not $petSelected) { throw 'Step 4 did not atomically import and select the custom pet.' }
            }
        }
        finally {
            if (-not $testProcess.HasExited) {
                $null = $testProcess.CloseMainWindow()
                if (-not $testProcess.WaitForExit(1200)) { $testProcess.Kill(); $testProcess.WaitForExit() }
            }
        }
    }
    $brokenPose = Join-Path $projectPath 'generated\sleep.png'
    [System.IO.File]::WriteAllText($brokenPose, 'not-a-png', [System.Text.UTF8Encoding]::new($false))
    $failureProcess = Start-Process -FilePath $ExePath -ArgumentList @(
        '--custom-pet', '--custom-pet-e2e-project', ('"' + $projectPath + '"'), '--custom-pet-e2e-step', 3
    ) -WindowStyle Hidden -PassThru
    try {
        $failureReady = Wait-CustomPetStep $failureProcess 3 'recovery-start'
        $failureRoot = $failureReady.Root
        $nextElement = $failureReady.Next
        $failureDeadline = [DateTime]::UtcNow.AddSeconds(20)
        $invoke = [System.Windows.Automation.InvokePattern]$nextElement.GetCurrentPattern(
            [System.Windows.Automation.InvokePattern]::Pattern)
        $invoke.Invoke()
        $errorCondition = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
            'custom-pet-validation-error')
        do {
            Start-Sleep -Milliseconds 250
            $recoveryElement = $failureRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $errorCondition)
        } while ($null -eq $recoveryElement -and [DateTime]::UtcNow -lt $failureDeadline)
        if ($null -eq $recoveryElement) { throw 'Invalid pose did not render inline recovery guidance.' }
    }
    finally {
        if (-not $failureProcess.HasExited) {
            $null = $failureProcess.CloseMainWindow()
            if (-not $failureProcess.WaitForExit(1200)) { $failureProcess.Kill(); $failureProcess.WaitForExit() }
        }
    }
    $errorLog = Join-Path $dataRoot 'error.log'
    if (Test-Path -LiteralPath $errorLog) { throw "UI E2E produced error.log: $errorLog" }
    $summary = [pscustomobject]@{
        result = 'PASS'
        executable = $ExePath
        fixtureProject = $projectPath
        navigationAndImport = $true
        inlineRecovery = $true
        screenshots = $screens
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $OutputDirectory 'summary.json'),
        ($summary | ConvertTo-Json -Depth 5),
        [System.Text.UTF8Encoding]::new($false))
    $summary | ConvertTo-Json -Depth 5
}
finally {
    [Environment]::SetEnvironmentVariable('WORKMATE_TEST_DIR', $oldTestRoot)
    [Environment]::SetEnvironmentVariable('WORKMATE_CUSTOM_PET_E2E', $oldE2E)
}
