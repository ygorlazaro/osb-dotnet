using System;
using System.Globalization;
using Terminal.Gui;

namespace Osb.Xwin;

internal class ClockView : AppView
{
    private readonly Label _timeLabel;
    private readonly Label _dateLabel;
    private readonly Label _stopwatchLabel;
    private readonly Button _formatButton;
    private readonly Button _toggleButton;
    private readonly Button _resetButton;
    private bool _format24H = true;
    private bool _stopwatchActive;
    private DateTime _stopwatchStart;
    private TimeSpan _stopwatchElapsed;
    private object _timeoutToken;

    public ClockView() : base("Relógio XWin")
    {
        X = 0;
        Y = 0;
        Width = 44;
        Height = 13;

        _timeLabel = new Label("")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
        };

        _dateLabel = new Label("")
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = 1,
        };

        _stopwatchLabel = new Label("Cronômetro: 00:00.00")
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = 1,
        };

        _formatButton = new Button("[ 12h ]")
        {
            X = 0,
            Y = 10,
            Width = 8,
            Height = 1,
        };

        _toggleButton = new Button("[ Iniciar ]")
        {
            X = 10,
            Y = 10,
            Width = 12,
            Height = 1,
        };

        _resetButton = new Button("[ Zerar ]")
        {
            X = 24,
            Y = 10,
            Width = 8,
            Height = 1,
        };

        var closeButton = new Button("[X]")
        {
            X = 36,
            Y = 0,
            Width = 4,
            Height = 1,
        };
        closeButton.Clicked += () => CloseWindow();

        var minimizeButton = new Button("[_]")
        {
            X = 32,
            Y = 0,
            Width = 4,
            Height = 1,
        };
        minimizeButton.Clicked += () => MinimizeWindow();

        _formatButton.Clicked += () =>
        {
            _format24H = !_format24H;
            _formatButton.Text = _format24H ? "[ 12h ]" : "[ 24h ]";
        };

        _toggleButton.Clicked += () =>
        {
            if (_stopwatchActive)
            {
                _stopwatchActive = false;
                _toggleButton.Text = "[ Iniciar ]";
            }
            else
            {
                _stopwatchStart = DateTime.Now - _stopwatchElapsed;
                _stopwatchActive = true;
                _toggleButton.Text = "[ Pausar ]";
            }
        };

        _resetButton.Clicked += () =>
        {
            _stopwatchActive = false;
            _stopwatchElapsed = TimeSpan.Zero;
            _toggleButton.Text = "[ Iniciar ]";
            _stopwatchLabel.Text = "Cronômetro: 00:00.00";
        };

        Add(_timeLabel);
        Add(_dateLabel);
        Add(_stopwatchLabel);
        Add(_formatButton);
        Add(_toggleButton);
        Add(_resetButton);
        Add(closeButton);
        Add(minimizeButton);

        _timeoutToken = Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(100), _ => UpdateClock());
    }

    private bool UpdateClock()
    {
        var now = DateTime.Now;
        var timeStr = _format24H ? now.ToString("HH:mm:ss") : now.ToString("hh:mm:ss tt");
        _timeLabel.Text = timeStr;

        var culture = CultureInfo.GetCultureInfo("pt-BR");
        var dateStr = now.ToString("dddd, dd 'de' MMMM 'de' yyyy", culture);
        dateStr = char.ToUpperInvariant(dateStr[0]) + dateStr[1..];
        _dateLabel.Text = dateStr;

        if (_stopwatchActive)
        {
            _stopwatchElapsed = DateTime.Now - _stopwatchStart;
            _stopwatchLabel.Text = $"Cronômetro: {_stopwatchElapsed:mm\\:ss\\.ff}";
        }

        return true;
    }

    private void CloseWindow()
    {
        if (_timeoutToken != null)
        {
            Application.MainLoop.RemoveTimeout(_timeoutToken);
        }
        var parent = SuperView as XwinApp;
        parent?.CloseWindow(this);
    }

    private void MinimizeWindow()
    {
        var parent = SuperView as XwinApp;
        parent?.MinimizeWindow(this);
        Visible = false;
    }
}
