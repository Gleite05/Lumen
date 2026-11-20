# Windows 2.0 — instalador (sem administrador)
#   powershell -ExecutionPolicy Bypass -File .\instalar.ps1 [-Fundo esmeralda] [-Cursores escuro] [-SemDefinicoes] [-SemCursores] [-SemFundo]
param(
    [ValidateSet('esmeralda', 'oceano', 'glaciar', 'indigo', 'ambar', 'grafite')] [string]$Fundo = 'esmeralda',
    [ValidateSet('escuro', 'claro')] [string]$Cursores = 'escuro',
    [switch]$SemDefinicoes,
    [switch]$SemCursores,
    [switch]$SemFundo,
    [switch]$SemCentral
)
$ErrorActionPreference = 'Stop'
$aqui = $PSScriptRoot
$utf8 = New-Object Text.UTF8Encoding $false
function Passo($t) { Write-Host "`n» $t" -ForegroundColor Cyan }

Add-Type -Namespace W2 -Name Api -MemberDefinition @'
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool SystemParametersInfo(uint a, uint b, string c, uint d);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageTimeout(IntPtr h, uint m, UIntPtr w, string l, uint f, uint t, out UIntPtr r);
'@

# 1. Atrium modificado -------------------------------------------------------------
Passo 'Atrium modificado'
$atriumDir = Join-Path $env:LOCALAPPDATA 'Programs\Atrium\Atrium'
$atriumExe = Join-Path $atriumDir 'Atrium.exe'
if (-not (Test-Path $atriumExe)) {
    Write-Host 'O Atrium oficial não está instalado. Instala primeiro o 2.7.0:' -ForegroundColor Yellow
    Write-Host '  https://github.com/wynter-yaxley/atrium-desktop/releases'
    exit 1
}
$novo = Join-Path $env:TEMP 'Atrium-Windows2.exe'
Invoke-WebRequest 'https://github.com/Gleite05/Windows-2.0/releases/latest/download/Atrium.exe' -OutFile $novo -UseBasicParsing
& $atriumExe --exit
Start-Sleep -Seconds 3
Get-Process Atrium -ErrorAction SilentlyContinue | Stop-Process -Force
$original = Join-Path $atriumDir 'Atrium-original.exe'
if (-not (Test-Path $original)) { Copy-Item $atriumExe $original }
Copy-Item $novo $atriumExe -Force
Write-Host "  Atrium.exe trocado (original guardado em Atrium-original.exe)"

# 2. Definições do Atrium -----------------------------------------------------------
if (-not $SemDefinicoes) {
    Passo 'Definições do Atrium'
    $cfg = Join-Path $env:LOCALAPPDATA 'Atrium'
    New-Item -ItemType Directory -Force $cfg | Out-Null
    $copia = Join-Path $cfg ("antes-windows2-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Force $copia | Out-Null
    foreach ($f in 'settings.json', 'widgets.json') {
        $atual = Join-Path $cfg $f
        if (Test-Path $atual) { Copy-Item $atual $copia }
    }
    # mantém as apps que já tinhas afixadas
    $s = Get-Content (Join-Path $aqui 'atrium\settings.json') -Raw | ConvertFrom-Json
    $atual = Join-Path $cfg 'settings.json'
    if (Test-Path $atual) {
        $velho = Get-Content $atual -Raw | ConvertFrom-Json
        if ($velho.pins) { $s.pins = $velho.pins }
    }
    [IO.File]::WriteAllText($atual, ($s | ConvertTo-Json -Depth 10), $utf8)
    Copy-Item (Join-Path $aqui 'atrium\widgets.json') (Join-Path $cfg 'widgets.json') -Force
    Write-Host "  Copiadas (as tuas antigas estão em $copia)"
}

# 3. BarraOculta --------------------------------------------------------------------
Passo 'BarraOculta (Ctrl+Alt+B mostra a barra do Windows)'
$extras = Join-Path $env:LOCALAPPDATA 'Programs\Atrium-extras'
New-Item -ItemType Directory -Force $extras | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$barra = Join-Path $extras 'BarraOculta.exe'
Get-Process BarraOculta -ErrorAction SilentlyContinue | Stop-Process -Force
& $csc /nologo /target:winexe /optimize+ "/out:$barra" /r:System.Windows.Forms.dll /r:System.Drawing.dll (Join-Path $aqui 'barra-oculta\BarraOculta.cs')
if ($LASTEXITCODE -ne 0) { throw 'A compilação da BarraOculta falhou.' }
Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name BarraOculta -Value "`"$barra`""
Start-Process $barra

# 3b. Central (fundo-central de controlo) -------------------------------------------
if (-not $SemCentral) {
    Passo 'Central (fundo com as horas de trabalho + painéis)'
    $central = Join-Path $env:USERPROFILE '.central'
    New-Item -ItemType Directory -Force $central | Out-Null
    & (Join-Path $central 'Central.exe') --sair 2>$null
    Start-Sleep -Seconds 2
    Get-Process Central -ErrorAction SilentlyContinue | Stop-Process -Force
    $wv = Join-Path $env:TEMP 'webview2-nupkg'
    Remove-Item $wv -Recurse -Force -ErrorAction SilentlyContinue
    Invoke-WebRequest 'https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2' -OutFile "$wv.zip" -UseBasicParsing
    Expand-Archive "$wv.zip" $wv -Force
    $lib = Get-ChildItem $wv -Recurse -Filter 'Microsoft.Web.WebView2.Core.dll' | Where-Object { $_.FullName -match '\\lib\\net4' } | Select-Object -First 1
    Copy-Item (Join-Path $lib.DirectoryName 'Microsoft.Web.WebView2.Core.dll'), (Join-Path $lib.DirectoryName 'Microsoft.Web.WebView2.WinForms.dll') $central -Force
    Copy-Item (Join-Path $wv 'runtimes\win-x64\native\WebView2Loader.dll') $central -Force
    cmd /c "`"$(Join-Path $aqui 'central\compilar.cmd')`" `"$central`""
    if ($LASTEXITCODE -ne 0) { throw 'A compilação do Central falhou.' }
    New-Item -ItemType Directory -Force (Join-Path $central 'web') | Out-Null
    Copy-Item (Join-Path $aqui 'central\web\*') (Join-Path $central 'web') -Force
    if (-not (Test-Path (Join-Path $central 'config.json'))) { Copy-Item (Join-Path $aqui 'central\config.exemplo.json') (Join-Path $central 'config.json') }
    Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name Central -Value "`"$(Join-Path $central 'Central.exe')`""
    Start-Process (Join-Path $central 'Central.exe')
    Write-Host "  Edita $central\config.json (horário, servidores, atalhos) e escolhe Recarregar no ícone do Central."
}

# 3c. Interruptor "Modo Windows original" ------------------------------------------
Passo 'Atalho "Modo Windows original" no ambiente de trabalho'
$central = Join-Path $env:USERPROFILE '.central'
New-Item -ItemType Directory -Force $central | Out-Null
cmd /c "`"$(Join-Path $aqui 'modo-windows\compilar.cmd')`" `"$central`""
if ($LASTEXITCODE -ne 0) { throw 'A compilação do ModoWindows falhou.' }
Copy-Item (Join-Path $aqui 'modo-windows\windows-original.ico'), (Join-Path $aqui 'modo-windows\windows2.ico') $central -Force
$desk = [Environment]::GetFolderPath('DesktopDirectory')
Remove-Item (Join-Path $desk 'Modo Windows 2.0.lnk') -ErrorAction SilentlyContinue
$lnk = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $desk 'Modo Windows original.lnk'))
$lnk.TargetPath = Join-Path $central 'ModoWindows.exe'; $lnk.WorkingDirectory = $central
$lnk.IconLocation = (Join-Path $central 'windows-original.ico') + ',0'; $lnk.Description = 'Volta ao Windows de origem'; $lnk.Save()

