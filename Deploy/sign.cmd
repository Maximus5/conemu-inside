@echo off

setlocal

if exist "%~dp0user_env.cmd" (
  call "%~dp0user_env.cmd"
)

if exist "%CONEMU_DEPLOY%sign_any.bat" (
  call "%CONEMU_DEPLOY%sign_any.bat" %*
)

endlocal
