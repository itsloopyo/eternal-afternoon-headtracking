@echo off
:: ============================================
:: Eternal Afternoon - Install
:: ============================================
:: Thin wrapper - install body lives in cameraunlock-core/scripts/install-body-cecil.cmd.

:: --- CONFIG BLOCK ---
set "GAME_ID=eternal-afternoon"
set "MOD_DISPLAY_NAME=Eternal Afternoon Head Tracking"
set "MOD_DLLS=EternalAfternoonHeadTracking.dll CameraUnlock.Core.dll CameraUnlock.Core.Unity.dll Mono.Cecil.dll"
set "MOD_INTERNAL_NAME=EternalAfternoonHeadTracking"
set "MOD_VERSION=0.1.2"
set "STATE_FILE=.headtracking-state.json"
set "FRAMEWORK_TYPE=MonoCecil"
set "MANAGED_SUBFOLDER=Eternal Afternoon_Data\Managed"
set "ASSEMBLY_DLL=Assembly-CSharp.dll"
set "PATCHER_FILE=BootstrapPatcher.cs"
set "PATCH_MARKER=HeadTracking_Patched_EternalAfternoon_v1"
set "MOD_CONTROLS=Controls:&echo   End       - Toggle head tracking on/off&echo   Insert    - Toggle aim reticle on/off&echo   Page Up   - Cycle tracking mode (both / rotation only / position only)&echo   Page Down - Toggle world/local yaw"
:: --- END CONFIG BLOCK ---

set "WRAPPER_DIR=%~dp0"
set "_BODY=%WRAPPER_DIR%shared\install-body-cecil.cmd"
if not exist "%_BODY%" set "_BODY=%WRAPPER_DIR%..\cameraunlock-core\scripts\install-body-cecil.cmd"
if not exist "%_BODY%" (
    echo ERROR: install-body-cecil.cmd not found in shared\ or ..\cameraunlock-core\scripts\.
    echo If this is a release ZIP, re-download it from GitHub ^(corrupt installer^).
    echo If this is the dev tree, run: git submodule update --init --recursive
    exit /b 1
)
call "%_BODY%" %*
exit /b %errorlevel%