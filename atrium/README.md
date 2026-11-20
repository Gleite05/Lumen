# Definições do Atrium

- `settings.json` — vidro personalizado (transparência 0,87, cantos 30, refração 6…), relógio com segundos, widgets à direita, sem o indicador de idioma (`imeStatus: false`), botão VPN (`showVpn`), tecla Windows abre o Iniciar (`winKeyStart`), contadores em todas as apps (`taskbarApps: all`), **modo de controlo remoto** (`remoteVisible: true`: a barra aparece no Chrome Remote Desktop, RustDesk, AnyDesk, TeamViewer, RDP…; o vidro reflete o fundo em vez do que está por trás). As apps afixadas (`pins`) vão vazias: o instalador mantém as tuas.
- `widgets.json` — tempo (grande), Bluetooth, sistema e Spotify (`media`, médio). O widget `media` só existe no Atrium modificado e a galeria da app de definições não o mostra: põe-se por aqui.

Para mudar à mão: `Atrium.exe --exit`, editar `%LOCALAPPDATA%\Atrium\settings.json` (UTF-8 sem BOM) e voltar a abrir o Atrium.