using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Terminal.Gui;

namespace Osb.Xwin;

internal class CodeEditorView : AppView
{
    private readonly List<string> _lines = new() { "" };
    private int _cursorRow;
    private int _cursorCol;
    private int _scrollRow;
    private const int LineNumberWidth = 4;
    private string _fileName = "sem_titulo.txt";
    private string _status = "Pronto";
    private Label _statusLabel;
    private readonly string _osbHomeDir;

    private static readonly string[] OsbKeywords =
    {
        "VAR", "FUNC", "RETURN", "IF", "ELSE", "FOR", "WHILE", "DO",
        "PRINT", "INPUT", "END", "THEN", "GOTO", "GOSUB", "SUB",
        "DIM", "AS", "INTEGER", "STRING", "BOOLEAN", "TRUE", "FALSE",
        "AND", "OR", "NOT", "MOD", "DIV", "STEP", "NEXT", "TO",
        "SLEEP", "CLS", "LOCATE", "COLOR", "SOUND", "BEEP"
    };

    public CodeEditorView(string osbHomeDir) : base("XWinText Editor")
    {
        _osbHomeDir = osbHomeDir;
        X = 0;
        Y = 0;
        Width = 64;
        Height = 25;

        var titleBar = new Label("[_][X]")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
        };

        var btnNew = new Button("Novo")
        {
            X = 0,
            Y = 1,
            Width = 8,
            Height = 1,
        };
        btnNew.Clicked += () => ActionNew();

        var btnOpen = new Button("Abrir")
        {
            X = 9,
            Y = 1,
            Width = 8,
            Height = 1,
        };
        btnOpen.Clicked += () => ActionOpenPrompt();

        var btnSave = new Button("Salvar")
        {
            X = 18,
            Y = 1,
            Width = 8,
            Height = 1,
        };
        btnSave.Clicked += () => ActionSavePrompt();

        var btnClose = new Button("Fechar")
        {
            X = 27,
            Y = 1,
            Width = 8,
            Height = 1,
        };
        btnClose.Clicked += () => CloseWindow();

        _statusLabel = new Label("Pronto")
        {
            X = 0,
            Y = 23,
            Width = Dim.Fill(),
            Height = 1,
        };

        var closeButton = new Button("[X]")
        {
            X = 60,
            Y = 0,
            Width = 4,
            Height = 1,
        };
        closeButton.Clicked += () => CloseWindow();

        var minimizeButton = new Button("[_]")
        {
            X = 56,
            Y = 0,
            Width = 4,
            Height = 1,
        };
        minimizeButton.Clicked += () => MinimizeWindow();

        Add(titleBar);
        Add(btnNew);
        Add(btnOpen);
        Add(btnSave);
        Add(btnClose);
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

        var ch = key.Key.ToString();
        if (ch.Length == 1 && !char.IsControl(ch[0]))
        {
            InsertChar(ch[0]);
            return true;
        }
        if (key.Key == Key.Backspace)
        {
            Backspace();
            return true;
        }
        if (key.Key == Key.Enter)
        {
            NewLine();
            return true;
        }
        if (key.Key == Key.CursorLeft)
        {
            MoveCursor(-1, 0);
            return true;
        }
        if (key.Key == Key.CursorRight)
        {
            MoveCursor(1, 0);
            return true;
        }
        if (key.Key == Key.CursorUp)
        {
            MoveCursor(0, -1);
            return true;
        }
        if (key.Key == Key.CursorDown)
        {
            MoveCursor(0, 1);
            return true;
        }

