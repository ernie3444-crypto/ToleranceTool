<#
.SYNOPSIS
  Reverses Install-ToleranceTool.ps1: unregisters the add-in and removes the copied
  .xll. Leaves your %APPDATA%\ToleranceTool configuration untouched.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$officeRoot = 'HKCU:\Software\Microsoft\Office'
$versions = Get-ChildItem $officeRoot -ErrorAction SilentlyContinue |
    Where-Object { $_.PSChildName -match '^\d+\.\d+$' -and (Test-Path (Join-Path $_.PSPath 'Excel\Options')) }

foreach ($v in $versions) {
    $optionsKey = Join-Path $v.PSPath 'Excel\Options'
    $item = Get-Item -Path $optionsKey
    foreach ($n in ($item.Property | Where-Object { $_ -match '^OPEN\d*$' })) {
        if ((Get-ItemProperty -Path $optionsKey -Name $n).$n -like "*ToleranceTool64-packed.xll*") {
            Remove-ItemProperty -Path $optionsKey -Name $n
            Write-Host "Excel $($v.PSChildName): removed $n"
        }
    }
}

$target = Join-Path $env:LOCALAPPDATA 'ToleranceTool\bin\ToleranceTool64-packed.xll'
if (Test-Path $target) {
    Remove-Item $target -Force
    Write-Host "Removed: $target"
}

Write-Host "`nDone. Restart Excel. Configuration in %APPDATA%\ToleranceTool was left in place."
