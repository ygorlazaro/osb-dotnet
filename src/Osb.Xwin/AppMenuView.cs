using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui;

namespace Osb.Xwin;

internal class AppMenuView : AppView
{
    public AppMenuView(IReadOnlyList<AppButton> buttons, Action<string> onSelect) : base("Menu de Aplicativos")
    {
        X = 0;
        Y = 0;
        Width = 52;
        Height = 22;

        var list = new ListView(buttons.Select(b => $"[{b.Shortcut}] {b.Label}").ToArray())
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        list.SelectedItemChanged += (e) =>
        {
            if (list.SelectedItem >= 0 && list.SelectedItem < buttons.Count)
            {
                onSelect(buttons[list.SelectedItem].Name);
            }
        };
        Add(list);
    }
}