        return base.OnKeyDown(key);
    }

    private void InsertChar(char c)
    {
        if (_cursorRow < 0 || _cursorRow >= _lines.Count) return;
        var line = _lines[_cursorRow];
        if (_cursorCol < 0) _cursorCol = 0;
        if (_cursorCol > line.Length) _cursorCol = line.Length;
        _lines[_cursorRow] = line.Insert(_cursorCol, c.ToString());
        _cursorCol++;
        _status = "Modificado";
        _statusLabel.Text = _status;
        SetNeedsDisplay();
    }

    private void Backspace()
    {
        if (_cursorRow < 0 || _cursorRow >= _lines.Count) return;
        var line = _lines[_cursorRow];
        if (_cursorCol > 0)
        {
            _lines[_cursorRow] = line.Remove(_cursorCol - 1, 1);
            _cursorCol--;
        }
        else if (_cursorRow > 0)
        {
            var prevLine = _lines[_cursorRow - 1];
            _cursorCol = prevLine.Length;
            _lines[_cursorRow - 1] = prevLine + line;
            _lines.RemoveAt(_cursorRow);
            _cursorRow--;
        }
        _status = "Modificado";
        _statusLabel.Text = _status;
        SetNeedsDisplay();
    }

    private void NewLine()
    {
        if (_cursorRow < 0 || _cursorRow >= _lines.Count) return;
        var line = _lines[_cursorRow];
        var before = line.Substring(0, _cursorCol);
        var after = line.Substring(_cursorCol);
        _lines[_cursorRow] = before;
        _lines.Insert(_cursorRow + 1, after);
        _cursorRow++;
        _cursorCol = 0;
        _status = "Modificado";
        _statusLabel.Text = _status;
        SetNeedsDisplay();
    }

    private void MoveCursor(int dc, int dr)
    {
        _cursorRow += dr;
        _cursorCol += dc;
        if (_cursorRow < 0) _cursorRow = 0;
        if (_cursorRow >= _lines.Count) _cursorRow = _lines.Count - 1;
        if (_cursorCol < 0) _cursorCol = 0;
        if (_cursorCol > (_lines[_cursorRow]?.Length ?? 0)) _cursorCol = _lines[_cursorRow]?.Length ?? 0;
        SetNeedsDisplay();
    }

    public override void OnDrawContent(Rect bounds)
    {
        base.OnDrawContent(bounds);
        var driver = Application.Driver;
        if (driver == null) return;

        var backColor = Color.Black;
        var y = 2;
        var visibleLines = (Bounds.Height > 3 ? Bounds.Height - 3 : 1);
        var startLine = _scrollRow;
        var endLine = Math.Min(startLine + visibleLines, _lines.Count);

        for (var i = startLine; i < endLine; i++)
        {
            var lineNum = i + 1;
            var lineNumStr = $"{lineNum,3} ";
            driver.Move(0, y);
            driver.SetAttribute(new Terminal.Gui.Attribute(Color.White, backColor));
            driver.AddStr(lineNumStr);

            var text = _lines[i] ?? string.Empty;
            var x = 4;
            foreach (var part in GetHighlightedParts(text))
            {
                driver.Move(x, y);
                var foreColor = Color.White;
                switch (part.Foreground)
                {
                    case Color.White: foreColor = Color.White; break;
                    case Color.Brown: foreColor = Color.Brown; break;
                    case Color.Cyan: foreColor = Color.Cyan; break;
                    case Color.Gray: foreColor = Color.Gray; break;
                }
                driver.SetAttribute(new Terminal.Gui.Attribute(foreColor, backColor));
                driver.AddStr(part.Text);
                x += part.Text.Length;
            }
            y++;
        }
    }

    private IEnumerable<(string Text, Color Foreground)> GetHighlightedParts(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield return (string.Empty, Color.White);
            yield break;
        }

        var i = 0;
        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                var start = i;
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                yield return (text.Substring(start, i - start), Color.White);
                continue;
            }

            if (text[i] == '\'')
            {
                var start = i;
                while (i < text.Length && text[i] != '\'') i++;
                if (i < text.Length) i++;
                yield return (text.Substring(start, i - start), Color.Gray);
                continue;
            }

            if (char.IsDigit(text[i]))
            {
                var start = i;
                while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.')) i++;
                yield return (text.Substring(start, i - start), Color.Brown);
                continue;
            }

            if (char.IsLetter(text[i]))
            {
                var start = i;
                while (i < text.Length && (char.IsLetter(text[i]) || char.IsDigit(text[i]) || text[i] == '_')) i++;
                var word = text.Substring(start, i - start);
                if (OsbKeywords.Contains(word.ToUpperInvariant()))
                {
                    yield return (word, Color.Cyan);
                }
                else
                {
                    yield return (word, Color.White);
                }
                continue;
            }

            yield return (text[i].ToString(), Color.White);
            i++;
        }
    }

    private void ActionNew()
    {
        _lines.Clear();
        _lines.Add("");
        _cursorRow = 0;
        _cursorCol = 0;
        _fileName = "sem_titulo.txt";
        _status = "Novo documento criado.";
        _statusLabel.Text = _status;
        SetNeedsDisplay();
    }

    private void ActionOpenPrompt()
    {
        var currentDir = _osbHomeDir;
        if (!string.IsNullOrEmpty(_fileName) && _fileName != "sem_titulo.txt" && !Path.IsPathRooted(_fileName))
        {
            currentDir = Path.Combine(_osbHomeDir, Path.GetDirectoryName(_fileName) ?? string.Empty);
            if (!Directory.Exists(currentDir)) currentDir = _osbHomeDir;
        }

        string? selectedFile = null;
        while (selectedFile == null)
        {
            var dlg = new Dialog("Abrir Arquivo", 60, 18);
            var pathLabel = new Label(currentDir)
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = 1,
            };
            var filesList = Directory.GetFileSystemEntries(currentDir)
                .Select(f => Path.GetFileName(f))
                .OrderBy(f => f)
                .ToArray();
            var filesView = new ListView(filesList)
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = 12,
            };
            var fileNameField = new TextField(Path.GetFileName(_fileName))
            {
                X = 0,
                Y = 14,
                Width = Dim.Fill(),
                Height = 1,
            };
            var btnUp = new Button("..")
            {
                X = 0,
                Y = 15,
                Width = 4,
                Height = 1,
            };
            btnUp.Clicked += () =>
            {
                var parent = Directory.GetParent(currentDir);
                if (parent != null)
                {
                    currentDir = parent.FullName;
                    Application.RequestStop(dlg);
                }
            };
            var btnOk = new Button("OK")
            {
                X = 20,
                Y = 15,
                Width = 8,
                Height = 1,
            };
            btnOk.Clicked += () =>
            {
                var name = fileNameField.Text?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    selectedFile = Path.Combine(currentDir, name.Trim());
                }
                Application.RequestStop(dlg);
            };
            var btnCancel = new Button("Cancelar")
            {
                X = 32,
                Y = 15,
                Width = 10,
                Height = 1,
            };
            btnCancel.Clicked += () =>
            {
                selectedFile = string.Empty;
                Application.RequestStop(dlg);
            };
            filesView.SelectedItemChanged += (e) =>
            {
                if (filesView.SelectedItem >= 0 && filesView.SelectedItem < filesList.Length)
                {
                    var entry = Path.Combine(currentDir, filesList[filesView.SelectedItem]);
                    if (Directory.Exists(entry))
                    {
                        currentDir = entry;
                        Application.RequestStop(dlg);
                    }
                    else
                    {
                        fileNameField.Text = filesList[filesView.SelectedItem];
                    }
                }
            };
            dlg.Add(pathLabel);
            dlg.Add(filesView);
            dlg.Add(fileNameField);
            dlg.Add(btnUp);
            dlg.Add(btnOk);
            dlg.Add(btnCancel);
            Application.Run(dlg);
        }
        if (!string.IsNullOrEmpty(selectedFile))
        {
            _fileName = Path.GetFileName(selectedFile);
            ActionOpen();
        }
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
            _lines.Clear();
            _lines.AddRange(text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None));
            if (_lines.Count == 0) _lines.Add("");
            _cursorRow = 0;
            _cursorCol = 0;
            _fileName = Path.GetFileName(path);
            _status = $"Aberto: {_fileName}";
            _statusLabel.Text = _status;
            SetNeedsDisplay();
        }
        catch (Exception ex)
        {
            _status = "Erro ao abrir: " + ex.Message;
            _statusLabel.Text = _status;
        }
    }

    private void ActionSave()
    {
        var text = string.Join("\n", _lines);
        var targetPath = Path.Combine(_osbHomeDir, _fileName);
        if (File.Exists(targetPath))
        {
            var overwrite = ConfirmOverwrite(_fileName);
            if (!overwrite) return;
        }
        try
        {
            File.WriteAllText(targetPath, text);
            _status = $"Salvo: {_fileName}";
            _statusLabel.Text = _status;
        }
        catch (Exception ex)
        {
            _status = "Erro ao salvar: " + ex.Message;
            _statusLabel.Text = _status;
        }
    }

    private bool ConfirmOverwrite(string fileName)
    {
        var dlg = new Dialog("Confirmar", 40, 8);
        var label = new Label($"O arquivo '{fileName}' ja existe. Sobrescrever?")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 2,
        };
        var btnYes = new Button("Sim")
        {
            X = 8,
            Y = 3,
            Width = 8,
            Height = 1,
        };
        var result = false;
        btnYes.Clicked += () =>
        {
            result = true;
            Application.RequestStop(dlg);
        };
        var btnNo = new Button("Nao")
        {
            X = 20,
            Y = 3,
            Width = 8,
            Height = 1,
        };
        btnNo.Clicked += () =>
        {
            result = false;
            Application.RequestStop(dlg);
        };
        dlg.Add(label);
        dlg.Add(btnYes);
        dlg.Add(btnNo);
        Application.Run(dlg);
        return result;
    }

    private void ActionSavePrompt()
    {
        var currentDir = _osbHomeDir;
        string? selectedFile = null;
        while (selectedFile == null)
        {
            var dlg = new Dialog("Salvar Arquivo", 60, 18);
            var pathLabel = new Label(currentDir)
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = 1,
            };
            var filesList = Directory.GetFileSystemEntries(currentDir)
                .Select(f => Path.GetFileName(f))
                .OrderBy(f => f)
                .ToArray();
            var filesView = new ListView(filesList)
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = 12,
            };
            var fileNameField = new TextField(_fileName)
            {
                X = 0,
                Y = 14,
                Width = Dim.Fill(),
                Height = 1,
            };
            var btnUp = new Button("..")
            {
                X = 0,
                Y = 15,
                Width = 4,
                Height = 1,
            };
            btnUp.Clicked += () =>
            {
                var parent = Directory.GetParent(currentDir);
                if (parent != null)
                {
                    currentDir = parent.FullName;
                    Application.RequestStop(dlg);
                }
            };
            var btnSave = new Button("Salvar")
            {
                X = 20,
                Y = 15,
                Width = 8,
                Height = 1,
            };
            btnSave.Clicked += () =>
            {
                var name = fileNameField.Text?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    selectedFile = Path.Combine(currentDir, name.Trim());
                }
                Application.RequestStop(dlg);
            };
            var btnCancel = new Button("Cancelar")
            {
                X = 32,
                Y = 15,
                Width = 10,
                Height = 1,
            };
            btnCancel.Clicked += () =>
            {
                selectedFile = string.Empty;
                Application.RequestStop(dlg);
            };
            filesView.SelectedItemChanged += (e) =>
            {
                if (filesView.SelectedItem >= 0 && filesView.SelectedItem < filesList.Length)
                {
                    var entry = Path.Combine(currentDir, filesList[filesView.SelectedItem]);
                    if (Directory.Exists(entry))
                    {
                        currentDir = entry;
                        Application.RequestStop(dlg);
                    }
                    else
                    {
                        fileNameField.Text = filesList[filesView.SelectedItem];
                    }
                }
            };
            dlg.Add(pathLabel);
            dlg.Add(filesView);
            dlg.Add(fileNameField);
            dlg.Add(btnUp);
            dlg.Add(btnSave);
            dlg.Add(btnCancel);
            Application.Run(dlg);
        }
        if (!string.IsNullOrEmpty(selectedFile))
        {
            _fileName = Path.GetFileName(selectedFile);
            ActionSave();
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
