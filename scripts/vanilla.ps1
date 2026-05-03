#Requires -Version 5.1
<#
.SYNOPSIS
    Removes mod to revert to vanilla Eternal Afternoon.
#>

& (Join-Path $PSScriptRoot "uninstall.ps1") -CleanTemp