# 4. Ajustes ------------------------------------------------------------------------
Passo 'Sem intermitência na barra do Windows'
Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name TaskbarFlashing -Value 0 -Type DWord
[UIntPtr]$r = [UIntPtr]::Zero
[W2.Api]::SendMessageTimeout([IntPtr]0xffff, 0x1A, [UIntPtr]::Zero, 'TraySettings', 2, 2000, [ref]$r) | Out-Null

# 5. Cursores -----------------------------------------------------------------------
if (-not $SemCursores) {
    $nome = if ($Cursores -eq 'claro') { 'Fluent-Claro' } else { 'Fluent-Escuro' }
    Passo "Cursores $nome"
    $dest = Join-Path $env:LOCALAPPDATA "Microsoft\Windows\Cursors\$nome"
    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item (Join-Path $aqui "cursores\$nome\*") $dest -Force
    $k = 'HKCU:\Control Panel\Cursors'
    foreach ($f in Get-ChildItem $dest) {
        Set-ItemProperty $k -Name $f.BaseName -Value $f.FullName -Type ExpandString
    }
    Set-ItemProperty $k -Name '(default)' -Value ($nome -replace '-', ' ')
    [W2.Api]::SystemParametersInfo(0x57, 0, $null, 0) | Out-Null   # SPI_SETCURSORS
}

# 6. Fundo --------------------------------------------------------------------------
if (-not $SemFundo -and $SemCentral) {   # com o Central, o fundo é dele
    Passo "Fundo $Fundo"
    $pasta = Join-Path ([Environment]::GetFolderPath('MyPictures')) 'Wallpapers\Vetoriais'
    New-Item -ItemType Directory -Force $pasta | Out-Null
    Copy-Item (Join-Path $aqui 'fundos\*.png') $pasta -Force
    $img = Join-Path $pasta "$Fundo-3840x2400.png"
    Set-ItemProperty 'HKCU:\Control Panel\Desktop' -Name WallpaperStyle -Value '10'
    Set-ItemProperty 'HKCU:\Control Panel\Desktop' -Name TileWallpaper -Value '0'
    [W2.Api]::SystemParametersInfo(0x14, 0, $img, 3) | Out-Null    # SPI_SETDESKWALLPAPER
}

# 7. Arranque do Atrium depois do Explorer ------------------------------------------
Passo 'Arranque'
Start-Process $atriumExe
Start-Sleep -Seconds 5
$tarefa = Get-ScheduledTask -TaskName 'Atrium Startup-*' -ErrorAction SilentlyContinue | Select-Object -First 1
if ($tarefa) {
    $tarefa.Triggers[0].Delay = 'PT15S'
    Set-ScheduledTask -InputObject $tarefa | Out-Null
    Write-Host '  Tarefa de arranque com 15 s de atraso'
} else {
    Write-Host '  Liga "Arrancar com o Windows" nas definições do Atrium.' -ForegroundColor Yellow
}

Write-Host "`nFeito. Win = Iniciar do Atrium · Ctrl+Alt+B = barra do Windows" -ForegroundColor Green
