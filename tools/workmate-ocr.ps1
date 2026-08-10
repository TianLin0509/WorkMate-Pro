param(
    [Parameter(Mandatory = $true)]
    [string]$Image,
    [Parameter(Mandatory = $true)]
    [string]$Output,
    [string]$Language = 'zh-Hans-CN'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Image -PathType Leaf)) {
    throw "Image not found: $Image"
}

Add-Type -AssemblyName System.Runtime.WindowsRuntime
[void][Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
[void][Windows.Storage.FileAccessMode, Windows.Storage, ContentType = WindowsRuntime]
[void][Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics.Imaging, ContentType = WindowsRuntime]
[void][Windows.Graphics.Imaging.SoftwareBitmap, Windows.Graphics.Imaging, ContentType = WindowsRuntime]
[void][Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime]
[void][Windows.Media.Ocr.OcrResult, Windows.Foundation, ContentType = WindowsRuntime]
[void][Windows.Globalization.Language, Windows.Globalization, ContentType = WindowsRuntime]
[void][Windows.Storage.Streams.IRandomAccessStream, Windows.Storage.Streams, ContentType = WindowsRuntime]

function Wait-WinRtResult {
    param(
        [Parameter(Mandatory = $true)]$Operation,
        [Parameter(Mandatory = $true)][Type]$ResultType
    )

    $method = [System.WindowsRuntimeSystemExtensions].GetMethods() |
        Where-Object {
            $_.Name -eq 'AsTask' -and
            $_.IsGenericMethod -and
            $_.GetParameters().Count -eq 1 -and
            $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
        } |
        Select-Object -First 1
    if ($null -eq $method) { throw 'Windows Runtime AsTask bridge is unavailable.' }
    $task = $method.MakeGenericMethod($ResultType).Invoke($null, @($Operation))
    return $task.GetAwaiter().GetResult()
}

$storageFile = Wait-WinRtResult ([Windows.Storage.StorageFile]::GetFileFromPathAsync((Resolve-Path -LiteralPath $Image).Path)) ([Windows.Storage.StorageFile])
$stream = $null
$bitmap = $null
try {
    $stream = Wait-WinRtResult ($storageFile.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
    $decoder = Wait-WinRtResult ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
    $bitmap = Wait-WinRtResult ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])

    $engine = $null
    try {
        $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage([Windows.Globalization.Language]::new($Language))
    }
    catch { }
    if ($null -eq $engine) {
        $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
    }
    if ($null -eq $engine) {
        throw "No Windows OCR language pack is available for '$Language'."
    }

    $result = Wait-WinRtResult ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
    $text = ($result.Lines | ForEach-Object { $_.Text }) -join [Environment]::NewLine
    do {
        $previous = $text
        $text = [regex]::Replace($text, '(?<=[\u3400-\u9FFF])\s+(?=[\u3400-\u9FFF])', '')
    } while ($text -ne $previous)

    $parent = Split-Path -Parent $Output
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($Output, $text, [System.Text.UTF8Encoding]::new($false))
    Write-Output $Output
}
finally {
    if ($null -ne $bitmap) { $bitmap.Dispose() }
    if ($null -ne $stream) { $stream.Dispose() }
}
