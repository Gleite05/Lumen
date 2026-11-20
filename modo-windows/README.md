# Modo Windows original

Um atalho no ambiente de trabalho que troca entre o **Windows 2.0** e o **Windows de origem**.

| Clique em… | O que acontece |
|---|---|
| **Modo Windows original** | Fecha o Atrium (barra, widgets, Iniciar), a BarraOculta, o Central e a barra da voz; mostra a barra do Windows sem ocultar automático; fundo e cursores de origem; a intermitência da barra volta a ser a normal. Nada disto arranca com o Windows enquanto estiveres neste modo (as entradas de arranque saem e a tarefa do Atrium fica desligada). O atalho passa a chamar-se **Modo Windows 2.0**. |
| **Modo Windows 2.0** | Repõe exatamente o que estava: entradas de arranque, tarefa do Atrium, cursores e intermitência (guardados em `%USERPROFILE%\.central\modo.json`), e volta a abrir o Atrium, a BarraOculta, o Central (que põe o fundo) e a barra da voz. |

Útil para emprestar o PC, para uma ligação remota com um programa que não se dê bem com o Atrium, ou para despistar um problema.

`ModoWindows.exe original` / `ModoWindows.exe atrium` forçam um modo. Compila com `compilar.cmd <pasta>` (o csc do .NET Framework, que vem com o Windows); o `instalar.ps1` põe-no em `%USERPROFILE%\.central` e cria o atalho.
