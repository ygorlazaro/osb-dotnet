using Osb.Xwin;
using Terminal.Gui;

Application.Init();
Application.Top!.ColorScheme = Colors.TopLevel;
Application.Run(new XwinApp());
Application.Shutdown();
