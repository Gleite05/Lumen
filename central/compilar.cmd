@echo off
rem Compila o Central.exe com o csc do .NET Framework 4.8 (vem com o Windows).
rem Precisa das DLL do WebView2 (Microsoft.Web.WebView2.Core.dll, .WinForms.dll e WebView2Loader.dll)
rem na pasta de saida: o pacote NuGet "Microsoft.Web.WebView2" (lib\net462 + runtimes\win-x64\native).
setlocal
set OUT=%~1
if "%OUT%"=="" set OUT=%~dp0saida
if not exist "%OUT%" mkdir "%OUT%"
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set WM=%WINDIR%\System32\WinMetadata
"%FW%\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /out:"%OUT%\Central.exe" /win32icon:"%~dp0central.ico" ^
 /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:Microsoft.CSharp.dll /r:System.Core.dll ^
 /r:"%OUT%\Microsoft.Web.WebView2.Core.dll" /r:"%OUT%\Microsoft.Web.WebView2.WinForms.dll" ^
 /r:"%WM%\Windows.Foundation.winmd" /r:"%WM%\Windows.Media.winmd" /r:"%WM%\Windows.Storage.winmd" ^
 /r:"%FW%\System.Runtime.dll" /r:"%FW%\System.Runtime.WindowsRuntime.dll" /r:"%FW%\System.Runtime.InteropServices.WindowsRuntime.dll" /r:"%FW%\System.Threading.Tasks.dll" ^
 "%~dp0Central.cs" "%~dp0Dados.cs" "%~dp0Musica.cs" "%~dp0Guarda.cs" "%~dp0Rato.cs"
