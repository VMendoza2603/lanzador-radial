@echo off
rem Genera dist\LanzadorRadial.exe con el compilador de C# que ya trae Windows (no hay que instalar nada).
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist dist mkdir dist

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:src\lanzador.ico /out:dist\LanzadorRadial.exe ^
    /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:Microsoft.VisualBasic.dll src\LanzadorRadial.cs

if errorlevel 1 (
    echo.
    echo Error al compilar.
) else (
    echo.
    echo Listo: dist\LanzadorRadial.exe
)
if not "%~1"=="--sin-pausa" pause
