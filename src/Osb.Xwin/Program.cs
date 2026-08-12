// XWIN - interface gráfica alternativa do OSB original.
//
// Este porte roda inteiramente em modo texto/console (nada de X11, Wayland, ou
// qualquer toolkit gráfico), do mesmo jeito que o VBDOS original não precisava de
// nada além da BIOS de vídeo do DOS. É lançado pelo Osb.Shell quando o usuário
// digita o comando X, exatamente como o COMMAND.COM do MS-DOS 6.22 chamava o
// WIN.COM: o XWIN toma conta da tela até o usuário sair, e aí o controle volta
// para o prompt do OSB.

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.CancelKeyPress += (_, e) => { e.Cancel = true; };
Osb.Xwin.MainMenu.Run();
