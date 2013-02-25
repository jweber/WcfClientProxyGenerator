@echo off

echo Compiling
call rake compile
IF NOT %ERRORLEVEL% == 0 goto FAILED

:FAILED
