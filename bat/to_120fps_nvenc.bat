@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Interpolate videos below TARGET_FPS to TARGET_FPS using NVEncC NVOF FRUC.
rem Default output is HEVC Main10 in MKV. Audio/subtitle/chapter/data streams are copied.

if not defined TARGET_FPS set "TARGET_FPS=120"
if not defined FRUC_NVENC_QVBR set "FRUC_NVENC_QVBR=20"
if not defined FRUC_NVENC_PRESET set "FRUC_NVENC_PRESET=P7"
if not defined FRUC_NVENC_BFRAMES set "FRUC_NVENC_BFRAMES=3"
if not defined FRUC_AVSYNC set "FRUC_AVSYNC=forcecfr"
if not defined FRUC_TEMPORAL_AQ set "FRUC_TEMPORAL_AQ=0"
if not defined FRUC_DEINTERLACE set "FRUC_DEINTERLACE=0"
if not defined FRUC_NORMALIZE_CFR set "FRUC_NORMALIZE_CFR=auto"
if not defined FRUC_NORMALIZE_CRF set "FRUC_NORMALIZE_CRF=10"
if not defined FRUC_NORMALIZE_PRESET set "FRUC_NORMALIZE_PRESET=veryfast"
if not defined FRUC_KEEP_TEMP set "FRUC_KEEP_TEMP=0"

set "PAUSE_ON_EXIT="
set "failed="

if "%~1"=="" (
    set "PAUSE_ON_EXIT=1"
    echo Usage: %~nx0 "input_video" ["input_video2" ...]
    set /p "prompt_input=Input video path: "
    call :ProcessOne "%prompt_input%"
    set "rc=%ERRORLEVEL%"
    if defined PAUSE_ON_EXIT pause
    exit /b %rc%
)

:arg_loop
if "%~1"=="" goto done
call :ProcessOne "%~1"
if errorlevel 1 set "failed=1"
shift
goto arg_loop

:done
if defined PAUSE_ON_EXIT pause
if defined failed exit /b 1
exit /b 0

:ProcessOne
set "in=%~1"
set "in=%in:"=%"

if not exist "%in%" (
    echo Input not found: "%in%"
    exit /b 1
)

where NVEncC64.exe >nul 2>nul
if errorlevel 1 (
    echo NVEncC64.exe was not found in PATH.
    exit /b 2
)

where ffprobe >nul 2>nul
if errorlevel 1 (
    echo ffprobe was not found in PATH.
    exit /b 2
)

where ffmpeg >nul 2>nul
if errorlevel 1 (
    echo ffmpeg was not found in PATH.
    exit /b 2
)

NVEncC64.exe --help 2>nul | findstr /I /C:"--vpp-fruc" >nul
if errorlevel 1 (
    echo This NVEncC64.exe does not expose the FRUC interpolation filter.
    exit /b 3
)

NVEncC64.exe --check-features 2>nul | findstr /I /R /C:"nvof.*fruc.*yes" >nul
if errorlevel 1 (
    echo NVOF FRUC is not available on this GPU/driver.
    exit /b 3
)

set "src_fps="
set "src_rfps="
set "needs_fruc="
set "fruc_ratio_warn="
set "field_order="
set "src_vfr="
set "normalize_fps="
set "__FRUC_INPUT=%in%"

for /f "usebackq tokens=1,2,3,4,5,6,7 delims=|" %%A in (`powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; function Get-Fps([string]$rate){ if([string]::IsNullOrWhiteSpace($rate) -or $rate -eq '0/0'){ return 0.0 }; if($rate -match '^([0-9.]+)/([0-9.]+)$'){ $den=[double]$matches[2]; if($den -eq 0){ return 0.0 }; return ([double]$matches[1]/$den) }; return [double]$rate }; $path=$env:__FRUC_INPUT; $target=[double]$env:TARGET_FPS; $json=& ffprobe -v error -select_streams v:0 -show_entries stream=avg_frame_rate,r_frame_rate,field_order -of json $path | ConvertFrom-Json; if(-not $json.streams){ throw 'No video stream found.' }; $stream=$json.streams[0]; $avg=Get-Fps $stream.avg_frame_rate; $rfps=Get-Fps $stream.r_frame_rate; if($avg -le 0){ $avg=$rfps }; if($rfps -le 0){ $rfps=$avg }; if($avg -le 0){ throw 'Invalid frame rate.' }; $needs=if($avg -lt $target){'1'}else{'0'}; $ratioWarn=if(($target/$avg) -gt 2.01){'1'}else{'0'}; $vfr=if([Math]::Abs($avg-$rfps) -gt 0.01){'1'}else{'0'}; $norm=Get-Fps $env:FRUC_NORMALIZE_FPS; if($norm -le 0){ $norm=$rfps }; $field=if($stream.field_order){[string]$stream.field_order}else{'unknown'}; '{0:0.######}|{1:0.######}|{2}|{3}|{4}|{5}|{6:0.######}' -f $avg,$rfps,$needs,$ratioWarn,$field,$vfr,$norm"`) do (
    set "src_fps=%%A"
    set "src_rfps=%%B"
    set "needs_fruc=%%C"
    set "fruc_ratio_warn=%%D"
    set "field_order=%%E"
    set "src_vfr=%%F"
    set "normalize_fps=%%G"
)

