@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Software AV1 encode via SVT-AV1.
rem Storage-quality default: AV1 should usually beat HEVC on size at similar
rem visual quality. Lower CRF means higher quality and larger files.
rem Useful storage range: 30-36. Preset 5 is slower than 6 but compresses better.
if not defined SVTAV1_CRF set "SVTAV1_CRF=34"
if not defined SVTAV1_PRESET set "SVTAV1_PRESET=5"
if not defined SVTAV1_GOP set "SVTAV1_GOP=240"
if not defined SVTAV1_PIX_FMT set "SVTAV1_PIX_FMT=yuv420p10le"

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

ffmpeg -hide_banner -encoders 2>nul | findstr /I /C:"libsvtav1" >nul
if errorlevel 1 (
    echo Required encoder not found: libsvtav1
    if defined PAUSE_ON_EXIT pause
    exit /b 3
)

for %%F in ("%in%") do (
    set "outdir=%%~dpFencoded_compare"
    set "base=%%~nF"
)

if not exist "%outdir%" mkdir "%outdir%"
set "out=%outdir%\%base%_av1_svtav1_crf%SVTAV1_CRF%_10bit.mkv"

echo.
echo [AV1 SVT-AV1] "%in%"
echo Output: "%out%"
echo.

ffmpeg -hide_banner -y -i "%in%" ^
    -map 0:v:0 -map 0:a? -map 0:s? -map 0:t? -map_metadata 0 ^
    -c:a copy -c:s copy -c:t copy ^
    -c:v libsvtav1 -preset %SVTAV1_PRESET% -crf %SVTAV1_CRF% -pix_fmt %SVTAV1_PIX_FMT% ^
    -g %SVTAV1_GOP% ^
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
