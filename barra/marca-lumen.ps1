<#
.SYNOPSIS
  Dá a marca Lumen ao código da barra (fork do Atrium Desktop, MIT) antes de o compilar.

.DESCRIPTION
  O fork segue o Atrium original todas as semanas sem alterações de marca (assim a fusão
  não tem conflitos). Este script corre na compilação, sobre uma CÓPIA do código:
    - "Atrium" -> "Lumen" em todos os ficheiros de texto e nos nomes de ficheiros/pastas
      (executáveis, janelas, menus, traduções, pasta de dados %LOCALAPPDATA%\Lumen, tarefa de arranque…);
    - atualizações e "Sobre" apontam para o repositório do Lumen;
    - ícones do Lumen no lugar dos do Atrium;
    - versão = a do Lumen.
  Os nomes internos em minúsculas (namespace atrium, alvos do CMake) ficam: não se veem.
  A licença MIT original é copiada para o pacote (THIRD-PARTY-NOTICES), como a licença exige.

.EXAMPLE
  ./marca-lumen.ps1 -Fonte ../atrium-desktop -Destino ../build/barra -Versao 1.0.0 -Marca ../marca
#>
param(
    [Parameter(Mandatory)] [string]$Fonte,
    [Parameter(Mandatory)] [string]$Destino,
    [Parameter(Mandatory)] [string]$Versao,
    [Parameter(Mandatory)] [string]$Marca,
    [string]$Repo = 'Gleite05/Lumen',
    [string]$Autor = 'Gonçalo Leite'
)
$ErrorActionPreference = 'Stop'
$Fonte = (Resolve-Path $Fonte).Path
if (Test-Path $Destino) { Remove-Item -Recurse -Force $Destino }
New-Item -ItemType Directory -Force $Destino | Out-Null
$Destino = (Resolve-Path $Destino).Path

# 1. Cópia sem .git nem compilações anteriores.
robocopy $Fonte $Destino /E /XD .git build dist bin obj /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy falhou ($LASTEXITCODE)" }

# 2. Texto: substituições específicas primeiro, depois a marca.
$textos = '.cpp', '.h', '.hlsl', '.rc', '.manifest', '.txt', '.json', '.cs', '.xaml', '.csproj', '.props', '.targets', '.ps1', '.resw', '.appxmanifest', '.config'
$especificas = [ordered]@{
    'api.github.com/repos/wynter-yaxley/atrium-desktop' = "api.github.com/repos/$Repo"
    'https://github.com/wynter-yaxley/atrium-desktop'   = "https://github.com/$Repo"
    'github.com/wynter-yaxley/atrium-desktop'           = "github.com/$Repo"
    'Atrium contributors'                               = $Autor
    'Atrium Desktop'                                    = 'Lumen'
    'Atrium-Desktop/1.0'                                = "Lumen/$Versao"
}
$utf8 = New-Object Text.UTF8Encoding $false
$n = 0
Get-ChildItem $Destino -Recurse -File | Where-Object { $textos -contains $_.Extension.ToLowerInvariant() -and $_.Name -ne 'LICENSE' } | ForEach-Object {
    $bytes = [IO.File]::ReadAllBytes($_.FullName)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $t = [Text.Encoding]::UTF8.GetString($bytes, $(if ($bom) { 3 } else { 0 }), $bytes.Length - $(if ($bom) { 3 } else { 0 }))
    $novo = $t
    foreach ($k in $especificas.Keys) { $novo = $novo.Replace($k, $especificas[$k]) }
    $novo = $novo.Replace('Atrium', 'Lumen')
    if ($novo -cne $t) {
        $enc = if ($bom) { New-Object Text.UTF8Encoding $true } else { $utf8 }
        [IO.File]::WriteAllText($_.FullName, $novo, $enc)
        $n++
    }
}
Write-Host "marca: $n ficheiros de texto alterados"

# 3. Nomes de ficheiros e pastas (das folhas para a raiz).
Get-ChildItem $Destino -Recurse | Where-Object { $_.Name -clike '*Atrium*' } | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
    Rename-Item -LiteralPath $_.FullName -NewName ($_.Name.Replace('Atrium', 'Lumen'))
}

# 4. Ícones (os mesmos nomes de ficheiro: o código continua a encontrá-los).
Copy-Item (Join-Path $Marca 'lumen.ico') (Join-Path $Destino 'assets/atrium.ico') -Force
Copy-Item (Join-Path $Marca 'lumen-256.png') (Join-Path $Destino 'assets/atrium-256.png') -Force
Copy-Item (Join-Path $Marca 'lumen-1024.png') (Join-Path $Destino 'assets/atrium-1024.png') -Force

# 5. Versão do Lumen (barra e definições) e autor.
$cm = Join-Path $Destino 'native/CMakeLists.txt'
(Get-Content $cm -Raw) -replace 'project\(atrium_native VERSION [0-9.]+', "project(atrium_native VERSION $Versao" | Set-Content $cm -NoNewline -Encoding utf8
$cs = Join-Path $Destino 'settings/Lumen.Settings/Lumen.Settings.csproj'
$x = Get-Content $cs -Raw
$x = $x -replace '<Version>[^<]+</Version>', "<Version>$Versao</Version>"
$x = $x -replace '<Copyright>[^<]+</Copyright>', "<Copyright>© $Autor · MIT</Copyright>"
[IO.File]::WriteAllText($cs, $x, $utf8)

# 6. Confirmação: não pode sobrar "Atrium" visível no código (fora da licença).
$resto = Get-ChildItem $Destino -Recurse -File | Where-Object { $textos -contains $_.Extension.ToLowerInvariant() -and $_.Name -ne 'LICENSE' } |
    Select-String -SimpleMatch -CaseSensitive 'Atrium' -List
if ($resto) { $resto | Select-Object -First 10 | ForEach-Object { Write-Warning "$($_.Path):$($_.LineNumber)" }; throw 'Sobrou "Atrium" no código' }
Write-Host "marca: Lumen $Versao pronto em $Destino"

exit 0
