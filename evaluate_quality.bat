@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Quality evaluation wrapper. The PowerShell body below does the heavy lifting
rem because parsing VMAF JSON and FFmpeg metric logs is much safer there.
set "__EVALUATE_QUALITY_BAT=%~f0"
set "__EVALUATE_ARG1=%~1"
set "__EVALUATE_ARG2=%~2"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$script = Get-Content -Raw -LiteralPath $env:__EVALUATE_QUALITY_BAT; $marker = '# POWERSHELL_SCRIPT_BELOW'; $pos = $script.LastIndexOf($marker); if ($pos -lt 0) { throw 'PowerShell body marker not found.' }; $body = $script.Substring($pos + $marker.Length); & ([scriptblock]::Create($body)) $env:__EVALUATE_ARG1 $env:__EVALUATE_ARG2"
exit /b %ERRORLEVEL%

# POWERSHELL_SCRIPT_BELOW
param(
    [string]$OriginalVideo,
    [string]$EncodedDir
)

$ErrorActionPreference = 'Stop'

function Exit-WithMessage {
    param(
        [string]$Message,
        [int]$Code = 1
    )
    Write-Host $Message
    exit $Code
}

function Get-FirstMatch {
    param(
        [string]$Path,
        [string]$Pattern
    )
    if (-not (Test-Path -LiteralPath $Path)) {
        return ''
    }

    $text = Get-Content -Raw -LiteralPath $Path
    $matches = [regex]::Matches($text, $Pattern)
    if ($matches.Count -eq 0) {
        return ''
    }

    return $matches[$matches.Count - 1].Groups[1].Value
}

function Invoke-MetricFfmpeg {
    param(
        [string[]]$Arguments,
        [string]$LogPath,
        [string]$WorkingDir
    )

    Push-Location -LiteralPath $WorkingDir
    try {
        $oldPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        & ffmpeg @Arguments *> $LogPath
        $exitCode = $LASTEXITCODE
        $ErrorActionPreference = $oldPreference
        return $exitCode
    }
    finally {
        if ($oldPreference) {
            $ErrorActionPreference = $oldPreference
        }
        Pop-Location
    }
}

if ([string]::IsNullOrWhiteSpace($OriginalVideo)) {
    Write-Host 'Usage: evaluate_quality.bat "input_video" ["encoded_compare_folder"]'
    $OriginalVideo = Read-Host 'Input video path'
}

$OriginalVideo = $OriginalVideo.Trim('"')
if (-not (Test-Path -LiteralPath $OriginalVideo)) {
    Exit-WithMessage "Input not found: `"$OriginalVideo`"" 1
}

$ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
if (-not $ffmpeg) {
    Exit-WithMessage 'ffmpeg was not found in PATH.' 2
}

$ffprobe = Get-Command ffprobe -ErrorAction SilentlyContinue
if (-not $ffprobe) {
    Exit-WithMessage 'ffprobe was not found in PATH.' 2
}

$originalItem = Get-Item -LiteralPath $OriginalVideo
$baseName = [IO.Path]::GetFileNameWithoutExtension($originalItem.Name)

if ([string]::IsNullOrWhiteSpace($EncodedDir)) {
    $EncodedDir = Join-Path $originalItem.DirectoryName 'encoded_compare'
}
$EncodedDir = $EncodedDir.Trim('"')
if (-not (Test-Path -LiteralPath $EncodedDir)) {
    Exit-WithMessage "Encoded folder not found: `"$EncodedDir`"" 1
}

$filtersText = & ffmpeg -hide_banner -filters 2>$null | Out-String
$hasVmaf = $filtersText -match '\blibvmaf\b'
$hasSsim = $filtersText -match '\bssim\b'
$hasPsnr = $filtersText -match '\bpsnr\b'

if (-not ($hasVmaf -or $hasSsim -or $hasPsnr)) {
    Exit-WithMessage 'No supported FFmpeg quality filters were found. Need at least one of: libvmaf, ssim, psnr.' 3
}

$reportDir = Join-Path $EncodedDir 'quality_reports'
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null

$summaryCsv = Join-Path $reportDir ("{0}_quality_summary.csv" -f $baseName)
if (Test-Path -LiteralPath $summaryCsv) {
    Remove-Item -LiteralPath $summaryCsv -Force
}

