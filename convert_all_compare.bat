@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Run every comparison encode for one input video.
rem Output files are written next to the source video under encoded_compare.
rem Storage-quality defaults aim for similar perceived quality, not equal bitrate.
rem You can override these before calling this script, for example:
rem   set X265_CRF=24
rem   set VVENC_QP=24
rem   set SVTAV1_CRF=32
rem   set NVENC_CQ=21
rem   set TARGET_VMAF=95

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

for %%F in ("%in%") do (
    set "outdir=%%~dpFencoded_compare"
    set "base=%%~nF"
)

set "scriptdir=%~dp0"

if not defined X265_CRF set "X265_CRF=26"
if not defined X265_PRESET set "X265_PRESET=slow"
if not defined NVENC_CQ set "NVENC_CQ=23"
if not defined NVENC_PRESET set "NVENC_PRESET=p7"
if not defined VVENC_QP set "VVENC_QP=26"
if not defined VVENC_PRESET set "VVENC_PRESET=3"
if not defined VVENC_PERIOD set "VVENC_PERIOD=4"
if not defined SVTAV1_CRF set "SVTAV1_CRF=34"
if not defined SVTAV1_PRESET set "SVTAV1_PRESET=5"
if not defined SVTAV1_GOP set "SVTAV1_GOP=240"
if not defined TARGET_VMAF set "TARGET_VMAF=93"

set "out_x265=%outdir%\%base%_h265_x265_crf%X265_CRF%_10bit.mkv"
set "out_nvenc=%outdir%\%base%_h265_nvenc_cq%NVENC_CQ%_10bit.mkv"
set "out_vvenc=%outdir%\%base%_h266_vvenc_qp%VVENC_QP%_10bit.mkv"
set "out_av1=%outdir%\%base%_av1_svtav1_crf%SVTAV1_CRF%_10bit.mkv"
set "QUALITY_ENCODED_FILES=%out_x265%|%out_nvenc%|%out_vvenc%|%out_av1%"

echo.
echo Input: "%in%"
echo Output folder: "%outdir%"
echo.
echo RTX 3060 note:
echo - HEVC/H.265 NVENC is supported and will be tested.
echo - AV1 NVENC is not supported on RTX 30 series, so AV1 uses SVT-AV1 software.
echo - H.266/VVC hardware encode is not supported here, so H.266 uses VVenC software.
echo.
echo Storage quality profile:
echo - H.265 x265:      CRF %X265_CRF%, preset %X265_PRESET%
echo - H.265 NVENC:     CQ %NVENC_CQ%, preset %NVENC_PRESET% ^(fast, larger files expected^)
echo - H.266 VVenC:     QP %VVENC_QP%, preset %VVENC_PRESET%, period %VVENC_PERIOD%s
echo - AV1 SVT-AV1:     CRF %SVTAV1_CRF%, preset %SVTAV1_PRESET%, GOP %SVTAV1_GOP%
echo - VMAF warning if below: %TARGET_VMAF%
echo.

call "%scriptdir%to_h265_x265.bat" "%in%"
set "rc_x265=%ERRORLEVEL%"

call "%scriptdir%to_h265_nvenc_3060.bat" "%in%"
set "rc_nvenc=%ERRORLEVEL%"

call "%scriptdir%to_h266_vvenc.bat" "%in%"
set "rc_vvenc=%ERRORLEVEL%"

call "%scriptdir%to_av1_svtav1.bat" "%in%"
set "rc_av1=%ERRORLEVEL%"

echo.
echo ===== Result summary =====
echo H.265 x265 software:      exit %rc_x265%
echo H.265 NVENC RTX 3060:     exit %rc_nvenc%
echo H.266 VVenC software:     exit %rc_vvenc%
echo AV1 SVT-AV1 software:     exit %rc_av1%
echo.
echo Output files:
for %%O in ("%out_x265%" "%out_nvenc%" "%out_vvenc%" "%out_av1%") do (
    if exist "%%~fO" echo %%~nxO  %%~zO bytes
)

call "%scriptdir%evaluate_quality.bat" "%in%" "%outdir%"
set "rc_quality=%ERRORLEVEL%"

if not "%rc_x265%"=="0" set "failed=1"
if not "%rc_nvenc%"=="0" set "failed=1"
if not "%rc_vvenc%"=="0" set "failed=1"
if not "%rc_av1%"=="0" set "failed=1"
if not "%rc_quality%"=="0" set "failed=1"

if defined PAUSE_ON_EXIT pause
if defined failed exit /b 1
exit /b 0
