@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Export local video with NVIDIA RTX VSR and NVOF FRUC interpolation via NVEncC.
rem Default output is HEVC Main10 in MKV, with audio/subtitles/chapters copied.
rem Default VSR target is 4K long edge: landscape 3840x-2, portrait -2x3840.
rem Optional 2nd arg or RTX_VSR_RES overrides the upscale target, e.g. 3840x-2.

if not defined TARGET_FPS set "TARGET_FPS=120"
if not defined RTX_VSR_LONG_EDGE set "RTX_VSR_LONG_EDGE=3840"
set "RTX_VSR_RES_EXPLICIT="
if defined RTX_VSR_RES set "RTX_VSR_RES_EXPLICIT=1"
if not "%~2"=="" (
    set "RTX_VSR_RES=%~2"
    set "RTX_VSR_RES_EXPLICIT=1"
)
if not defined RTX_VSR_QUALITY set "RTX_VSR_QUALITY=4"
if not defined RTX_NVENC_QVBR set "RTX_NVENC_QVBR=20"
if not defined RTX_NVENC_PRESET set "RTX_NVENC_PRESET=P7"
if not defined RTX_NVENC_BFRAMES set "RTX_NVENC_BFRAMES=3"
if not defined RTX_AVSYNC set "RTX_AVSYNC=forcecfr"
if not defined RTX_TEMPORAL_AQ set "RTX_TEMPORAL_AQ=0"
if not defined RTX_DEINTERLACE set "RTX_DEINTERLACE=0"
if not defined FRUC_NORMALIZE_CFR set "FRUC_NORMALIZE_CFR=auto"
if not defined FRUC_NORMALIZE_CRF set "FRUC_NORMALIZE_CRF=10"
if not defined FRUC_NORMALIZE_PRESET set "FRUC_NORMALIZE_PRESET=veryfast"
if not defined FRUC_KEEP_TEMP set "FRUC_KEEP_TEMP=0"

set "PAUSE_ON_EXIT="
if "%~1"=="" (
    set "PAUSE_ON_EXIT=1"
    echo Usage: %~nx0 "input_video" [output_res]
    echo Default: auto 4K long edge ^(landscape 3840x-2, portrait -2x3840^) + FRUC %TARGET_FPS%fps
    echo Example override: %~nx0 "input.mp4" 3840x-2
    set /p "in=Input video path: "
) else (
    set "in=%~1"
)

call :ProcessOne "%in%"
set "rc=%ERRORLEVEL%"
if defined PAUSE_ON_EXIT pause
exit /b %rc%

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

