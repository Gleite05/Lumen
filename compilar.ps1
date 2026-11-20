<#
.SYNOPSIS
  Compila as peças próprias do Lumen (Central, Painel, Clássico) com o csc do .NET Framework 4.8,
  que vem com o Windows. Não precisa do Visual Studio.

.PARAMETER Saida
  Pasta onde ficam os .exe (e as DLL do WebView2).
.PARAMETER WebView2
  Pasta com Microsoft.Web.WebView2.Core.dll, Microsoft.Web.WebView2.WinForms.dll e WebView2Loader.dll
  (pacote NuGet Microsoft.Web.WebView2: lib\net462 + runtimes\win-x64\native). Sem ela, o script descarrega-o.
#>
param(
    [string]$Saida = (Join-Path $PSScriptRoot 'saida\lumen'),
    [string]$WebView2 = '',
    [string]$VersaoWebView2 = '1.0.3485.44'
)
$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$versao = (Get-Content (Join-Path $raiz 'VERSION') -Raw).Trim()
New-Item -ItemType Directory -Force $Saida | Out-Null

# WebView2: usa a pasta dada ou descarrega o pacote NuGet.
if (-not $WebView2) {
    $WebView2 = Join-Path $raiz 'saida\webview2'
    if (-not (Test-Path (Join-Path $WebView2 'Microsoft.Web.WebView2.Core.dll'))) {
        $tmp = Join-Path $raiz 'saida\webview2.zip'
        New-Item -ItemType Directory -Force $WebView2 | Out-Null
        Invoke-WebRequest "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/$VersaoWebView2" -OutFile $tmp -UseBasicParsing
        $x = Join-Path $raiz 'saida\webview2-pkg'
        Expand-Archive $tmp $x -Force
        Copy-Item "$x\lib\net462\Microsoft.Web.WebView2.Core.dll", "$x\lib\net462\Microsoft.Web.WebView2.WinForms.dll", "$x\runtimes\win-x64\native\WebView2Loader.dll" $WebView2
        Remove-Item $tmp, $x -Recurse -Force
    }
}
foreach ($dll in 'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll', 'WebView2Loader.dll') { Copy-Item (Join-Path $WebView2 $dll) $Saida -Force }

$fw = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$wm = "$env:WINDIR\System32\WinMetadata"
$csc = Join-Path $fw 'csc.exe'
$info = Join-Path $Saida 'Versao.g.cs'
@"
[assembly: System.Reflection.AssemblyVersion("$versao.0")]
[assembly: System.Reflection.AssemblyFileVersion("$versao.0")]
[assembly: System.Reflection.AssemblyProduct("Lumen")]
[assembly: System.Reflection.AssemblyCompany("Gonçalo Leite")]
[assembly: System.Reflection.AssemblyCopyright("© Gonçalo Leite · MIT")]
"@ | Set-Content $info -Encoding utf8

$comuns = @("/r:System.Windows.Forms.dll", "/r:System.Drawing.dll", "/r:System.Web.Extensions.dll", "/r:Microsoft.CSharp.dll", "/r:System.Core.dll",
    "/r:$Saida\Microsoft.Web.WebView2.Core.dll", "/r:$Saida\Microsoft.Web.WebView2.WinForms.dll")
$winrt = @("/r:$wm\Windows.Foundation.winmd", "/r:$wm\Windows.Media.winmd", "/r:$wm\Windows.Storage.winmd",
    "/r:$fw\System.Runtime.dll", "/r:$fw\System.Runtime.WindowsRuntime.dll", "/r:$fw\System.Runtime.InteropServices.WindowsRuntime.dll", "/r:$fw\System.Threading.Tasks.dll")
$icone = Join-Path $raiz 'marca\lumen.ico'
$base = @("$raiz\comum\Lumen.cs", "$raiz\comum\Temas.cs", $info)

function Compilar($nome, $fontes, $extra) {
    $args = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001', "/out:$Saida\$nome", "/win32icon:$icone") + $comuns + $extra + $base + $fontes
    & $csc @args
    if ($LASTEXITCODE) { throw "falhou a compilação de $nome" }
    Write-Host "ok $nome"
}

$c = "$raiz\central"
Compilar 'Lumen.Central.exe' @("$c\Central.cs", "$c\Dados.cs", "$c\Musica.cs", "$c\Rato.cs", "$c\Guarda.cs", "$c\BarraOculta.cs") $winrt
if (Test-Path "$raiz\painel\Painel.cs") { Compilar 'Lumen.Painel.exe' @("$raiz\painel\Painel.cs", "$raiz\comum\Aplicar.cs") @() }
if (Test-Path "$raiz\classico\Classico.cs") { Compilar 'Lumen.Classico.exe' @("$raiz\classico\Classico.cs", "$raiz\comum\Aplicar.cs") @() }

# Páginas
New-Item -ItemType Directory -Force "$Saida\web\central" | Out-Null
Copy-Item "$c\web\*" "$Saida\web\central" -Recurse -Force
if (Test-Path "$raiz\painel\web") { New-Item -ItemType Directory -Force "$Saida\web\painel" | Out-Null; Copy-Item "$raiz\painel\web\*" "$Saida\web\painel" -Recurse -Force }
Remove-Item $info
Write-Host "Lumen $versao compilado em $Saida"
