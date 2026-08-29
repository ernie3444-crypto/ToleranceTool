<#
.SYNOPSIS
  Per-user install for the Tolerance Tool Excel add-in. No admin rights required.

.DESCRIPTION
  Copies the packed .xll to %LOCALAPPDATA%\ToleranceTool\bin, seeds the starter
  configuration libraries into %APPDATA%\ToleranceTool (without overwriting existing
  ones), and registers the add-in with every per-user Excel install it finds.

  Run Uninstall-ToleranceTool.ps1 to reverse the registration.

.PARAMETER Xll
  Path to ToleranceTool64-packed.xll. Defaults to the copy beside this script,
  then to ..\src\ToleranceTool.AddIn\bin\Release\net48\publish.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\Install-ToleranceTool.ps1
#>
[CmdletBinding()]
param(
    [string]$Xll
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

function Resolve-Xll {
    param([string]$Hint)
    $candidates = @(
        $Hint,
        (Join-Path $root 'ToleranceTool64-packed.xll'),
        (Join-Path $root '..\src\ToleranceTool.AddIn\bin\Release\net48\publish\ToleranceTool64-packed.xll'),
        (Join-Path $root '..\src\ToleranceTool.AddIn\bin\Debug\net48\publish\ToleranceTool64-packed.xll')
    ) | Where-Object { $_ }
    foreach ($c in $candidates) {
        if (Test-Path $c) { return (Resolve-Path $c).Path }
    }
    throw "Could not find ToleranceTool64-packed.xll. Build in Release, or pass -Xll <path>."
}

$source = Resolve-Xll -Hint $Xll
Write-Host "Add-in:  $source"

# 1. Copy the .xll to a stable per-user location.
$binDir = Join-Path $env:LOCALAPPDATA 'ToleranceTool\bin'
New-Item -ItemType Directory -Force -Path $binDir | Out-Null
$target = Join-Path $binDir 'ToleranceTool64-packed.xll'
Copy-Item -Path $source -Destination $target -Force
Write-Host "Copied:  $target"

# 2. Seed starter configuration (never overwrite the user's own files).
$configDir = Join-Path $env:APPDATA 'ToleranceTool'
New-Item -ItemType Directory -Force -Path $configDir | Out-Null
$starter = Join-Path $root 'config'
if (Test-Path $starter) {
    Get-ChildItem -Path $starter -Filter *.xml | ForEach-Object {
        $dest = Join-Path $configDir $_.Name
        if (Test-Path $dest) {
            Write-Host "Kept:    $dest (already present)"
        } else {
            Copy-Item $_.FullName $dest
            Write-Host "Seeded:  $dest"
        }
    }
}

# 3. Register with each per-user Excel install.
$officeRoot = 'HKCU:\Software\Microsoft\Office'
$versions = Get-ChildItem $officeRoot -ErrorAction SilentlyContinue |
    Where-Object { $_.PSChildName -match '^\d+\.\d+$' -and (Test-Path (Join-Path $_.PSPath 'Excel')) }

if (-not $versions) {
    Write-Warning "No per-user Excel registration key found. Add the .xll manually via File > Options > Add-ins > Manage: Excel Add-ins > Browse:`n  $target"
    return
}

$openValue = "/R ""$target"""
foreach ($v in $versions) {
    $optionsKey = Join-Path $v.PSPath 'Excel\Options'
    New-Item -Path $optionsKey -Force | Out-Null

    $existing = Get-Item -Path $optionsKey
    $names = $existing.Property | Where-Object { $_ -match '^OPEN\d*$' }

    $already = $false
    foreach ($n in $names) {
        if ((Get-ItemProperty -Path $optionsKey -Name $n).$n -like "*ToleranceTool64-packed.xll*") { $already = $true; $slot = $n }
    }

    if ($already) {
        Set-ItemProperty -Path $optionsKey -Name $slot -Value $openValue
        Write-Host "Excel $($v.PSChildName): updated $slot"
    } else {
        $used = $names | ForEach-Object { if ($_ -eq 'OPEN') { 0 } else { [int]($_ -replace 'OPEN','') } }
        $next = 0
        while ($used -contains $next) { $next++ }
        $name = if ($next -eq 0) { 'OPEN' } else { "OPEN$next" }
        New-ItemProperty -Path $optionsKey -Name $name -Value $openValue -PropertyType String -Force | Out-Null
        Write-Host "Excel $($v.PSChildName): registered as $name"
    }
}

Write-Host "`nDone. Restart Excel; the 'Tolerance Tool' ribbon tab should appear."
