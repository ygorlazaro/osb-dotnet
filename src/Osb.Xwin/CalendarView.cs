using System;
using System.Collections.Generic;
using System.Globalization;
using Terminal.Gui;

namespace Osb.Xwin;

internal class CalendarView : AppView
{
    private readonly Label _monthLabel;
    private readonly Label _daysView;
    private DateTime _currentDate;
    private int _selectedMonth;
    private int _selectedYear;
    private bool _selectingMonth;
    private bool _selectingYear;

    public CalendarView() : base("Calendário XWin")
    {
        X = 0;
        Y = 0;
        Width = 42;
        Height = 19;

        _currentDate = DateTime.Now;
        _selectedMonth = _currentDate.Month;
        _selectedYear = _currentDate.Year;

        var titleBar = new Label("[_][X]")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
        };

        _monthLabel = new Label("")
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = 1,
        };

        var daysLabel = new Label(" DOM  SEG  TER  QUA  QUI  SEX  SAB")
        {
            X = 0,
            Y = 3,
            Width = Dim.Fill(),
            Height = 1,
        };

        _daysView = new Label("")
        {
            X = 0,
            Y = 4,
            Width = Dim.Fill(),
            Height = 12,
        };

        var prevButton = new Button("[<]")
        {
            X = 0,
            Y = 16,
            Width = 4,
            Height = 1,
        };

        var nextButton = new Button("[>]")
        {
            X = 36,
            Y = 16,
            Width = 4,
            Height = 1,
        };

        var monthSelector = new Button("[ Mes > ]")
        {
            X = 10,
            Y = 16,
            Width = 12,
            Height = 1,
        };

        var yearSelector = new Button("[ Ano > ]")
        {
            X = 24,
            Y = 16,
            Width = 12,
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

        prevButton.Clicked += () =>
        {
            _selectedMonth--;
            if (_selectedMonth < 1)
            {
                _selectedMonth = 12;
                _selectedYear--;
            }
            _currentDate = new DateTime(_selectedYear, _selectedMonth, 1);
            UpdateCalendar();
        };

        nextButton.Clicked += () =>
        {
            _selectedMonth++;
            if (_selectedMonth > 12)
            {
                _selectedMonth = 1;
                _selectedYear++;
            }
            _currentDate = new DateTime(_selectedYear, _selectedMonth, 1);
            UpdateCalendar();
        };

        monthSelector.Clicked += () =>
        {
            _selectingMonth = !_selectingMonth;
            _selectingYear = false;
            if (_selectingMonth)
            {
                ShowMonthSelector();
            }
        };

        yearSelector.Clicked += () =>
        {
            _selectingYear = !_selectingYear;
            _selectingMonth = false;
            if (_selectingYear)
            {
                ShowYearSelector();
            }
        };

        Add(titleBar);
        Add(_monthLabel);
        Add(daysLabel);
        Add(_daysView);
        Add(prevButton);
        Add(nextButton);
        Add(monthSelector);
        Add(yearSelector);
        Add(closeButton);
        Add(minimizeButton);

        UpdateCalendar();
    }

    private void ShowMonthSelector()
    {
        var months = new[]
        {
            "Janeiro", "Fevereiro", "Marco", "Abril", "Maio", "Junho",
            "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"
        };
        var dlg = new Dialog("Selecione o Mes", 30, 16);
        var list = new ListView(months)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 10,
        };
        list.SelectedItemChanged += (e) =>
        {
            if (list.SelectedItem >= 0)
            {
                _selectedMonth = list.SelectedItem + 1;
                _currentDate = new DateTime(_selectedYear, _selectedMonth, 1);
                UpdateCalendar();
            }
            _selectingMonth = false;
            Application.RequestStop(dlg);
        };
        dlg.Add(list);
        Application.Run(dlg);
    }

    private void ShowYearSelector()
    {
        var years = new List<string>();
        for (var y = _selectedYear - 10; y <= _selectedYear + 10; y++)
        {
            years.Add(y.ToString());
        }
        var dlg = new Dialog("Selecione o Ano", 20, 16);
        var list = new ListView(years.ToArray())
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 10,
        };
        list.SelectedItemChanged += (e) =>
        {
            if (list.SelectedItem >= 0 && int.TryParse(years[list.SelectedItem], out var year))
            {
                _selectedYear = year;
                _currentDate = new DateTime(_selectedYear, _selectedMonth, 1);
                UpdateCalendar();
            }
            _selectingYear = false;
            Application.RequestStop(dlg);
        };
        dlg.Add(list);
        Application.Run(dlg);
    }

    private void UpdateCalendar()
    {
        var monthName = _currentDate.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("pt-BR"));
        monthName = char.ToUpperInvariant(monthName[0]) + monthName[1..];
        _monthLabel.Text = monthName;

        var firstDay = new DateTime(_currentDate.Year, _currentDate.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(_currentDate.Year, _currentDate.Month);
        var startDay = (int)firstDay.DayOfWeek;

        var lines = new List<string>();
        var line = new string(' ', startDay * 4);
        var col = startDay;

        for (var day = 1; day <= daysInMonth; day++)
        {
            var isToday = DateTime.Now.Year == _currentDate.Year && DateTime.Now.Month == _currentDate.Month && DateTime.Now.Day == day;
            var dayStr = isToday ? $"[{day,2}]" : $" {day,2} ";
            line += dayStr;
            col++;
            if (col > 6)
            {
                lines.Add(line);
                line = string.Empty;
                col = 0;
            }
        }
        if (line.Length > 0)
        {
            lines.Add(line);
        }

        _daysView.Text = string.Join("\n", lines);
    }

    private void CloseWindow()
    {
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
