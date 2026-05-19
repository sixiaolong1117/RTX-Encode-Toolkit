@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Hardware HEVC/H.265 encode for NVIDIA RTX 3060 via NVENC.
rem RTX 3060 supports HEVC NVENC, but not AV1 NVENC or H.266/VVC hardware encode.
rem Very tiny videos can be rejected by NVENC because of hardware minimum dimensions.
rem Hardware encoding is fast, but less storage-efficient than x265/VVenC/SVT-AV1.
rem Lower CQ means higher quality and larger files. Useful storage range: 21-26.
if not defined NVENC_CQ set "NVENC_CQ=23"
if not defined NVENC_PRESET set "NVENC_PRESET=p7"
if not defined NVENC_PIX_FMT set "NVENC_PIX_FMT=p010le"

set "PAUSE_ON_EXIT="
if "%~1"=="" (
    set "PAUSE_ON_EXIT=1"
    echo Usage: %~nx0 "input_video"
    set /p "in=Input video path: "
) else (
    set "in=%~1"
)

set "in=%in:"=%"
if not exist "%in%" (
    echo Input not found: "%in%"
    if defined PAUSE_ON_EXIT pause
    exit /b 1
)

where ffmpeg >nul 2>nul
if errorlevel 1 (
    echo ffmpeg was not found in PATH.
    if defined PAUSE_ON_EXIT pause
    exit /b 2
)

ffmpeg -hide_banner -encoders 2>nul | findstr /I /C:"hevc_nvenc" >nul
if errorlevel 1 (
    echo Required encoder not found: hevc_nvenc
    if defined PAUSE_ON_EXIT pause
    exit /b 3
)

for %%F in ("%in%") do (
    set "outdir=%%~dpFencoded_compare"
    set "base=%%~nF"
)

if not exist "%outdir%" mkdir "%outdir%"
set "out=%outdir%\%base%_h265_nvenc_cq%NVENC_CQ%_10bit.mkv"

echo.
echo [H.265 NVENC RTX 3060] "%in%"
echo Output: "%out%"
echo.

ffmpeg -hide_banner -y -hwaccel auto -i "%in%" ^
    -map 0:v:0 -map 0:a? -map 0:s? -map 0:t? -map_metadata 0 ^
    -c:a copy -c:s copy -c:t copy ^
    -c:v hevc_nvenc -preset %NVENC_PRESET% -tune hq ^
    -rc vbr -cq %NVENC_CQ% -b:v 0 -multipass fullres ^
    -profile:v main10 -pix_fmt %NVENC_PIX_FMT% ^
    -spatial-aq 1 -temporal-aq 1 -rc-lookahead 32 -b_ref_mode middle ^
    -max_muxing_queue_size 4096 ^
    "%out%"

set "rc=%ERRORLEVEL%"
if "%rc%"=="0" (
    echo Done: "%out%"
) else (
    echo Failed with exit code %rc%.
)

if defined PAUSE_ON_EXIT pause
exit /b %rc%
