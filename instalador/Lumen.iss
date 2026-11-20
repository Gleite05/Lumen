; Instalador do Lumen (Inno Setup 6).
;   iscc /DVersao=1.0.0 /DPacote=..\saida\pacote Lumen.iss
; Instala por utilizador (sem pedir administrador) em %LOCALAPPDATA%\Programs\Lumen.
; No fim corre "Lumen.Classico.exe --instalar", que configura e arranca tudo;
; ao desinstalar, "--desinstalar" devolve o Windows de origem.

#ifndef Versao
  #define Versao "1.0.0"
#endif
#ifndef Pacote
  #define Pacote "..\saida\pacote"
#endif

[Setup]
AppId={{8B3C6F2E-4D1A-4E7B-9C55-1F0A7E2D9B31}
AppName=Lumen
AppVersion={#Versao}
AppVerName=Lumen {#Versao}
AppPublisher=Gonçalo Leite
AppPublisherURL=https://github.com/Gleite05/Lumen
AppSupportURL=https://github.com/Gleite05/Lumen/issues
AppUpdatesURL=https://github.com/Gleite05/Lumen/releases
AppCopyright=© 2026 Gonçalo Leite
VersionInfoVersion={#Versao}
VersionInfoProductName=Lumen
VersionInfoDescription=Lumen
DefaultDirName={localappdata}\Programs\Lumen
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
OutputDir=..\saida
OutputBaseFilename=Lumen-Setup-{#Versao}
SetupIconFile=..\marca\lumen.ico
UninstallDisplayIcon={app}\lumen.ico
UninstallDisplayName=Lumen
WizardStyle=modern
WizardImageFile=imagens\lado-100.bmp,imagens\lado-150.bmp,imagens\lado-200.bmp
WizardSmallImageFile=imagens\canto-100.bmp,imagens\canto-150.bmp,imagens\canto-200.bmp
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
CloseApplications=no
RestartApplications=no
ShowLanguageDialog=auto
UsePreviousAppDir=yes
UsePreviousLanguage=yes

[Languages]
Name: "pt"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
pt.TemaTitulo=Escolhe o teu tema
pt.TemaSub=As cores do fundo, dos painéis e do Painel. Podes mudar quando quiseres.
pt.TemaTexto=O fundo vivo acende-se ao longo do teu dia de trabalho.
pt.CurTitulo=Cursores
pt.CurSub=O ponteiro do rato combina com o vidro do Lumen.
pt.CurTexto=Escolhe o estilo do ponteiro:
pt.CurEscuro=Fluent escuro (recomendado)
pt.CurClaro=Fluent claro
pt.CurWindows=Os do Windows
pt.TarefaAtalho=Pôr no ambiente de trabalho o atalho «Modo Windows clássico» (volta ao Windows de sempre com um clique)
pt.AConfigurar=A preparar o teu Lumen…
pt.AbrirPainel=Abrir o Painel do Lumen para personalizar
pt.SemWebView=O Lumen precisa do Microsoft Edge WebView2 Runtime (já vem com o Windows 11).%n%nNão foi encontrado neste PC. Instala-o a partir de https://go.microsoft.com/fwlink/p/?LinkId=2124703 e volta a correr o instalador.
pt.Painel=Lumen
en.TemaTitulo=Choose your theme
en.TemaSub=The colours of the wallpaper, the panels and the Panel. Change them any time.
en.TemaTexto=The live wallpaper lights up through your work day.
en.CurTitulo=Cursors
en.CurSub=A mouse pointer that matches Lumen's glass.
en.CurTexto=Choose the pointer style:
en.CurEscuro=Fluent dark (recommended)
en.CurClaro=Fluent light
en.CurWindows=Windows' own
en.TarefaAtalho=Put a "Classic Windows mode" shortcut on the desktop (back to plain Windows in one click)
en.AConfigurar=Getting your Lumen ready…
en.AbrirPainel=Open the Lumen Panel to customize it
en.SemWebView=Lumen needs the Microsoft Edge WebView2 Runtime (it comes with Windows 11).%n%nIt wasn't found on this PC. Install it from https://go.microsoft.com/fwlink/p/?LinkId=2124703 and run the installer again.
en.Painel=Lumen
es.TemaTitulo=Elige tu tema
es.TemaSub=Los colores del fondo, los paneles y el Panel. Puedes cambiarlos cuando quieras.
es.TemaTexto=El fondo vivo se ilumina a lo largo de tu jornada.
es.CurTitulo=Cursores
es.CurSub=Un puntero que combina con el cristal de Lumen.
es.CurTexto=Elige el estilo del puntero:
es.CurEscuro=Fluent oscuro (recomendado)
es.CurClaro=Fluent claro
es.CurWindows=Los de Windows
es.TarefaAtalho=Poner en el escritorio el acceso «Modo Windows clásico» (vuelve al Windows de siempre con un clic)
es.AConfigurar=Preparando tu Lumen…
es.AbrirPainel=Abrir el Panel de Lumen para personalizarlo
es.SemWebView=Lumen necesita Microsoft Edge WebView2 Runtime (viene con Windows 11).%n%nNo se encontró en este PC. Instálalo desde https://go.microsoft.com/fwlink/p/?LinkId=2124703 y vuelve a ejecutar el instalador.
es.Painel=Lumen

[Tasks]
Name: "atalho"; Description: "{cm:TarefaAtalho}"; Check: not Atualizacao

[Files]
Source: "{#Pacote}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[InstallDelete]
; versões antigas: o que já não faz parte do pacote sai (as definições ficam em %LOCALAPPDATA%\Lumen)
Type: filesandordirs; Name: "{app}\web"
Type: filesandordirs; Name: "{app}\Settings"
Type: filesandordirs; Name: "{app}\locales"

[Icons]
Name: "{userprograms}\Lumen"; Filename: "{app}\Lumen.Painel.exe"; IconFilename: "{app}\lumen.ico"; Comment: "{cm:Painel}"

[Run]
Filename: "{app}\Lumen.Classico.exe"; Parameters: "--instalar {code:ArgsInstalar}"; StatusMsg: "{cm:AConfigurar}"; Flags: waituntilterminated
Filename: "{app}\Lumen.Painel.exe"; Description: "{cm:AbrirPainel}"; Flags: postinstall nowait skipifsilent

[UninstallRun]
Filename: "{app}\Lumen.Classico.exe"; Parameters: "--desinstalar"; Flags: waituntilterminated; RunOnceId: "LumenDesinstalar"

[UninstallDelete]
; as definições ficam (para quem volta a instalar); só as caches saem
Type: filesandordirs; Name: "{localappdata}\Lumen\webview"
Type: filesandordirs; Name: "{localappdata}\Lumen\webview-painel"
Type: filesandordirs; Name: "{localappdata}\Lumen\fundo"
Type: filesandordirs; Name: "{localappdata}\Lumen\cache"

[Code]
var
  PaginaTema, PaginaCursores: TInputOptionWizardPage;

const
  Temas = 'esmeralda,oceano,indigo,ambar,grafite,glaciar';

function Atualizacao: Boolean;
begin
  Result := FileExists(ExpandConstant('{localappdata}\Lumen\lumen.json'));
end;

function TemWebView2: Boolean;
var v: String;
begin
  Result :=
    (RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', v) and (v <> '') and (v <> '0.0.0.0')) or
    (RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', v) and (v <> '') and (v <> '0.0.0.0'));
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if not TemWebView2 then
  begin
    SuppressibleMsgBox(CustomMessage('SemWebView'), mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

function ParteDe(Lista: String; Indice: Integer): String;
var i, p: Integer;
begin
  for i := 1 to Indice do begin p := Pos(',', Lista); Delete(Lista, 1, p); end;
  p := Pos(',', Lista);
  if p > 0 then Result := Copy(Lista, 1, p - 1) else Result := Lista;
end;

function NomeTema(i: Integer): String;
begin
  if ActiveLanguage = 'en' then Result := ParteDe('Emerald,Ocean,Indigo,Amber,Graphite,Glacier (light)', i)
  else if ActiveLanguage = 'es' then Result := ParteDe('Esmeralda,Océano,Índigo,Ámbar,Grafito,Glaciar (claro)', i)
  else Result := ParteDe('Esmeralda,Oceano,Índigo,Âmbar,Grafite,Glaciar (claro)', i);
end;

procedure InitializeWizard;
var i: Integer;
begin
  PaginaTema := CreateInputOptionPage(wpSelectTasks, CustomMessage('TemaTitulo'), CustomMessage('TemaSub'), CustomMessage('TemaTexto'), True, False);
  for i := 0 to 5 do PaginaTema.Add(NomeTema(i));
  PaginaTema.SelectedValueIndex := 0;
  PaginaCursores := CreateInputOptionPage(PaginaTema.ID, CustomMessage('CurTitulo'), CustomMessage('CurSub'), CustomMessage('CurTexto'), True, False);
  PaginaCursores.Add(CustomMessage('CurEscuro'));
  PaginaCursores.Add(CustomMessage('CurClaro'));
  PaginaCursores.Add(CustomMessage('CurWindows'));
  PaginaCursores.SelectedValueIndex := 0;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  // numa atualização ficam o tema e os cursores que já tens
  Result := Atualizacao and ((PageID = PaginaTema.ID) or (PageID = PaginaCursores.ID));
end;

function ArgsInstalar(Param: String): String;
var idioma, cursores: String;
begin
  Result := '';
  if Atualizacao then Exit; // o que já está configurado fica
  if ActiveLanguage = 'en' then idioma := 'en' else if ActiveLanguage = 'es' then idioma := 'es' else idioma := 'pt-PT';
  case PaginaCursores.SelectedValueIndex of 1: cursores := 'claro'; 2: cursores := 'windows'; else cursores := 'escuro'; end;
  Result := '--idioma ' + idioma + ' --tema ' + ParteDe(Temas, PaginaTema.SelectedValueIndex) + ' --cursores ' + cursores;
  if WizardIsTaskSelected('atalho') then Result := Result + ' --atalho';
end;

// Antes de copiar: fecha o Lumen que estiver a correr (atualizações) sem mudar nada.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var codigo: Integer;
begin
  Result := '';
  if FileExists(ExpandConstant('{app}\Lumen.Classico.exe')) then
    Exec(ExpandConstant('{app}\Lumen.Classico.exe'), '--parar', '', SW_HIDE, ewWaitUntilTerminated, codigo);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM Lumen.Painel.exe', '', SW_HIDE, ewWaitUntilTerminated, codigo);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM Lumen.Settings.exe', '', SW_HIDE, ewWaitUntilTerminated, codigo);
end;
