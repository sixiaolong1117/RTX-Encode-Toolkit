@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem Auto-levels / auto-contrast for images.
rem Uses ffmpeg to stretch the histogram to full range (0-255),
rem which is equivalent to "Auto Levels" / "Auto Contrast" in Photoshop.
rem The effect is: find the darkest and brightest pixels in the image,
rem then remap them to 0 and 255 respectively, stretching everything in between.
rem
rem Usage: auto_levels.bat "input_image1" ["input_image2" ...]
rem   Drag multiple images onto this script, or pass paths as arguments.
rem   Output images are saved to an "auto_levels_output" subfolder next to
rem   each input image, with "_auto_levels" appended to the filename.

rem Always pause on exit so the window stays open after drag-and-drop.
set "PAUSE_ON_EXIT=1"

if "%~1"=="" (
    echo Usage: %~nx0 "input_image1" ["input_image2" ...]
    echo   Drag multiple images onto this script to auto-level them.
    echo   Output goes to an "auto_levels_output" subfolder.
    set /p "in=Input image path: "
    set "args="
) else (
    set "args=%*"
)

where ffmpeg >nul 2>nul
if errorlevel 1 (
    echo ffmpeg was not found in PATH.
    if defined PAUSE_ON_EXIT pause
    exit /b 2
)

rem If only one image was entered interactively, process it.
if not defined args (
    if defined in (
        set "args=!in!"
    )
)

if not defined args (
    echo No input files specified.
    if defined PAUSE_ON_EXIT pause
    exit /b 1
)

set "total=0"
set "success=0"
set "failed=0"

for %%F in (%args%) do (
    set "input=%%~F"
    set "input=!input:"=!

    if not exist "!input!" (
        echo [SKIP] Not found: "!input!"
        set /a "failed+=1"
        set /a "total+=1"
    ) else (
        rem Determine output path: auto_levels_output subfolder next to input
        for %%I in ("!input!") do (
            set "indir=%%~dpI"
            set "name=%%~nI"
            set "ext=%%~xI"
        )
        set "outdir=!indir!auto_levels_output"
        if not exist "!outdir!" mkdir "!outdir!"
        set "output=!outdir!\!name!_auto_levels!ext!"

        echo.
        echo [Auto Levels] "!input!"
        echo Output: "!output!"
        echo.

        ffmpeg -hide_banner -y -i "!input!" ^
            -vf "normalize=blackpt=black:whitept=white:independence=1:strength=1.0" ^
            "!output!"

        set "rc=!ERRORLEVEL!"
        if "!rc!"=="0" (
            echo Done: "!output!"
            set /a "success+=1"
        ) else (
            echo Failed with exit code !rc!.
            set /a "failed+=1"
        )
        set /a "total+=1"
    )
)

echo.
echo ===== Summary =====
echo Total: !total!  Success: !success!  Failed: !failed!
echo.

if defined PAUSE_ON_EXIT pause
if "!failed!"=="0" (exit /b 0) else (exit /b 1)