$encodedFiles = @()
if (-not [string]::IsNullOrWhiteSpace($env:QUALITY_ENCODED_FILES)) {
    $encodedFiles = $env:QUALITY_ENCODED_FILES -split '\|' |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object {
            if (Test-Path -LiteralPath $_) {
                Get-Item -LiteralPath $_
            }
        } |
        Sort-Object Name
}

if (-not $encodedFiles) {
    $encodedFiles = Get-ChildItem -LiteralPath $EncodedDir -File -Filter '*.mkv' |
        Where-Object { $_.Name.StartsWith("$baseName`_", [StringComparison]::OrdinalIgnoreCase) } |
        Sort-Object Name
}

if (-not $encodedFiles) {
    Exit-WithMessage "No encoded MKV files found in `"$EncodedDir`" for base name `"$baseName`"." 4
}

Write-Host ''
Write-Host '===== Quality evaluation ====='
Write-Host "Reference: $($originalItem.FullName)"
Write-Host "Reports:   $reportDir"
Write-Host ''
Write-Host 'Metrics: VMAF is usually the most useful single score; SSIM/PSNR are supporting signals.'
if ($env:TARGET_VMAF -match '^\d+(\.\d+)?$') {
    Write-Host "Warning threshold: VMAF below $env:TARGET_VMAF will be marked below target."
}
Write-Host ''

$rows = New-Object System.Collections.Generic.List[object]
$index = 0

