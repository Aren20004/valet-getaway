@echo off
setlocal
for /f "tokens=2" %%V in ('findstr /b "m_EditorVersion:" "%~dp0ProjectSettings\ProjectVersion.txt"') do set "UNITY_VERSION=%%V"
if not defined UNITY_VERSION (
  echo Cannot find the Unity version. Keep this launcher in the project folder.
  pause
  exit /b 1
)
set "UNITY_EDITOR=%ProgramFiles%\Unity\Hub\Editor\%UNITY_VERSION%\Editor\Unity.exe"
if defined UNITY_EDITOR_PATH set "UNITY_EDITOR=%UNITY_EDITOR_PATH%"
if not exist "%UNITY_EDITOR%" (
  echo Install Unity %UNITY_VERSION% using Unity Hub, then try again.
  echo You can also add this project folder directly in Unity Hub.
  pause
  exit /b 1
)
start "" "%UNITY_EDITOR%" -projectPath "%~dp0." -openfile "%~dp0Assets\Scenes\ValetTestCity.unity"
