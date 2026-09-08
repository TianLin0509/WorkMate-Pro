[CmdletBinding()]
param(
    [string]$ExePath,
    [string]$OutputDirectory
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
$dataRoot = Join-Path $OutputDirectory 'data'
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
            try { $null = $testProcess.WaitForInputIdle(15000) } catch { }
            $deadline = [DateTime]::UtcNow.AddSeconds(20)
            do {
                Start-Sleep -Milliseconds 200
                $testProcess.Refresh()
                $handle = $testProcess.MainWindowHandle
            } while ($handle -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline)
            if ($handle -eq [IntPtr]::Zero) { throw "Step $step did not expose a main window." }
            Start-Sleep -Milliseconds 900

            $root = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
            $elements = $root.FindAll(
                [System.Windows.Automation.TreeScope]::Descendants,
                [System.Windows.Automation.Condition]::TrueCondition)
            $tree = foreach ($element in $elements) {
                try {
                    if (-not [string]::IsNullOrWhiteSpace($element.Current.Name) -or -not [string]::IsNullOrWhiteSpace($element.Current.AutomationId)) {
                        $element.Current.AutomationId + '|' + $element.Current.Name
                    }
                } catch { }
            }
            $treePath = Join-Path $OutputDirectory ("custom-pet-step{0}-automation.txt" -f $step)
            [System.IO.File]::WriteAllLines($treePath, [string[]]$tree, [System.Text.UTF8Encoding]::new($false))
            $hasStepId = [bool]($tree | Where-Object { $_ -like ("custom-pet-step-$step|*") })
            $hasNextId = [bool]($tree | Where-Object { $_ -like 'custom-pet-next|*' })
            if (-not $hasStepId -or -not $hasNextId) {
                throw "Step $step automation tree is missing stable custom-pet ids."
            }
            $nextCondition = [System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
                'custom-pet-next')
            $nextElement = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nextCondition)
            if ($null -eq $nextElement -or -not $nextElement.Current.IsEnabled) { throw "Step $step next action is unavailable." }

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
                $expectedCondition = [System.Windows.Automation.PropertyCondition]::new(
                    [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
                    ("custom-pet-step-" + ($step + 1)))
                do {
                    Start-Sleep -Milliseconds 200
                    $expected = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $expectedCondition)
                } while ($null -eq $expected -and [DateTime]::UtcNow -lt $transitionDeadline)
                if ($null -eq $expected) { throw "Step $step did not advance through the real next action." }
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
        try { $null = $failureProcess.WaitForInputIdle(15000) } catch { }
        $failureDeadline = [DateTime]::UtcNow.AddSeconds(20)
        do {
            Start-Sleep -Milliseconds 200
            $failureProcess.Refresh()
            $failureHandle = $failureProcess.MainWindowHandle
        } while ($failureHandle -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $failureDeadline)
        if ($failureHandle -eq [IntPtr]::Zero) { throw 'Recovery E2E did not expose a main window.' }
        $failureRoot = [System.Windows.Automation.AutomationElement]::FromHandle($failureHandle)
        $nextCondition = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
            'custom-pet-next')
        # This process owns both the pet and workbench. MainWindowHandle can
        # initially select the pet; bind to the actual action in this process.
        $processCondition = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $failureProcess.Id)
        do {
            $nextElement = $null
            $processWindows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
                [System.Windows.Automation.TreeScope]::Children, $processCondition)
            foreach ($candidateWindow in $processWindows) {
                $candidateNext = $candidateWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nextCondition)
                if ($null -ne $candidateNext -and $candidateNext.Current.IsEnabled) {
                    $failureRoot = $candidateWindow
                    $nextElement = $candidateNext
                    break
                }
            }
            if ($null -ne $nextElement -and $nextElement.Current.IsEnabled) { break }
            if ($failureProcess.HasExited) { throw 'Recovery E2E exited before its next action became ready.' }
            Start-Sleep -Milliseconds 200
        } while ([DateTime]::UtcNow -lt $failureDeadline)
        if ($null -eq $nextElement -or -not $nextElement.Current.IsEnabled) { throw 'Recovery E2E next action is unavailable.' }
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
