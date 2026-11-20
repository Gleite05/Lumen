@echo off
rem Compila VpnPainel.exe com o csc do .NET Framework (vem com o Windows)
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /out:"%~dp0VpnPainel.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /win32icon:"%~dp0vpn.ico" "%~dp0VpnPainel.cs"