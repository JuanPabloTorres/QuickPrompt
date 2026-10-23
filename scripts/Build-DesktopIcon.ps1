param([string]$OutputPath = (Join-Path $PSScriptRoot '../apps/web/public/branding/quickprompt.ico'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$novaSource = [Drawing.Image]::FromFile((Join-Path $PSScriptRoot '../apps/web/public/branding/quickprompt_official.png'))
try {
    $novaFrames = @()
    foreach ($novaSize in @(16, 24, 32, 48, 64, 128, 256)) {
        $novaBitmap = New-Object Drawing.Bitmap $novaSize, $novaSize
        $novaGraphics = [Drawing.Graphics]::FromImage($novaBitmap)
        $novaMemory = New-Object IO.MemoryStream
        try {
            $novaGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $novaGraphics.DrawImage($novaSource, 0, 0, $novaSize, $novaSize)
            $novaBitmap.Save($novaMemory, [Drawing.Imaging.ImageFormat]::Png)
            $novaFrames += @{ size = $novaSize; bytes = $novaMemory.ToArray() }
        } finally { $novaGraphics.Dispose(); $novaBitmap.Dispose(); $novaMemory.Dispose() }
    }
    $novaDestination = [IO.File]::Create([IO.Path]::GetFullPath($OutputPath))
    $novaWriter = New-Object IO.BinaryWriter $novaDestination
    try {
        $novaWriter.Write([uint16]0); $novaWriter.Write([uint16]1); $novaWriter.Write([uint16]$novaFrames.Count)
        $novaOffset = 6 + 16 * $novaFrames.Count
        foreach ($novaFrame in $novaFrames) {
            $novaDimension = if ($novaFrame.size -eq 256) { 0 } else { $novaFrame.size }
            $novaWriter.Write([byte]$novaDimension); $novaWriter.Write([byte]$novaDimension)
            $novaWriter.Write([uint16]0); $novaWriter.Write([uint16]1); $novaWriter.Write([uint16]32)
            $novaWriter.Write([uint32]$novaFrame.bytes.Length); $novaWriter.Write([uint32]$novaOffset)
            $novaOffset += $novaFrame.bytes.Length
        }
        foreach ($novaFrame in $novaFrames) { $novaWriter.Write([byte[]]$novaFrame.bytes) }
    } finally { $novaWriter.Dispose(); $novaDestination.Dispose() }
} finally { $novaSource.Dispose() }
Write-Output ([IO.Path]::GetFullPath($OutputPath))
