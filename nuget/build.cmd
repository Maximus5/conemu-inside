@echo off
setlocal

call "%ConEmuDrive%\Program Files (x86)\Microsoft Visual Studio\2019\Professional\VC\Auxiliary\Build\vcvarsall.bat" x64
if errorlevel 1 exit /b 1

cd /d "%~dp0.."
msbuild ConEmuInside.sln -t:Rebuild -p:"Configuration=Release";"Platform=Any CPU"
if errorlevel 1 exit /b 1

cd /d "%~dp0..\ConEmuInside\bin\Release"

if not exist "%~dp0sign.cmd" (
  echo sign.cmd not found, skipping
  goto skip_sign
)
call "%~dp0sign.cmd" ConEmu.WinForms.dll ConEmuInside.exe
:skip_sign
