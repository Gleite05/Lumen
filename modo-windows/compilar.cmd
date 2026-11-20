@echo off
rem Compila o ModoWindows.exe com o csc do .NET Framework (vem com o Windows).
set OUT=%~1
if "%OUT%"=="" set OUT=%~dp0
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /out:"%OUT%\ModoWindows.exe" /win32icon:"%~dp0windows-original.ico" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:Microsoft.CSharp.dll /r:System.Core.dll "%~dp0ModoWindows.cs"
