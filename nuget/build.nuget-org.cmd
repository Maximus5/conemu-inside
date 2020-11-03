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

if not exist data mkdir data
if not exist data\icon.png (
  echo Downloading icon
  ConEmuC -download https://conemu.github.io/img/logo-128.png data\icon.png 2> nul
  if errorlevel 1 exit /b 1
)
if not exist data\license.txt (
  echo Downloading license
  ConEmuC -download https://github.com/Maximus5/ConEmu/raw/master/Release/ConEmu/License.txt data\license.txt 2> nul
  if errorlevel 1 exit /b 1
)

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
