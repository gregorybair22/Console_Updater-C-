@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "RARFILE=net7.0-windows.rar"
set "DESTFOLDER=maquinasdispensadorasnuevosoftware"
set "TARGETROOT=secur"
set "APPSETTINGS=appsettings.json"

REM === Carpeta dentro del RAR (según tu captura) ===
set "INNER_BASE=net7.0-windows"

REM ===== Fecha/hora (PowerShell) =====
for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "Get-Date -Format 'yyyyMMdd_HHmm'"`) do set "STAMP=%%I"
if not defined STAMP call :fail "No pude obtener fecha/hora del sistema (PowerShell)."

for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "Get-Date -Format 'yyyyMMdd_HHmmss'"`) do set "STAMP2=%%I"
if not defined STAMP2 call :fail "No pude obtener fecha/hora del sistema (PowerShell)."

set "TMP=_unpacked_tmp_%STAMP2%"

echo [INFO] Sello detectado: %STAMP%

REM ===== Comprobaciones =====
if not exist "%RARFILE%" call :fail "No existe %RARFILE% en %CD%"

if not exist "%TARGETROOT%" (
  echo [INFO] Creando carpeta "%TARGETROOT%"...
  mkdir "%TARGETROOT%" >nul 2>&1
  if errorlevel 1 call :fail "No pude crear %TARGETROOT%"
)

REM ===== Localizar extractor rar/unrar =====
set "EXE="
for %%X in (rar.exe unrar.exe) do (
  where %%X >nul 2>&1 && (set "EXE=%%X" & goto :foundexe)
)

if exist "%ProgramFiles%\WinRAR\UnRAR.exe" set "EXE=%ProgramFiles%\WinRAR\UnRAR.exe"
if not defined EXE if exist "%ProgramFiles%\WinRAR\rar.exe" set "EXE=%ProgramFiles%\WinRAR\rar.exe"
if not defined EXE if exist "%ProgramFiles(x86)%\WinRAR\UnRAR.exe" set "EXE=%ProgramFiles(x86)%\WinRAR\UnRAR.exe"
if not defined EXE if exist "%ProgramFiles(x86)%\WinRAR\rar.exe" set "EXE=%ProgramFiles(x86)%\WinRAR\rar.exe"

:foundexe
if not defined EXE call :fail "No encontre rar.exe/unrar.exe. Instala WinRAR o anade rar/unrar al PATH."

echo [INFO] Usando extractor: "%EXE%"

REM ===== Validar appsettings origen (de la instalación actual) =====
if not exist "%DESTFOLDER%\%APPSETTINGS%" call :fail "No existe %DESTFOLDER%\%APPSETTINGS% (origen) en %CD%"

REM ===== Crear TMP único =====
echo [INFO] Creando tmp: "%TMP%"
mkdir "%TMP%" >nul 2>&1
if errorlevel 1 call :fail "No pude crear %TMP%"

REM ===== Descomprimir =====
echo [INFO] Descomprimiendo "%RARFILE%" en "%TMP%"...
"%EXE%" x -o+ "%RARFILE%" "%TMP%\" >nul 2>&1
if errorlevel 1 call :fail "Fallo al descomprimir %RARFILE%"

REM ===== Comprobar carpeta interna net7.0-windows =====
if not exist "%TMP%\%INNER_BASE%\" (
  call :cleanup_tmp
  call :fail "Dentro del rar no existe la carpeta %INNER_BASE%."
)

REM ===== Copiar appsettings a la carpeta que se desplegará =====
copy /y "%DESTFOLDER%\%APPSETTINGS%" "%TMP%\%INNER_BASE%\" >nul 2>&1
if errorlevel 1 (
  call :cleanup_tmp
  call :fail "Fallo al copiar %APPSETTINGS% a %TMP%\%INNER_BASE%"
)

REM ===== Archivar la carpeta actual a secur =====
if exist "%DESTFOLDER%" (
  set "ARCHIVED=%TARGETROOT%\%DESTFOLDER%_%STAMP%"
  if exist "!ARCHIVED!" (
    call :cleanup_tmp
    call :fail "Ya existe !ARCHIVED!. No sobreescribo."
  )

  echo [INFO] Archivando actual: "%DESTFOLDER%" -> "!ARCHIVED!"...
  move "%DESTFOLDER%" "!ARCHIVED!" >nul 2>&1
  if errorlevel 1 (
    call :cleanup_tmp
    call :fail "Fallo al mover %DESTFOLDER% a !ARCHIVED!"
  )
) else (
  echo [WARN] No existe "%DESTFOLDER%" en raiz. No hay nada que archivar.
)

REM ===== Desplegar: net7.0-windows -> maquinasdispensadorasnuevosoftware =====
echo [INFO] Desplegando NUEVA carpeta: "%INNER_BASE%" -> "%DESTFOLDER%"...
move "%TMP%\%INNER_BASE%" "%DESTFOLDER%" >nul 2>&1
if errorlevel 1 (
  call :cleanup_tmp
  call :fail "Fallo al mover %TMP%\%INNER_BASE% a .\%DESTFOLDER%"
)

REM ===== Limpiar TMP (no fatal si falla) =====
call :cleanup_tmp

echo.
echo [OK] Proceso completado correctamente.
echo      - NUEVA carpeta en raiz: "%CD%\%DESTFOLDER%"
echo      - appsettings copiado a: "%CD%\%DESTFOLDER%\%APPSETTINGS%"
if defined ARCHIVED echo      - ANTIGUA archivada en: "%CD%\!ARCHIVED!"
echo.
pause
exit /b 0

:cleanup_tmp
if exist "%TMP%" (
  rmdir /s /q "%TMP%" >nul 2>&1
  if exist "%TMP%" (
    echo [WARN] No pude borrar el tmp "%TMP%". (No es grave; puede estar en uso)
  )
)
exit /b 0

:fail
echo.
echo [ERROR] %~1
echo.
pause
exit /b 1
