@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Export local SDR video with RTX VSR and RTX HDR in the same NVEncC run.
rem This is simultaneous in one filter chain, not VSR then HDR as a second encode.
rem Default VSR target is 4K long edge: landscape 3840x-2, portrait -2x3840.
rem Optional 2nd arg or RTX_VSR_RES overrides the upscale target, e.g. 3840x-2.

if not defined RTX_VSR_LONG_EDGE set "RTX_VSR_LONG_EDGE=3840"
set "RTX_VSR_RES_EXPLICIT="
if defined RTX_VSR_RES set "RTX_VSR_RES_EXPLICIT=1"
if not "%~2"=="" (
    set "RTX_VSR_RES=%~2"
    set "RTX_VSR_RES_EXPLICIT=1"
)
if not defined RTX_VSR_QUALITY set "RTX_VSR_QUALITY=4"
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
    echo Usage: %~nx0 "input_sdr_video" [output_res]
    echo Default: auto 4K long edge ^(landscape 3840x-2, portrait -2x3840^)
    echo Example override: %~nx0 "input.mp4" 3840x-2
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

NVEncC64.exe --help 2>nul | findstr /I /C:"ngx-vsr" >nul
if errorlevel 1 (
    echo This NVEncC64.exe does not expose RTX VSR ^(ngx-vsr^).
    if defined PAUSE_ON_EXIT pause
    exit /b 3
)

NVEncC64.exe --help 2>nul | findstr /I /C:"--vpp-ngx-truehdr" >nul
if errorlevel 1 (
    echo This NVEncC64.exe does not expose RTX HDR / NGX TrueHDR.
    if defined PAUSE_ON_EXIT pause
    exit /b 3
)

if not defined RTX_VSR_RES_EXPLICIT (
    where ffprobe >nul 2>nul
    if errorlevel 1 (
        echo ffprobe was not found in PATH. It is required for auto orientation.
        if defined PAUSE_ON_EXIT pause
        exit /b 2
    )

    call :ResolveVsrTarget "%in%"
    if errorlevel 1 (
        if defined PAUSE_ON_EXIT pause
        exit /b 4
    )
)

for %%F in ("%in%") do (
    set "outdir=%%~dpFrtx_exports"
    set "base=%%~nF"
)

if not exist "%outdir%" mkdir "%outdir%"
set "out=%outdir%\%base%_rtx_vsr_%RTX_VSR_RES%_hdr_hevc10_hdr10.mkv"

echo.
echo [RTX VSR + RTX HDR NVEncC] "%in%"
echo Output: "%out%"
if defined RTX_VSR_RES_EXPLICIT (
    echo Target: %RTX_VSR_RES%, VSR quality %RTX_VSR_QUALITY%
) else (
    echo Target: %RTX_VSR_RES% ^(4K long edge %RTX_VSR_LONG_EDGE%, %RTX_VSR_ORIENTATION%, display %RTX_VSR_SOURCE_DISPLAY%, rotation %RTX_VSR_ROTATION%^), VSR quality %RTX_VSR_QUALITY%
)
echo TrueHDR: contrast=%RTX_HDR_CONTRAST%, saturation=%RTX_HDR_SATURATION%, middlegray=%RTX_HDR_MIDDLEGRAY%, maxluminance=%RTX_HDR_MAXLUMINANCE%
echo.

NVEncC64.exe --avhw -i "%in%" -o "%out%" ^
    --codec hevc --profile main10 --output-depth 10 --output-csp yuv420 ^
    --qvbr %RTX_NVENC_QVBR% --preset %RTX_NVENC_PRESET% --multipass 2pass-full ^
    --lookahead 32 --lookahead-level 3 --aq --aq-temporal --bframes %RTX_NVENC_BFRAMES% --bref-mode middle ^
    --output-res %RTX_VSR_RES% --vpp-resize algo=ngx-vsr,vsr-quality=%RTX_VSR_QUALITY% ^
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
