setlocal
set "PATH=%~dp0..\..\Tools\Nuget\bin;%PATH%"
cd /d "%~dp0"
if "%~1" == "" (
  for %%g in (ConEmu.Control.WinForms.*.nupkg) do (
    call ..\..\Tools\Uploaders\ConEmuUploadNuget.cmd %%g -Source https://www.nuget.org/api/v2/package
  )
) else (
  call ..\..\Tools\Uploaders\ConEmuUploadNuget.cmd %* -Source https://www.nuget.org/api/v2/package
)
