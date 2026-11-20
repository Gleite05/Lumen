<#
.SYNOPSIS
  Junta tudo o que o instalador do Lumen leva numa pasta (saida\pacote).

.PARAMETER Barra
  A pasta da barra já compilada com a marca Lumen (dist\Lumen do build.ps1 sobre o código de marca-lumen.ps1).
.PARAMETER Lumen
  A pasta com os programas próprios (saída do compilar.ps1).
#>
param(
    [Parameter(Mandatory)] [string]$Barra,
    [Parameter(Mandatory)] [string]$Lumen,
    [string]$Saida = (Join-Path (Split-Path $PSScriptRoot) 'saida\pacote')
)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot
if (Test-Path $Saida) { Remove-Item -Recurse -Force $Saida }
New-Item -ItemType Directory -Force $Saida | Out-Null

# 1. A barra (sem o Uninstall.ps1: assim não se regista sozinha em "Aplicações instaladas" — quem instala é o Lumen)
robocopy $Barra $Saida /E /XF Uninstall.ps1 LICENSE /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy da barra falhou" }
if (-not (Test-Path "$Saida\Lumen.exe")) { throw "Lumen.exe (barra) em falta em $Barra" }

# 2. Os programas do Lumen e as páginas
robocopy $Lumen $Saida /E /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy do Lumen falhou" }

# 3. Fundos dos temas, cursores, ícones e definições de origem
New-Item -ItemType Directory -Force "$Saida\fundos", "$Saida\cursores", "$Saida\predefinicoes" | Out-Null
Get-ChildItem "$raiz\fundos\*-3840x2400.png" | ForEach-Object { Copy-Item $_.FullName "$Saida\fundos\$($_.Name -replace '-3840x2400','')" }
Copy-Item "$raiz\cursores\Fluent-Escuro", "$raiz\cursores\Fluent-Claro" "$Saida\cursores" -Recurse
Copy-Item "$raiz\marca\lumen.ico" "$Saida\lumen.ico"
Copy-Item "$raiz\marca\lumen-256.png" "$Saida\lumen.png"
Copy-Item "$raiz\classico\windows-classico.ico" "$Saida\windows-classico.ico"
Copy-Item "$raiz\barra\settings.json", "$raiz\barra\widgets.json" "$Saida\predefinicoes"

# 4. Licenças: a do Lumen e a do Atrium Desktop (MIT — tem de ir com a barra)
Copy-Item "$raiz\LICENSE" "$Saida\LICENSE.txt"
$atrium = Get-Content "$raiz\atrium-desktop\LICENSE" -Raw
$notas = Get-Content "$raiz\THIRD-PARTY-NOTICES.md" -Raw
Set-Content "$Saida\THIRD-PARTY-NOTICES.txt" ($notas + "`r`n`r`n----- Atrium Desktop -----`r`n`r`n" + $atrium) -Encoding utf8

$n = (Get-ChildItem $Saida -Recurse -File).Count
$mb = [math]::Round(((Get-ChildItem $Saida -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host "pacote: $n ficheiros, $mb MB em $Saida"
