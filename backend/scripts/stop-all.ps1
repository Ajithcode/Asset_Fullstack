# Stops the services started by start-all.ps1 (MongoDB service is left running).
$LogDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'logs'
Get-ChildItem $LogDir -Filter *.pid -ErrorAction SilentlyContinue | ForEach-Object {
  $id = [int](Get-Content $_.FullName)
  Stop-Process -Id $id -Force -ErrorAction SilentlyContinue
  Write-Host "stopped $($_.BaseName) (pid $id)"
  Remove-Item $_.FullName
}