foreach ($encoded in $encodedFiles) {
    $index++
    $safeName = ($encoded.BaseName -replace '[^A-Za-z0-9._-]', '_')
    if ($safeName.Length -gt 80) {
        $safeName = $safeName.Substring(0, 80)
    }
    $prefix = '{0:D2}_{1}' -f $index, $safeName

    $vmafJsonLeaf = "${prefix}_vmaf.json"
    $vmafLog = Join-Path $reportDir "${prefix}_vmaf_ffmpeg.log"
    $ssimStatsLeaf = "${prefix}_ssim_frames.log"
    $ssimLog = Join-Path $reportDir "${prefix}_ssim_ffmpeg.log"
    $psnrStatsLeaf = "${prefix}_psnr_frames.log"
    $psnrLog = Join-Path $reportDir "${prefix}_psnr_ffmpeg.log"

    $vmaf = ''
    $ssim = ''
    $psnr = ''
    $status = New-Object System.Collections.Generic.List[string]

    Write-Host "Evaluating: $($encoded.Name)"

    if ($encoded.Length -le 0) {
        $status.Add('zero_size_file')
    }
    else {
        if ($hasVmaf) {
            $filter = "[0:v]settb=AVTB,setpts=PTS-STARTPTS,format=yuv420p[dist];[1:v]settb=AVTB,setpts=PTS-STARTPTS,format=yuv420p[ref];[dist][ref]libvmaf=log_path=${vmafJsonLeaf}:log_fmt=json:n_threads=0:shortest=1"
            $args = @('-hide_banner', '-nostats', '-i', $encoded.FullName, '-i', $originalItem.FullName, '-lavfi', $filter, '-an', '-f', 'null', '-')
            $rc = Invoke-MetricFfmpeg -Arguments $args -LogPath $vmafLog -WorkingDir $reportDir
            if ($rc -eq 0) {
                $vmafJsonPath = Join-Path $reportDir $vmafJsonLeaf
                try {
                    $json = Get-Content -Raw -LiteralPath $vmafJsonPath | ConvertFrom-Json
                    $vmaf = '{0:F3}' -f [double]$json.pooled_metrics.vmaf.mean
                }
                catch {
                    $vmaf = Get-FirstMatch -Path $vmafLog -Pattern 'VMAF score:\s*([0-9.]+)'
                }
            }
            else {
                $status.Add("vmaf_failed_$rc")
            }
        }
        else {
            $status.Add('vmaf_filter_missing')
        }

        if ($hasSsim) {
            $filter = "[0:v]settb=AVTB,setpts=PTS-STARTPTS,format=yuv420p[dist];[1:v]settb=AVTB,setpts=PTS-STARTPTS,format=yuv420p[ref];[dist][ref]ssim=stats_file=${ssimStatsLeaf}:shortest=1"
            $args = @('-hide_banner', '-nostats', '-i', $encoded.FullName, '-i', $originalItem.FullName, '-lavfi', $filter, '-an', '-f', 'null', '-')
            $rc = Invoke-MetricFfmpeg -Arguments $args -LogPath $ssimLog -WorkingDir $reportDir
            if ($rc -eq 0) {
                $ssim = Get-FirstMatch -Path $ssimLog -Pattern 'All:([0-9.]+)'
            }
            else {
                $status.Add("ssim_failed_$rc")
            }
        }
        else {
            $status.Add('ssim_filter_missing')
        }

        if ($hasPsnr) {
            $filter = "[0:v]settb=AVTB,setpts=PTS-STARTPTS,format=yuv420p[dist];[1:v]settb=AVTB,setpts=PTS-STARTPTS,format=yuv420p[ref];[dist][ref]psnr=stats_file=${psnrStatsLeaf}:shortest=1"
            $args = @('-hide_banner', '-nostats', '-i', $encoded.FullName, '-i', $originalItem.FullName, '-lavfi', $filter, '-an', '-f', 'null', '-')
            $rc = Invoke-MetricFfmpeg -Arguments $args -LogPath $psnrLog -WorkingDir $reportDir
            if ($rc -eq 0) {
                $psnr = Get-FirstMatch -Path $psnrLog -Pattern 'average:([0-9.]+|inf)'
            }
            else {
                $status.Add("psnr_failed_$rc")
            }
        }
        else {
            $status.Add('psnr_filter_missing')
        }

        if ($vmaf -and $env:TARGET_VMAF -match '^\d+(\.\d+)?$') {
            if ([double]$vmaf -lt [double]$env:TARGET_VMAF) {
                $status.Add("below_target_vmaf_$env:TARGET_VMAF")
            }
        }
    }

    $duration = ''
    $bitrateKbps = ''
    try {
        $durationRaw = & ffprobe -v error -show_entries format=duration -of default=nokey=1:noprint_wrappers=1 $encoded.FullName
        $bitrateRaw = & ffprobe -v error -show_entries format=bit_rate -of default=nokey=1:noprint_wrappers=1 $encoded.FullName
        $durationText = (($durationRaw | Select-Object -First 1) -as [string]).Trim()
        $bitrateText = (($bitrateRaw | Select-Object -First 1) -as [string]).Trim()
        if ($durationText) {
            $duration = '{0:F3}' -f [double]$durationText
        }
        if ($bitrateText -and $bitrateText -match '^\d+$') {
            $bitrateKbps = '{0:F1}' -f ([double]$bitrateText / 1000.0)
        }
    }
    catch {
        $status.Add('ffprobe_failed')
    }

    $statusText = if ($status.Count -eq 0) { 'ok' } else { $status -join ';' }
    $sizeMb = [Math]::Round($encoded.Length / 1MB, 3)

    $rows.Add([pscustomobject]@{
        file = $encoded.Name
        size_bytes = $encoded.Length
        size_mb = $sizeMb
        duration_sec = $duration
        bitrate_kbps = $bitrateKbps
        vmaf_mean = $vmaf
        ssim_all = $ssim
        psnr_avg = $psnr
        status = $statusText
    }) | Out-Null

    Write-Host ("  size={0} MB  VMAF={1}  SSIM={2}  PSNR={3}  {4}" -f $sizeMb, $(if ($vmaf) { $vmaf } else { 'n/a' }), $(if ($ssim) { $ssim } else { 'n/a' }), $(if ($psnr) { $psnr } else { 'n/a' }), $statusText)
}

$rows | Export-Csv -LiteralPath $summaryCsv -NoTypeInformation -Encoding UTF8

Write-Host ''
Write-Host "Summary CSV: $summaryCsv"
Write-Host ''
Write-Host 'Sorted by VMAF:'
$rows |
    Sort-Object @{ Expression = { if ($_.vmaf_mean) { [double]$_.vmaf_mean } else { -1 } }; Descending = $true } |
    Format-Table file, size_mb, bitrate_kbps, vmaf_mean, ssim_all, psnr_avg, status -AutoSize

exit 0
