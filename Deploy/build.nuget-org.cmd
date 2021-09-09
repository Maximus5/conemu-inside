@echo off
setlocal

rem EXPECTED TO BE CALLED AFTER build.cmd

set "PATH=%~dp0..\..\Tools\Nuget\bin;%~dp0..\..\Tools\Arch;%PATH%"
cd /d "%~dp0..\ConEmuInside\bin\Release"

if exist "ConEmu.Control.WinForms.*.nupkg" del "ConEmu.Control.WinForms.*.nupkg"
if exist "%~dp0ConEmu.Control.WinForms.*.nupkg" del "%~dp0ConEmu.Control.WinForms.*.nupkg"

call nuget pack -Verbosity detailed -NoDefaultExcludes
if errorlevel 1 (
  call cecho "Failed to create the package ConEmu.Control.WinForms for Nuget.Org."
  exit /b 100
)
call cecho /green "A package for Nuget.Org were created."

move "%~dp0..\ConEmuInside\bin\Release\ConEmu.Control.WinForms.*.nupkg" "%~dp0"
if errorlevel 1 (
  call cecho "Moving ConEmu.Control.WinForms package failed."
  exit /b 100
)

set BUILD_NO=21.9.5
7z a ConEmuInside.%BUILD_NO%.7z *.exe *.dll ConEmu.xml *.config ConEmu\*
if errorlevel 1 (
  call cecho "Failed to create the 7zip package."
  exit /b 100
)
7z a ConEmuInside.pdb.%BUILD_NO%.7z *.pdb
if errorlevel 1 (
  call cecho "Failed to create the 7zip package."
  exit /b 100
)
move "%~dp0..\ConEmuInside\bin\Release\ConEmuInside.*.7z" "%~dp0"
if errorlevel 1 (
  call cecho "Moving ConEmu.Control.WinForms package failed."
  exit /b 100
)
