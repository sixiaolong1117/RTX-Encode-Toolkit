@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Software HEVC/H.265 encode via libx265.
rem Storage-quality default: a visually strong 10-bit encode that lets newer
rem codecs beat it on size instead of forcing every output to the same bitrate.
rem Lower CRF means higher quality and larger files. Useful storage range: 24-28.
if not defined X265_CRF set "X265_CRF=26"
if not defined X265_PRESET set "X265_PRESET=slow"
if not defined X265_PIX_FMT set "X265_PIX_FMT=yuv420p10le"

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

ffmpeg -hide_banner -encoders 2>nul | findstr /I /C:"libx265" >nul
if errorlevel 1 (
    echo Required encoder not found: libx265
    if defined PAUSE_ON_EXIT pause
    exit /b 3
)

for %%F in ("%in%") do (
    set "outdir=%%~dpFencoded_compare"
    set "base=%%~nF"
)

if not exist "%outdir%" mkdir "%outdir%"
set "out=%outdir%\%base%_h265_x265_crf%X265_CRF%_10bit.mkv"

echo.
echo [H.265 x265] "%in%"
echo Output: "%out%"
echo.

ffmpeg -hide_banner -y -i "%in%" ^
    -map 0:v:0 -map 0:a? -map 0:s? -map 0:t? -map_metadata 0 ^
    -c:a copy -c:s copy -c:t copy ^
    -c:v libx265 -preset %X265_PRESET% -crf %X265_CRF% -pix_fmt %X265_PIX_FMT% ^
    -x265-params "aq-mode=3:repeat-headers=1:log-level=warning" ^
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