NVEncC64.exe --help 2>nul | findstr /I /C:"ngx-vsr" >nul
if errorlevel 1 (
    echo This NVEncC64.exe does not expose RTX VSR ^(ngx-vsr^).
    exit /b 3
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

if not defined RTX_VSR_RES_EXPLICIT (
    call :ResolveVsrTarget "%in%"
    if errorlevel 1 exit /b 4
)

set "src_fps="
set "src_rfps="
set "needs_fruc="
set "fruc_ratio_warn="
set "field_order="
set "src_vfr="
set "normalize_fps="
set "__RTX_FRUC_INPUT=%in%"

for /f "usebackq tokens=1,2,3,4,5,6,7 delims=|" %%A in (`powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; function Get-Fps([string]$rate){ if([string]::IsNullOrWhiteSpace($rate) -or $rate -eq '0/0'){ return 0.0 }; if($rate -match '^([0-9.]+)/([0-9.]+)$'){ $den=[double]$matches[2]; if($den -eq 0){ return 0.0 }; return ([double]$matches[1]/$den) }; return [double]$rate }; $path=$env:__RTX_FRUC_INPUT; $target=[double]$env:TARGET_FPS; $json=& ffprobe -v error -select_streams v:0 -show_entries stream=avg_frame_rate,r_frame_rate,field_order -of json $path | ConvertFrom-Json; if(-not $json.streams){ throw 'No video stream found.' }; $stream=$json.streams[0]; $avg=Get-Fps $stream.avg_frame_rate; $rfps=Get-Fps $stream.r_frame_rate; if($avg -le 0){ $avg=$rfps }; if($rfps -le 0){ $rfps=$avg }; if($avg -le 0){ throw 'Invalid frame rate.' }; $needs=if($avg -lt $target){'1'}else{'0'}; $ratioWarn=if(($target/$avg) -gt 2.01){'1'}else{'0'}; $vfr=if([Math]::Abs($avg-$rfps) -gt 0.01){'1'}else{'0'}; $norm=Get-Fps $env:FRUC_NORMALIZE_FPS; if($norm -le 0){ $norm=$rfps }; $field=if($stream.field_order){[string]$stream.field_order}else{'unknown'}; '{0:0.######}|{1:0.######}|{2}|{3}|{4}|{5}|{6:0.######}' -f $avg,$rfps,$needs,$ratioWarn,$field,$vfr,$norm"`) do (
    set "src_fps=%%A"
    set "src_rfps=%%B"
    set "needs_fruc=%%C"
    set "fruc_ratio_warn=%%D"
    set "field_order=%%E"
    set "src_vfr=%%F"
    set "normalize_fps=%%G"
)

set "__RTX_FRUC_INPUT="

if not defined src_fps (
    echo Could not read source frame rate: "%in%"
    exit /b 4
)

set "normalize_cfr=0"
if /I "%FRUC_NORMALIZE_CFR%"=="1" set "normalize_cfr=1"
if /I "%FRUC_NORMALIZE_CFR%"=="yes" set "normalize_cfr=1"
if /I "%FRUC_NORMALIZE_CFR%"=="true" set "normalize_cfr=1"
if /I "%FRUC_NORMALIZE_CFR%"=="force" set "normalize_cfr=1"
if /I "%FRUC_NORMALIZE_CFR%"=="auto" if "%src_vfr%"=="1" set "normalize_cfr=1"

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
set "out=%outdir%\%output_base%_rtx_vsr_%RTX_VSR_RES%_fruc%TARGET_FPS%_hevc10.mkv"

set "RTX_TEMPORAL_AQ_FLAG="
set "RTX_DEINTERLACE_FLAG="
set "RTX_BREF_MODE_FLAG=--bref-mode middle"
set "FRUC_FILTER_FLAG=--vpp-fruc fps=%TARGET_FPS%"
set "encode_in=%in%"
set "temp_norm="
if not "%RTX_TEMPORAL_AQ%"=="0" set "RTX_TEMPORAL_AQ_FLAG=--aq-temporal"
if not "%RTX_DEINTERLACE%"=="0" set "RTX_DEINTERLACE_FLAG=--vpp-deinterlace adaptive"
if "%RTX_NVENC_BFRAMES%"=="0" set "RTX_BREF_MODE_FLAG="
if "%needs_fruc%"=="0" set "FRUC_FILTER_FLAG="
if "%normalize_cfr%"=="1" set "temp_norm=%outdir%\%base%_cfr%normalize_tag%_%RANDOM%%RANDOM%.mkv"

echo.
echo [RTX VSR + FRUC %TARGET_FPS%fps NVEncC] "%in%"
echo Output: "%out%"
if defined RTX_VSR_RES_EXPLICIT (
    echo Target: %RTX_VSR_RES%, VSR quality %RTX_VSR_QUALITY%
) else (
    echo Target: %RTX_VSR_RES% ^(4K long edge %RTX_VSR_LONG_EDGE%, %RTX_VSR_ORIENTATION%, display %RTX_VSR_SOURCE_DISPLAY%, rotation %RTX_VSR_ROTATION%^), VSR quality %RTX_VSR_QUALITY%
)
echo Source FPS avg/tbr: %src_fps% / %src_rfps%

if "%needs_fruc%"=="0" (
    echo FRUC: skipped because source FPS is already %TARGET_FPS% or higher.
) else (
    echo FRUC: %TARGET_FPS%fps
)

if "%fruc_ratio_warn%"=="1" (
    echo Warning: this is more than 2x interpolation. NVOF FRUC can flicker on difficult motion.
)

if "%src_vfr%"=="1" (
    echo Warning: source frame timing is VFR/irregular. A CFR prepass will be used to avoid flashing/jumps.
)

if /I not "%field_order%"=="progressive" if /I not "%field_order%"=="unknown" (
    echo Warning: source field_order is %field_order%. Interlaced/telecine sources can flicker after FRUC.
    echo          Try: set RTX_DEINTERLACE=1
)

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

echo.

NVEncC64.exe --avhw -i "%encode_in%" -o "%out%" ^
    --codec hevc --profile main10 --output-depth 10 --output-csp yuv420 ^
    --qvbr %RTX_NVENC_QVBR% --preset %RTX_NVENC_PRESET% --multipass 2pass-full ^
    --lookahead 32 --lookahead-level 3 --aq %RTX_TEMPORAL_AQ_FLAG% --bframes %RTX_NVENC_BFRAMES% %RTX_BREF_MODE_FLAG% ^
    %RTX_DEINTERLACE_FLAG% %FRUC_FILTER_FLAG% ^
    --output-res %RTX_VSR_RES% --vpp-resize algo=ngx-vsr,vsr-quality=%RTX_VSR_QUALITY% ^
    --colormatrix auto --colorprim auto --transfer auto --colorrange auto ^
    --audio-copy --sub-copy --chapter-copy --data-copy --attachment-copy --metadata copy --video-metadata copy ^
    --avsync %RTX_AVSYNC% --output-format matroska ^
    --log-level info

set "rc=%ERRORLEVEL%"
if defined temp_norm if "%FRUC_KEEP_TEMP%"=="0" if exist "%temp_norm%" del /q "%temp_norm%" >nul 2>nul

if "%rc%"=="0" (
    echo Done: "%out%"
) else (
    echo Failed with exit code %rc%.
)

exit /b %rc%

:ResolveVsrTarget
set "__RTX_VSR_INPUT=%~1"
set "RTX_VSR_SOURCE_DISPLAY="
set "RTX_VSR_ORIENTATION="
set "RTX_VSR_ROTATION="

for /f "usebackq tokens=1,2,3,4 delims=|" %%A in (`powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $path=$env:__RTX_VSR_INPUT; $long=[int]$env:RTX_VSR_LONG_EDGE; $json=& ffprobe -v error -select_streams v:0 -show_entries stream=width,height:stream_tags=rotate:stream_side_data=rotation -of json $path | ConvertFrom-Json; if(-not $json.streams){ throw 'No video stream found.' }; $stream=$json.streams[0]; $w=[int]$stream.width; $h=[int]$stream.height; $rotation=0.0; if($stream.tags -and $stream.tags.PSObject.Properties.Name -contains 'rotate' -and $stream.tags.rotate){ $rotation=[double]$stream.tags.rotate }; if($stream.side_data_list){ foreach($sd in @($stream.side_data_list)){ if($sd.PSObject.Properties.Name -contains 'rotation' -and $null -ne $sd.rotation){ $rotation=[double]$sd.rotation; break } } }; $norm=$rotation; while($norm -lt 0){ $norm += 360 }; while($norm -ge 360){ $norm -= 360 }; $dw=$w; $dh=$h; if([math]::Round($norm) -eq 90 -or [math]::Round($norm) -eq 270){ $dw=$h; $dh=$w }; if($dw -ge $dh){ $res=('{0}x-2' -f $long); $orientation='landscape' } else { $res=('-2x{0}' -f $long); $orientation='portrait' }; '{0}|{1}x{2}|{3}|{4}' -f $res,$dw,$dh,$orientation,$rotation"`) do (
    set "RTX_VSR_RES=%%A"
    set "RTX_VSR_SOURCE_DISPLAY=%%B"
    set "RTX_VSR_ORIENTATION=%%C"
    set "RTX_VSR_ROTATION=%%D"
)

set "__RTX_VSR_INPUT="

if not defined RTX_VSR_RES (
    echo Could not determine VSR output resolution for "%~1".
    exit /b 1
)

exit /b 0
