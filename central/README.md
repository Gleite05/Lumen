# Central

O fundo de ecrã deixa de ser só uma imagem e passa a ser uma central de controlo.

- **O fundo conta as horas, em segredo.** As 8 camadas verdes acendem-se de baixo para cima ao longo do horário de trabalho definido. Às 9:00 estão apagadas e no fim do dia estão todas acesas; a camada da hora em curso tem um fio de luz na crista. Quem olha vê só um fundo bonito. Nos fins de semana e feriados fica apagado.
- **Painéis por cima, como se fizessem parte do fundo** (a janela fica embutida no ambiente de trabalho, ao lado dos ícones; sobrevive ao Win+D):
  - **Servidores** — estado e tempo de resposta (ping; se não responder, testa a porta TCP). Clicar abre o RDP, o SSH (PuTTY) ou a página.
  - **Hoje** — as reuniões do dia no Outlook clássico (só lê se o Outlook já estiver aberto), com “daqui a X min”.
  - **Rede** — VPN do Windows ligada e há quanto tempo, com interruptor; se nenhuma estiver ligada, lista-as para ligar com um clique. IP público e latência da internet. **Avisa quando a VPN cai** sem teres sido tu.
  - **Atalhos** — RDP, SSH ou páginas web que uses muito.
  - **A tocar** — Spotify (ou outro leitor) com capa, progresso e controlos.
- **Guarda**: guarda uma cópia boa das definições do Atrium sempre que mudam; se o Atrium arrancar com as de fábrica, repõe-nas sozinho. Nos primeiros minutos depois de iniciar sessão garante que a BarraOculta e o Atrium estão a correr.
- Ícone junto ao relógio: **Recarregar** (depois de mudar o `config.json`), abrir a pasta, **Sair**.

## Configuração

`%USERPROFILE%\.central\config.json` (o instalador copia o [`config.exemplo.json`](config.exemplo.json) na primeira vez):

| Campo | O quê |
|---|---|
| `horario` | Blocos de trabalho, na hora do PC: `[["09:00","14:00"],["15:00","18:00"]]` |
| `dias` | Dias de trabalho (0 = domingo … 6 = sábado) |
| `feriados` | Datas `AAAA-MM-DD` sem trabalho |
| `area` | Onde ficam os painéis, em coordenadas de um ecrã 1920×1200 (é escalado) |
| `servidores` | `nome`, `sub`, `host`, `porta` (TCP, se o ping falhar), `acao` |
| `atalhos` | `nome`, `icone` (`server` `mail` `phone` `camera` `globe` `terminal` `shield` `home` `db`), `acao` |
| `guarda` | `false` para desligar a guarda |
| `modo` | `fundo` se preferires uma janela no fundo em vez de embutida no ambiente de trabalho |

Ações: `rdp:host`, `ssh:utilizador@host`, `url:https://…`, `exe:caminho|argumentos`. Não se guardam palavras-passe.

## Compilar

`compilar.cmd <pasta>` — usa o `csc` do .NET Framework 4.8 que vem com o Windows. A pasta tem de ter as DLL do WebView2 (pacote NuGet `Microsoft.Web.WebView2`: `lib\net462` e `runtimes\win-x64\native\WebView2Loader.dll`). O `instalar.ps1` faz tudo isto.

`Central.exe --fundo 0.5` desenha só o fundo (a meio do dia) em `fundo-teste.png`.
