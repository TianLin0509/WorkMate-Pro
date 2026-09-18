param([string]$OutputDirectory, [string]$DataDirectory)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $project 'dist\WorkMate.exe'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $project ('artifacts\' + (Get-Date -Format yyyyMMdd-HHmmss) + '-workbench-e2e') }
if (-not $DataDirectory) { $DataDirectory = Join-Path ([IO.Path]::GetTempPath()) ('wm-wb-' + [guid]::NewGuid().ToString('N').Substring(0,8)) }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $DataDirectory -Force | Out-Null
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WorkbenchNative {
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd,IntPtr dc,uint flags);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
}
'@
$oldRoot = $env:WORKMATE_TEST_DIR
$oldArea = $env:WORKMATE_WORKBENCH_TEST_AREA
$proof = @()
function Find-Element($root, [string]$id) {
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $found = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($found) { return $found }
        Start-Sleep -Milliseconds 80
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Control missing: $id"
}
function Invoke-Control($root, [string]$id) {
    Write-Host "ACTION $id"
    $element = Find-Element $root $id
    $before = $element.GetRuntimeId() -join ','
    ([System.Windows.Automation.InvokePattern]$element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
    if ($id -ne 'memo-term-long') {
        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        do {
            $current = Find-Element $root $id
            if (($current.GetRuntimeId() -join ',') -ne $before) { return }
            Start-Sleep -Milliseconds 80
        } while ([DateTime]::UtcNow -lt $deadline)
        throw "UI rebuild did not finish after $id"
    }
}
function Read-Draft($root) {
    $element = Find-Element $root 'memo-draft'
    return ([System.Windows.Automation.ValuePattern]$element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).Current.Value
}
function Wait-Draft($root, [string]$expected) {
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        if ((Read-Draft $root) -eq $expected) { return }
        Start-Sleep -Milliseconds 80
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Draft value did not match: expected=[$expected] actual=[$(Read-Draft $root)]"
}
try {
    foreach ($area in @('980,700','640,460')) {
        $tag = $area.Replace(',','x')
        $rootPath = Join-Path $DataDirectory $tag
        New-Item -ItemType Directory -Path $rootPath -Force | Out-Null
        $fixture = @{ SchemaVersion=13; FirstRun=$false; LastGreetDate=(Get-Date -Format yyyy-MM-dd); LastPriorityPromptDate=(Get-Date -Format yyyy-MM-dd); TrackEnabled=$false }
        [IO.File]::WriteAllText((Join-Path $rootPath 'data.json'), ($fixture | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
        $env:WORKMATE_TEST_DIR = $rootPath
        $env:WORKMATE_WORKBENCH_TEST_AREA = $area
        $process = Start-Process -FilePath $exe -ArgumentList '--workbench' -PassThru -WindowStyle Hidden
        try {
            $deadline = [DateTime]::UtcNow.AddSeconds(20)
            $window = $null
            do {
                $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,
                    [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id))
                foreach ($candidate in $windows) {
                    $control = $candidate.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
                        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'memo-draft'))
                    if ($control) { $window = $candidate; break }
                }
                if (-not $window) { Start-Sleep -Milliseconds 100 }
            } while (-not $window -and [DateTime]::UtcNow -lt $deadline)
            if (-not $window) { throw 'Workbench did not become ready.' }
            $input = Find-Element $window 'memo-draft'
            $draft = '草稿跨筛选与页面保留 ' + $tag
            ([System.Windows.Automation.ValuePattern]$input.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue($draft)
            Write-Host "ACTION set-draft area=$area pid=$($process.Id)"
            Wait-Draft $window $draft
            $textPattern = [System.Windows.Automation.TextPattern]$input.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern)
            $textPattern.DocumentRange.FindText('跨筛选', $false, $false).Select()
            Invoke-Control $window 'memo-term-long'
            Invoke-Control $window 'memo-filter-done'
            Wait-Draft $window $draft
            Invoke-Control $window 'workbench-nav-today'
            Invoke-Control $window 'workbench-nav-memos'
            Wait-Draft $window $draft
            Invoke-Control $window 'memo-filter-active'
            Wait-Draft $window $draft
            $restoredInput = Find-Element $window 'memo-draft'
            $selection = ([System.Windows.Automation.TextPattern]$restoredInput.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern)).GetSelection()
            if ($selection.Count -ne 1 -or $selection[0].GetText(-1) -ne '跨筛选') { throw 'Draft text selection did not survive navigation.' }
            $handle = [IntPtr]$window.Current.NativeWindowHandle
            $rect = New-Object WorkbenchNative+RECT
            if (-not [WorkbenchNative]::GetWindowRect($handle, [ref]$rect)) { throw 'GetWindowRect failed.' }
            $dpi = [WorkbenchNative]::GetDpiForWindow($handle)
            $scale = $dpi / 96.0
            $size = $area.Split(',')
            if (($rect.Right-$rect.Left)/$scale -gt [double]$size[0]+2 -or ($rect.Bottom-$rect.Top)/$scale -gt [double]$size[1]+2) { throw 'Workbench exceeds simulated monitor work area.' }
            foreach ($id in @('memo-add','workbench-close','memo-filter-active','workbench-nav-settings')) {
                $element = Find-Element $window $id
                $bounds = $element.Current.BoundingRectangle
                if ($element.Current.IsOffscreen -or $bounds.Width -le 0 -or $bounds.Left -lt $rect.Left -or $bounds.Right -gt $rect.Right -or $bounds.Top -lt $rect.Top -or $bounds.Bottom -gt $rect.Bottom) { throw "Key control inaccessible: $id" }
            }
            $bitmap = [Drawing.Bitmap]::new($rect.Right-$rect.Left, $rect.Bottom-$rect.Top)
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            try {
                $dc = $graphics.GetHdc()
                try { if (-not [WorkbenchNative]::PrintWindow($handle,$dc,2)) { throw 'Screenshot failed.' } }
                finally { $graphics.ReleaseHdc($dc) }
                $screenshot = Join-Path $OutputDirectory ('workbench-' + $tag + '.png')
                $bitmap.Save($screenshot, [Drawing.Imaging.ImageFormat]::Png)
            } finally { $graphics.Dispose(); $bitmap.Dispose() }
            Invoke-Control $window 'memo-add'
            Wait-Draft $window ''
            $data = Get-Content -LiteralPath (Join-Path $rootPath 'data.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            $memo = @($data.Memos | Where-Object { $_.Text -eq $draft })
            if ($memo.Count -ne 1 -or $memo[0].Term -ne 'long') { throw 'Saved memo lost draft text or selected term.' }
            Invoke-Control $window 'workbench-nav-settings'
            $null = Find-Element $window 'weather-network-consent'
            $null = Find-Element $window 'outlook-consent'
            $version = Find-Element $window 'settings-version'
            $expectedVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion
            if (-not $version.Current.Name.Contains($expectedVersion)) { throw 'Settings does not display actual FileVersion.' }
            if ($data.WeatherNetworkAllowed -or $data.MeetingRadarEnabled) { throw 'UI fixture unexpectedly authorized external access.' }
            $proof += [pscustomobject]@{ areaDip=$area; dpi=$dpi; screenshot=$screenshot; dataRoot=$rootPath; draftFilter=$true; draftNavigation=$true; term=$memo[0].Term; reachable=$true; version=$expectedVersion }
            Write-Host "PASS workbench-real-ui area=$area dpi=$dpi draft/filter/navigation/save/controls/version"
        } finally {
            if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
        }
    }
    $proof | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'workbench-evidence.json') -Encoding UTF8
} finally {
    $env:WORKMATE_TEST_DIR = $oldRoot
    $env:WORKMATE_WORKBENCH_TEST_AREA = $oldArea
}
