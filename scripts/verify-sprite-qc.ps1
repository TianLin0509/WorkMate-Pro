param(
    [switch]$Strict
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$project = Split-Path -Parent $PSScriptRoot
$animationRoot = Join-Path $project 'assets\animations'
$reportPath = Join-Path $project 'artifacts\sprite-qc.log'
$issues = New-Object System.Collections.Generic.List[string]
$checkedFrames = 0

function Get-FrameMetric([string]$path) {
    $bitmap = [System.Drawing.Bitmap]::FromFile($path)
    try {
        $step = 6
        [double]$alpha = 0
        [double]$weightedX = 0
        [double]$weightedY = 0
        [double]$visible = 0
        [double]$red = 0
        [double]$green = 0
        [double]$blue = 0
        for ($y = 0; $y -lt $bitmap.Height; $y += $step) {
            for ($x = 0; $x -lt $bitmap.Width; $x += $step) {
                $pixel = $bitmap.GetPixel($x, $y)
                if ($pixel.A -lt 20) { continue }
                $weight = $pixel.A / 255.0
                $alpha += $weight
                $weightedX += $x * $weight
                $weightedY += $y * $weight
                $red += $pixel.R * $weight
                $green += $pixel.G * $weight
                $blue += $pixel.B * $weight
                $visible++
            }
        }
        if ($alpha -le 0) {
            return [pscustomobject]@{ Width=$bitmap.Width; Height=$bitmap.Height; Empty=$true }
        }
        return [pscustomobject]@{
            Width = $bitmap.Width
            Height = $bitmap.Height
            Empty = $false
            CenterX = $weightedX / $alpha / $bitmap.Width
            CenterY = $weightedY / $alpha / $bitmap.Height
            Coverage = $visible / ([math]::Ceiling($bitmap.Width / $step) * [math]::Ceiling($bitmap.Height / $step))
            MeanR = $red / $alpha
            MeanG = $green / $alpha
            MeanB = $blue / $alpha
        }
    }
    finally { $bitmap.Dispose() }
}

Get-ChildItem -LiteralPath $animationRoot -Directory | Sort-Object Name | ForEach-Object {
    $pet = $_.Name
    $framesRoot = Join-Path $_.FullName 'frames'
    Get-ChildItem -LiteralPath $framesRoot -Directory | Sort-Object Name | ForEach-Object {
        $sequence = $_.Name
        $frames = @(Get-ChildItem -LiteralPath $_.FullName -Filter '*.png' -File | Sort-Object Name)
        if ($frames.Count -ne 25) {
            $issues.Add("ERROR $pet/$sequence expected=25 actual=$($frames.Count)")
            return
        }
        $previous = $null
        $sequenceWidth = 0
        $sequenceHeight = 0
        foreach ($frame in $frames) {
            $metric = Get-FrameMetric $frame.FullName
            $checkedFrames++
            if ($metric.Empty) {
                $issues.Add("ERROR $pet/$sequence/$($frame.Name) empty-alpha")
                $previous = $null
                continue
            }
            if ($sequenceWidth -eq 0) {
                $sequenceWidth = $metric.Width
                $sequenceHeight = $metric.Height
            }
            elseif ($metric.Width -ne $sequenceWidth -or $metric.Height -ne $sequenceHeight) {
                $issues.Add("ERROR $pet/$sequence/$($frame.Name) inconsistent-size=$($metric.Width)x$($metric.Height) expected=${sequenceWidth}x${sequenceHeight}")
            }
            if ($null -ne $previous) {
                $centerDelta = [math]::Sqrt([math]::Pow($metric.CenterX - $previous.CenterX, 2) + [math]::Pow($metric.CenterY - $previous.CenterY, 2))
                $coverageDelta = [math]::Abs($metric.Coverage - $previous.Coverage) / [math]::Max(0.001, $previous.Coverage)
                $colorDelta = [math]::Sqrt([math]::Pow($metric.MeanR - $previous.MeanR, 2) + [math]::Pow($metric.MeanG - $previous.MeanG, 2) + [math]::Pow($metric.MeanB - $previous.MeanB, 2))
                if ($centerDelta -gt 0.30) { $issues.Add("WARN $pet/$sequence/$($frame.Name) center-delta=$([math]::Round($centerDelta,3))") }
                if ($coverageDelta -gt 1.25) { $issues.Add("WARN $pet/$sequence/$($frame.Name) coverage-delta=$([math]::Round($coverageDelta,3))") }
                if ($colorDelta -gt 100) { $issues.Add("WARN $pet/$sequence/$($frame.Name) color-delta=$([math]::Round($colorDelta,1))") }
            }
            $previous = $metric
        }
    }
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("Sprite QC time: $(Get-Date -Format o)")
$lines.Add("Checked frames: $checkedFrames")
$lines.Add("Issues: $($issues.Count)")
$issues | ForEach-Object { $lines.Add($_) }
[System.IO.File]::WriteAllLines($reportPath, $lines, [System.Text.UTF8Encoding]::new($false))
$lines | ForEach-Object { Write-Host $_ }
if ($Strict -and $issues.Count -gt 0) { throw "Sprite QC failed: $($issues.Count) issue(s). See $reportPath" }
