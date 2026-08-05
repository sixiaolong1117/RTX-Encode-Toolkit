#Requires -Version 7.0
<#
.SYNOPSIS
  下载 NVEncC 与 ffmpeg 的 Windows x64 二进制到仓库根目录 tools/，
  供应用内置打包（开箱即用，无需用户手动配置工具路径）。

.DESCRIPTION
  - NVEncC:   rigaya/NVEnc release (.7z)，用 7-Zip 官方精简版 7zr.exe 解压
  - ffmpeg:   BtbN/FFmpeg-Builds release (.zip，GPL 构建，含 ffmpeg + ffprobe + libx264)
  两者均做 sha256 校验。tools/ 不入 git，由 csproj 在构建时复制到输出目录。

  升级版本：修改下方 $NVEncTag / $FFmpegAsset 常量（NVEncC 的 sha256 一并更新，
  ffmpeg 的 sha256 自动从 release 的 checksums.sha256 读取）。

.EXAMPLE
  ./scripts/Download-Tools.ps1
  ./scripts/Download-Tools.ps1 -Force   # 强制重新下载
#>
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# ---------- 版本常量（升级时修改这里） ----------
$NVEncTag    = '9.30'
$NVEncAsset  = "NVEncC_${NVEncTag}_x64.7z"
$NVEncSha256 = '6b17bdeb990cd63b731634f0ccdf7f7e7275723d75065353842b021d21832ba8'

$FFmpegRepo  = 'BtbN/FFmpeg-Builds'
$FFmpegAsset = 'ffmpeg-n7.1-latest-win64-gpl-7.1.zip'
# ------------------------------------------------

$RepoRoot   = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$ToolsDir   = Join-Path $RepoRoot 'tools'
$TempDir    = Join-Path $env:TEMP ("rtx-tools-" + [guid]::NewGuid().ToString('N'))

$RequiredExes = @('NVEncC64.exe', 'ffmpeg.exe', 'ffprobe.exe')
# 幂等标记：记录当前版本，tools/ 与之匹配则跳过（升级版本后自动重新下载）
$VersionMarker = "NVEnc=$NVEncTag`n$NVEncAsset=$NVEncSha256`nFFmpeg=$FFmpegAsset"

function Invoke-Download {
    param([string]$Url, [string]$Dest, [string]$Sha256)
    Write-Host "下载 $Url ..."
    curl.exe -sS -L --fail --output $Dest $Url
    if ($LASTEXITCODE -ne 0) { throw "下载失败: $Url (exit $LASTEXITCODE)" }
    if ($Sha256) {
        $actual = (Get-FileHash -LiteralPath $Dest -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $Sha256.ToLowerInvariant()) {
            throw "sha256 校验失败: $([IO.Path]::GetFileName($Dest))`n期望 $Sha256`n实际 $actual"
        }
        Write-Host "  sha256 OK: $($Sha256.Substring(0, 16))..."
    }
}

try {
    New-Item -ItemType Directory -Force $ToolsDir | Out-Null

    # 幂等：3 个 exe 已就位且版本标记匹配则跳过（-Force 强制重下）
    $markerPath = Join-Path $ToolsDir '.version'
    $allPresent = -not ($RequiredExes | ForEach-Object { Test-Path (Join-Path $ToolsDir $_) } | Where-Object { -not $_ })
    $markerMatch = (Test-Path $markerPath) -and ((Get-Content $markerPath -Raw) -eq $VersionMarker)
    if (-not $Force -and $allPresent -and $markerMatch) {
        Write-Host "tools/ 已就绪 ($($RequiredExes -join ', '))，跳过。使用 -Force 强制重新下载。"
        exit 0
    }
    Write-Host 'tools/ 不存在或版本不匹配，开始下载。'

    New-Item -ItemType Directory -Force $TempDir | Out-Null

    # ---------- 1. ffmpeg (zip) ----------
    $ffmpegZip  = Join-Path $TempDir $FFmpegAsset
    $checksums  = Join-Path $TempDir 'checksums.sha256'
    $ffmpegSha  = $null
    try {
        Invoke-Download "https://github.com/$FFmpegRepo/releases/download/latest/checksums.sha256" $checksums
        $ffmpegSha = (Get-Content $checksums | Where-Object { $_ -match [regex]::Escape($FFmpegAsset) }) -split '\s+' | Select-Object -First 1
    }
    catch { Write-Warning "获取 ffmpeg 校验和失败，跳过校验: $_" }
    Invoke-Download "https://github.com/$FFmpegRepo/releases/download/latest/$FFmpegAsset" $ffmpegZip $ffmpegSha
    Write-Host "解压 $FFmpegAsset ..."
    Expand-Archive -LiteralPath $ffmpegZip -DestinationPath (Join-Path $TempDir 'ffmpeg') -Force
    Copy-Item (Get-ChildItem -Recurse -Filter 'ffmpeg.exe' (Join-Path $TempDir 'ffmpeg') | Select-Object -First 1).FullName $ToolsDir
    Copy-Item (Get-ChildItem -Recurse -Filter 'ffprobe.exe' (Join-Path $TempDir 'ffmpeg') | Select-Object -First 1).FullName $ToolsDir
    # 随包附上 ffmpeg 的 GPL 许可文本（独立进程分发，不传染 MIT 主程序）
    $ffmpegLicense = Get-ChildItem -Recurse -File (Join-Path $TempDir 'ffmpeg') | Where-Object { $_.Name -match 'LICENSE' } | Select-Object -First 1
    if ($ffmpegLicense) { Copy-Item $ffmpegLicense.FullName (Join-Path $ToolsDir 'ffmpeg-LICENSE.txt') }

    # ---------- 2. NVEncC (7z) ----------
    $nvenc7z = Join-Path $TempDir $NVEncAsset
    Invoke-Download "https://github.com/rigaya/NVEnc/releases/download/$NVEncTag/$NVEncAsset" $nvenc7z $NVEncSha256
    $sevenZip = Join-Path $TempDir '7zr.exe'
    if (-not (Test-Path $sevenZip)) {
        Invoke-Download 'https://www.7-zip.org/a/7zr.exe' $sevenZip
    }
    Write-Host "解压 $NVEncAsset ..."
    & $sevenZip x $nvenc7z "-o$TempDir\nvenc" -y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "7z 解压失败: $NVEncAsset" }
    Copy-Item (Get-ChildItem -Recurse -Filter 'NVEncC64.exe' (Join-Path $TempDir 'nvenc') | Select-Object -First 1).FullName $ToolsDir

    Set-Content -LiteralPath $markerPath -Value $VersionMarker -NoNewline

    Write-Host "完成。工具已放入 $ToolsDir :"
    Get-ChildItem $ToolsDir -File | ForEach-Object { Write-Host "  $($_.Name) ($([Math]::Round($_.Length/1MB, 1)) MB)" }
}
finally {
    if (Test-Path $TempDir) { Remove-Item -Recurse -Force $TempDir }
}
