using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Terminal.Gui;

namespace Osb.Xwin;

internal class TextEditorView : AppView
{
    private readonly TextView _textView;
    private string _fileName = "sem_titulo.txt";
    private string _status = "Pronto";
    private readonly string _osbHomeDir;
    private Label _statusLabel;

    private static readonly string[] OsbKeywords =
    {
        "VAR", "FUNC", "RETURN", "IF", "ELSE", "FOR", "WHILE", "DO",
        "PRINT", "INPUT", "END", "THEN", "GOTO", "GOSUB", "SUB",
        "DIM", "AS", "INTEGER", "STRING", "BOOLEAN", "TRUE", "FALSE",
        "AND", "OR", "NOT", "MOD", "DIV", "STEP", "NEXT", "TO",
        "SLEEP", "CLS", "LOCATE", "COLOR", "SOUND", "BEEP"
    };

    public TextEditorView(string osbHomeDir) : base("XWinText Editor")
    {
        _osbHomeDir = osbHomeDir;
        X = 0;
        Y = 0;
        Width = 62;
        Height = 25;

        var btnNew = new Button("Novo")
        {
            X = 0,
            Y = 0,
            Width = 8,
            Height = 1,
        };
        btnNew.Clicked += () => ActionNew();

        var btnOpen = new Button("Abrir")
        {
            X = 9,
            Y = 0,
            Width = 8,
            Height = 1,
        };
        btnOpen.Clicked += () => ActionOpenPrompt();

        var btnSave = new Button("Salvar")
        {
            X = 18,
            Y = 0,
            Width = 8,
            Height = 1,
        };
        btnSave.Clicked += () => ActionSave();

        var btnClose = new Button("Fechar")
        {
            X = 27,
            Y = 0,
            Width = 8,
            Height = 1,
        };
        btnClose.Clicked += () => CloseWindow();

        _statusLabel = new Label("Pronto")
        {
            X = 0,
            Y = 22,
            Width = Dim.Fill(),
            Height = 1,
        };

        _textView = new TextView()
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = 20,
        };
        _textView.TextChanged += () =>
        {
            _status = "Modificado";
            _statusLabel.Text = _status;
        };

        var closeButton = new Button("[X]")
        {
            X = 58,
            Y = 0,
            Width = 4,
            Height = 1,
        };
        closeButton.Clicked += () => CloseWindow();

        var minimizeButton = new Button("[_]")
        {
            X = 54,
            Y = 0,
            Width = 4,
            Height = 1,
        };
        minimizeButton.Clicked += () => MinimizeWindow();

        Add(btnNew);
        Add(btnOpen);
        Add(btnSave);
        Add(btnClose);
        Add(_textView);
        Add(_statusLabel);
        Add(closeButton);
        Add(minimizeButton);
    }

    public override bool OnKeyDown(KeyEvent key)
    {
        if (key.Key == (Key.CtrlMask | Key.N))
        {
            ActionNew();
            return true;
        }
        if (key.Key == (Key.CtrlMask | Key.O))
        {
            ActionOpenPrompt();
            return true;
        }
        if (key.Key == (Key.CtrlMask | Key.S))
        {
            ActionSave();
            return true;
        }
        return base.OnKeyDown(key);
    }

    private void ActionNew()
    {
        _textView.Text = string.Empty;
        _fileName = "sem_titulo.txt";
        _status = "Novo documento criado.";
        _statusLabel.Text = _status;
    }

    private void ActionOpenPrompt()
    {
        var dlg = new Dialog("Abrir Arquivo", 40, 10);
        var tf = new TextField(_fileName)
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = 1,
        };
        dlg.Add(tf);
        var btnOk = new Button("OK");
        btnOk.Clicked += () =>
        {
            var name = tf.Text?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name))
            {
                _fileName = name.Trim();
                ActionOpen();
            }
            Application.RequestStop(dlg);
        };
        dlg.Add(btnOk);
        Application.Run(dlg);
    }

    private void ActionOpen()
    {
        var path = Path.Combine(_osbHomeDir, _fileName);
        if (!File.Exists(path))
        {
            _status = $"Arquivo nao encontrado: {_fileName}";
            _statusLabel.Text = _status;
            return;
        }
        try
        {
            var text = File.ReadAllText(path);
            _textView.Text = text;
            _fileName = Path.GetFileName(path);
            _status = $"Aberto: {_fileName}";
            _statusLabel.Text = _status;
        }
        catch (Exception ex)
        {
            _status = "Erro ao abrir: " + ex.Message;
            _statusLabel.Text = _status;
        }
    }

    private void ActionSave()
    {
        var text = _textView.Text.ToString();
        try
        {
            File.WriteAllText(Path.Combine(_osbHomeDir, _fileName), text);
            _status = $"Salvo: {_fileName}";
            _statusLabel.Text = _status;
        }
        catch (Exception ex)
        {
            _status = "Erro ao salvar: " + ex.Message;
            _statusLabel.Text = _status;
        }
    }

    private void MinimizeWindow()
    {
        var parent = SuperView as XwinApp;
        parent?.MinimizeWindow(this);
        Visible = false;
    }

    private void CloseWindow()
    {
        var parent = SuperView as XwinApp;
        parent?.CloseWindow(this);
        Dispose();
    }
}
