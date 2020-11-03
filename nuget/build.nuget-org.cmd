@echo off
cd /d "%~dp0"

rem EXPECTED TO BE CALLED AFTER build.cmd
rem The version in the package is updated by
rem build.cmd -> UpdatePackageVersions.ps1

setlocal
set "PATH=%~dp0..\..\Tools\Nuget\bin;%PATH%"
rem ??? what would be the path to Nuget.exe cmdline tool?..

if exist "ConEmu.Control.WinForms.*.nupkg" del "ConEmu.Control.WinForms.*.nupkg"
if exist "ConEmu.Control.WinForms\*.nupkg" del "ConEmu.Control.WinForms\*.nupkg"

cd /d ConEmu.Control.WinForms
call nuget pack -Verbosity detailed -NoDefaultExcludes
if errorlevel 1 (
  call cecho "Failed to create the package ConEmu.Control.WinForms for Nuget.Org."
  exit /b 100
)
call cecho /green "A package for Nuget.Org were created."

move "%~dp0ConEmu.Control.WinForms\ConEmu.Control.WinForms.*.nupkg" "%~dp0"
if errorlevel 1 (
  call cecho "Moving ConEmu.Control.WinForms package failed."
  exit /b 100
)