set "__FRUC_INPUT="

if not defined src_fps (
    echo Could not read source frame rate: "%in%"
    exit /b 4
)

echo.
echo [NVEncC FRUC %TARGET_FPS%fps] "%in%"
echo Source FPS avg/tbr: %src_fps% / %src_rfps%

if "%needs_fruc%"=="0" (
    echo Skip: source FPS is already %TARGET_FPS% or higher.
    exit /b 0
)

if "%fruc_ratio_warn%"=="1" (
    echo Warning: this is more than 2x interpolation. NVOF FRUC can flicker on difficult motion.
)

set "normalize_cfr=0"
if /I "%FRUC_NORMALIZE_CFR%"=="1" set "normalize_cfr=1"
if /I "%FRUC_NORMALIZE_CFR%"=="yes" set "normalize_cfr=1"
if /I "%FRUC_NORMALIZE_CFR%"=="true" set "normalize_cfr=1"
if /I "%FRUC_NORMALIZE_CFR%"=="force" set "normalize_cfr=1"
if /I "%FRUC_NORMALIZE_CFR%"=="auto" if "%src_vfr%"=="1" set "normalize_cfr=1"

if "%src_vfr%"=="1" (
    echo Warning: source frame timing is VFR/irregular. Direct FRUC can flash or jump.
)

if /I not "%field_order%"=="progressive" if /I not "%field_order%"=="unknown" (
    echo Warning: source field_order is %field_order%. Interlaced/telecine sources can flicker after FRUC.
    echo          Try: set FRUC_DEINTERLACE=1
)

for %%F in ("%in%") do (
    set "outdir=%%~dpFrtx_exports"
    set "base=%%~nF"
)

if not exist "%outdir%" mkdir "%outdir%"
set "normalize_tag=%normalize_fps%"
set "normalize_tag=%normalize_tag:/=_%"
set "normalize_tag=%normalize_tag:.=_%"
set "output_base=%base%"
if "%normalize_cfr%"=="1" set "output_base=%base%_cfr%normalize_tag%"
set "out=%outdir%\%output_base%_fruc%TARGET_FPS%_hevc10.mkv"
set "FRUC_TEMPORAL_AQ_FLAG="
set "FRUC_DEINTERLACE_FLAG="
set "FRUC_BREF_MODE_FLAG=--bref-mode middle"
set "encode_in=%in%"
set "temp_norm="
if not "%FRUC_TEMPORAL_AQ%"=="0" set "FRUC_TEMPORAL_AQ_FLAG=--aq-temporal"
if not "%FRUC_DEINTERLACE%"=="0" set "FRUC_DEINTERLACE_FLAG=--vpp-deinterlace adaptive"
if "%FRUC_NVENC_BFRAMES%"=="0" set "FRUC_BREF_MODE_FLAG="
if "%normalize_cfr%"=="1" set "temp_norm=%outdir%\%base%_cfr%normalize_tag%_%RANDOM%%RANDOM%.mkv"

if "%normalize_cfr%"=="1" (
    echo Pre-normalize video timing to CFR %normalize_fps%fps: "%temp_norm%"
    ffmpeg -hide_banner -v warning -stats -y -i "%in%" -map 0 -vf "fps=%normalize_fps%" -c:v libx264 -preset %FRUC_NORMALIZE_PRESET% -crf %FRUC_NORMALIZE_CRF% -pix_fmt yuv420p -c:a copy -c:s copy -c:d copy -c:t copy -map_metadata 0 -map_chapters 0 -max_muxing_queue_size 4096 "%temp_norm%"
    if errorlevel 1 (
        echo Failed to create CFR pre-normalized input.
        if exist "%temp_norm%" del /q "%temp_norm%" >nul 2>nul
        exit /b 5
    )
    set "encode_in=%temp_norm%"
)

echo Output: "%out%"
echo.

NVEncC64.exe --avhw -i "%encode_in%" -o "%out%" ^
    --codec hevc --profile main10 --output-depth 10 --output-csp yuv420 ^
    --qvbr %FRUC_NVENC_QVBR% --preset %FRUC_NVENC_PRESET% --multipass 2pass-full ^
    --lookahead 32 --lookahead-level 3 --aq %FRUC_TEMPORAL_AQ_FLAG% --bframes %FRUC_NVENC_BFRAMES% %FRUC_BREF_MODE_FLAG% ^
    %FRUC_DEINTERLACE_FLAG% --vpp-fruc fps=%TARGET_FPS% ^
    --colormatrix auto --colorprim auto --transfer auto --colorrange auto ^
    --audio-copy --sub-copy --chapter-copy --data-copy --attachment-copy --metadata copy --video-metadata copy ^
    --avsync %FRUC_AVSYNC% --output-format matroska ^
    --log-level info

set "rc=%ERRORLEVEL%"
if defined temp_norm if "%FRUC_KEEP_TEMP%"=="0" if exist "%temp_norm%" del /q "%temp_norm%" >nul 2>nul

if "%rc%"=="0" (
    echo Done: "%out%"
) else (
    echo Failed with exit code %rc%.
)

exit /b %rc%
