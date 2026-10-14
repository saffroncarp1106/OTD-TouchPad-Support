@echo off
cd /d "%~dp0"
py -3 "%~dp0viewer\touchpad_viewer.py"
if errorlevel 1 pause
