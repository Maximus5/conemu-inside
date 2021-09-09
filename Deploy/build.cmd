@echo off
setlocal

if "%~1" == "" (
  echo Usage: build.cmd ^<BUILD_NO^>
  exit /b 1
)
set CurVerBuild=%~1

powershell -noprofile -ExecutionPolicy RemoteSigned -command "%~dp0UpdatePackageVersions.ps1" %CurVerBuild%
if errorlevel 1 exit /b 1

call "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\Common7\Tools\VsDevCmd.bat"
if errorlevel 1 exit /b 1

cd /d "%~dp0.."
msbuild ConEmuInside.sln -t:Rebuild -p:"Configuration=Release";"Platform=Any CPU"
if errorlevel 1 exit /b 1

cd /d "%~dp0..\ConEmuInside\bin\Release"

rem if not exist "%~dp0sign.cmd" (
rem   echo sign.cmd not found, skipping
rem   goto skip_sign
rem )
rem call "%~dp0sign.cmd" ConEmu.WinForms.dll ConEmuInside.exe
rem :skip_sign
