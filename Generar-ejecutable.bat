@echo off
chcp 65001 >nul
title Generando PokeBank.exe
cd /d "%~dp0"

echo.
echo  Generando PokeBank.exe (puede tardar uno o dos minutos la primera vez)...
echo.

dotnet publish "src\PokeBank.App\PokeBank.App.csproj" -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none ^
  -o "%~dp0Publicado"

if errorlevel 1 (
  echo.
  echo  Hubo un error al generar el ejecutable. Revisa los mensajes de arriba.
  pause
  exit /b 1
)

echo.
echo  Creando acceso directo en el escritorio...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$exe = Join-Path '%~dp0Publicado' 'PokeBank.exe';" ^
  "$lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'PokeBank.lnk';" ^
  "$s = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk);" ^
  "$s.TargetPath = $exe; $s.WorkingDirectory = (Split-Path $exe); $s.IconLocation = $exe + ',0'; $s.Description = 'PokeBank'; $s.Save()"

echo.
echo  Listo. El programa quedo en:  %~dp0Publicado\PokeBank.exe
echo  y tienes un acceso directo "PokeBank" en el escritorio.
echo.
start "" "%~dp0Publicado"
pause
