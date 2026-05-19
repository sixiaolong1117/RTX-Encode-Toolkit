@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Export local SDR video as HDR10 using NVIDIA RTX Video HDR / NGX TrueHDR via NVEncC.
rem Default output is HEVC Main10 in MKV, tagged as BT.2020/PQ HDR10.

if not defined RTX_HDR_CONTRAST set "RTX_HDR_CONTRAST=125"
if not defined RTX_HDR_SATURATION set "RTX_HDR_SATURATION=75"
if not defined RTX_HDR_MIDDLEGRAY set "RTX_HDR_MIDDLEGRAY=44"
if not defined RTX_HDR_MAXLUMINANCE set "RTX_HDR_MAXLUMINANCE=1000"
if not defined RTX_HDR_MAXCLL set "RTX_HDR_MAXCLL=1000,400"
if not defined RTX_HDR_MASTER_DISPLAY set "RTX_HDR_MASTER_DISPLAY=G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,1)"
if not defined RTX_NVENC_QVBR set "RTX_NVENC_QVBR=20"
if not defined RTX_NVENC_PRESET set "RTX_NVENC_PRESET=P7"
if not defined RTX_NVENC_BFRAMES set "RTX_NVENC_BFRAMES=5"

set "PAUSE_ON_EXIT="
if "%~1"=="" (
    set "PAUSE_ON_EXIT=1"
    echo Usage: %~nx0 "input_sdr_video"
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

where NVEncC64.exe >nul 2>nul
if errorlevel 1 (
    echo NVEncC64.exe was not found in PATH.
    if defined PAUSE_ON_EXIT pause
    exit /b 2
)

NVEncC64.exe --help 2>nul | findstr /I /C:"--vpp-ngx-truehdr" >nul
if errorlevel 1 (
    echo This NVEncC64.exe does not expose RTX HDR / NGX TrueHDR.
    if defined PAUSE_ON_EXIT pause
    exit /b 3
)

for %%F in ("%in%") do (
    set "outdir=%%~dpFrtx_exports"
    set "base=%%~nF"
)

if not exist "%outdir%" mkdir "%outdir%"
set "out=%outdir%\%base%_rtx_hdr_hevc10_hdr10.mkv"

echo.
echo [RTX HDR NVEncC] "%in%"
echo Output: "%out%"
echo TrueHDR: contrast=%RTX_HDR_CONTRAST%, saturation=%RTX_HDR_SATURATION%, middlegray=%RTX_HDR_MIDDLEGRAY%, maxluminance=%RTX_HDR_MAXLUMINANCE%
echo.

NVEncC64.exe --avhw -i "%in%" -o "%out%" ^
    --codec hevc --profile main10 --output-depth 10 --output-csp yuv420 ^
    --qvbr %RTX_NVENC_QVBR% --preset %RTX_NVENC_PRESET% --multipass 2pass-full ^
    --lookahead 32 --lookahead-level 3 --aq --aq-temporal --bframes %RTX_NVENC_BFRAMES% --bref-mode middle ^
    --vpp-ngx-truehdr contrast=%RTX_HDR_CONTRAST%,saturation=%RTX_HDR_SATURATION%,middlegray=%RTX_HDR_MIDDLEGRAY%,maxluminance=%RTX_HDR_MAXLUMINANCE% ^
    --colormatrix bt2020nc --colorprim bt2020 --transfer smpte2084 --max-cll %RTX_HDR_MAXCLL% --master-display "%RTX_HDR_MASTER_DISPLAY%" ^
    --audio-copy --sub-copy --chapter-copy --data-copy --attachment-copy --metadata copy ^
    --avsync auto --output-format matroska ^
    --log-level info

set "rc=%ERRORLEVEL%"
if "%rc%"=="0" (
    echo Done: "%out%"
) else (
    echo Failed with exit code %rc%.
)

if defined PAUSE_ON_EXIT pause
exit /b %rc%
