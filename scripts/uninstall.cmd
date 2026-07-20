@echo off
:: ============================================
:: Eternal Afternoon - Uninstall
:: ============================================
:: Thin wrapper - uninstall body lives in cameraunlock-core/scripts/uninstall-body.cmd
:: (one body, framework-aware via FRAMEWORK_TYPE).

:: --- CONFIG BLOCK ---
set "GAME_ID=eternal-afternoon"
set "MOD_DISPLAY_NAME=Eternal Afternoon Head Tracking"
set "MOD_DLLS=EternalAfternoonHeadTracking.dll CameraUnlock.Core.dll CameraUnlock.Core.Unity.dll Mono.Cecil.dll"
set "MOD_INTERNAL_NAME=EternalAfternoonHeadTracking"
set "STATE_FILE=.headtracking-state.json"
set "FRAMEWORK_TYPE=MonoCecil"
set "LEGACY_DLLS="

:: --- Loader-specific config (leave the ones that don't apply blank) ---
:: MonoCecil: used to find + restore the original Assembly-CSharp.dll.
set "MANAGED_SUBFOLDER=Eternal Afternoon_Data\Managed"
set "ASSEMBLY_DLL=Assembly-CSharp.dll"
set "PATCH_MARKER=HeadTracking_Patched_EternalAfternoon_v1"
:: MonoCecil: extra files to also remove from MANAGED_SUBFOLDER (config/log
:: files left behind by the mod itself).
set "MANAGED_EXTRAS=HeadTracking.cfg HeadTracking.log HeadTracking_BOOT.log HeadTracking.manifest.json"
:: ASILoader: filename the ASI DLL was renamed to. Defaults to winmm.dll.
set "ASI_LOADER_NAME=winmm.dll"
:: --- END CONFIG BLOCK ---

set "WRAPPER_DIR=%~dp0"
set "_BODY=%WRAPPER_DIR%shared\uninstall-body.cmd"
if not exist "%_BODY%" set "_BODY=%WRAPPER_DIR%..\cameraunlock-core\scripts\uninstall-body.cmd"
if not exist "%_BODY%" (
    echo ERROR: uninstall-body.cmd not found in shared\ or ..\cameraunlock-core\scripts\.
    echo If this is a release ZIP, re-download it from GitHub ^(corrupt installer^).
    echo If this is the dev tree, run: git submodule update --init --recursive
    exit /b 1
)
call "%_BODY%" %*
exit /b %errorlevel%