@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Software VVC/H.266 encode via libvvenc.
rem Storage-quality default: VVC should usually be smaller than HEVC at similar
rem visual quality. Lower QP means higher quality and larger files.
rem Useful storage range: 24-30. QP 32 can be too aggressive for many sources.
rem libvvenc preset: 0=faster, 1=fast, 2=medium, 3=slow, 4=slower.
if not defined VVENC_QP set "VVENC_QP=26"
if not defined VVENC_PRESET set "VVENC_PRESET=3"
if not defined VVENC_PERIOD set "VVENC_PERIOD=4"
if not defined VVENC_PIX_FMT set "VVENC_PIX_FMT=yuv420p10le"

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

ffmpeg -hide_banner -encoders 2>nul | findstr /I /C:"libvvenc" >nul
if errorlevel 1 (
    echo Required encoder not found: libvvenc
    if defined PAUSE_ON_EXIT pause
    exit /b 3
)

for %%F in ("%in%") do (
    set "outdir=%%~dpFencoded_compare"
    set "base=%%~nF"
)

if not exist "%outdir%" mkdir "%outdir%"
set "out=%outdir%\%base%_h266_vvenc_qp%VVENC_QP%_10bit.mkv"

echo.
echo [H.266 VVenC] "%in%"
echo Output: "%out%"
echo.

ffmpeg -hide_banner -y -i "%in%" ^
    -map 0:v:0 -map 0:a? -map 0:s? -map 0:t? -map_metadata 0 ^
    -c:a copy -c:s copy -c:t copy ^
    -c:v libvvenc -preset %VVENC_PRESET% -qp %VVENC_QP% -qpa 1 -period %VVENC_PERIOD% -pix_fmt %VVENC_PIX_FMT% ^
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
