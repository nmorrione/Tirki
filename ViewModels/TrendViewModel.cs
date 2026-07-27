using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using Tirki.Services;
using LinearGradientPaint = LiveChartsCore.SkiaSharpView.Painting.LinearGradientPaint;

namespace Tirki.ViewModels;

public partial class TrendViewModel : ObservableObject
{
    public const string AllTimeOption = "Tutto lo storico";

    private static readonly CultureInfo ItalianCulture = new("it-IT");
    private static readonly SKColor LineColor = new(76, 175, 80);

    private readonly LocalDatabaseService _database;
    private readonly AutoSyncService _autoSync;
    private bool _suppressPeriodReload;

    public TrendViewModel(LocalDatabaseService database, AutoSyncService autoSync)
    {
        _database = database;
        _autoSync = autoSync;

        selectedPeriod = AllTimeOption;

        _autoSync.SyncCompleted += () => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());
    }

    public ObservableCollection<string> PeriodOptions { get; } = new();

    public ObservableCollection<ISeries> Series { get; } = new();

    public ObservableCollection<ICartesianAxis> XAxes { get; } = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasData;

    [ObservableProperty]
    private string selectedPeriod;

    partial void OnSelectedPeriodChanged(string value)
    {
        if (_suppressPeriodReload) return;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var transactions = await _database.GetTransactionsAsync();

            RebuildPeriodOptions(transactions);

            if (transactions.Count == 0)
            {
                HasData = false;
                Series.Clear();
                XAxes.Clear();
                return;
            }

            var months = BuildMonthRange(transactions);

            var netByMonth = transactions
                .GroupBy(t => new DateTime(t.Date.Year, t.Date.Month, 1))
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

            var values = months.Select(m => (double)netByMonth.GetValueOrDefault(m)).ToList();
            var labels = months.Select(m => ItalianCulture.TextInfo.ToTitleCase(m.ToString("MMM yy", ItalianCulture))).ToList();

            HasData = true;
            BuildChart(values, labels);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildPeriodOptions(List<Models.Transaction> transactions)
    {
        var years = transactions.Select(t => t.Date.Year).Distinct().OrderByDescending(y => y).ToList();

        var options = new List<string> { AllTimeOption };
        options.AddRange(years.Select(y => y.ToString(CultureInfo.InvariantCulture)));

        if (PeriodOptions.SequenceEqual(options)) return;

        var previousSelection = SelectedPeriod;
        PeriodOptions.Clear();
        foreach (var option in options)
            PeriodOptions.Add(option);

        _suppressPeriodReload = true;
        SelectedPeriod = options.Contains(previousSelection) ? previousSelection : AllTimeOption;
        _suppressPeriodReload = false;
    }

    private List<DateTime> BuildMonthRange(List<Models.Transaction> transactions)
    {
        var today = DateTime.Today;
        DateTime start;
        DateTime end;

        if (SelectedPeriod == AllTimeOption || !int.TryParse(SelectedPeriod, out var year))
        {
            var earliest = transactions.Min(t => t.Date);
            start = new DateTime(earliest.Year, earliest.Month, 1);
            end = new DateTime(today.Year, today.Month, 1);
        }
        else
        {
            start = new DateTime(year, 1, 1);
            end = year == today.Year ? new DateTime(today.Year, today.Month, 1) : new DateTime(year, 12, 1);
        }

        var months = new List<DateTime>();
        for (var month = start; month <= end; month = month.AddMonths(1))
            months.Add(month);

        return months;
    }

    private void BuildChart(List<double> values, List<string> labels)
    {
        Series.Clear();

        Series.Add(new LineSeries<double>
        {
            Values = values,
            Name = "Risparmio netto",
            Stroke = new SolidColorPaint(LineColor, 3),
            Fill = new LinearGradientPaint(LineColor.WithAlpha(90), LineColor.WithAlpha(0), new SKPoint(0, 0), new SKPoint(0, 1)),
            GeometrySize = 6,
            GeometryStroke = new SolidColorPaint(LineColor, 3),
            GeometryFill = new SolidColorPaint(SKColors.White),
        });

        Series.Add(new LineSeries<double>
        {
            Values = values.Select(_ => 0.0).ToList(),
            Name = "Zero",
            Stroke = new SolidColorPaint(new SKColor(158, 158, 158), 1) { PathEffect = new DashEffect(new float[] { 6, 6 }) },
            Fill = null,
            GeometrySize = 0,
            IsHoverable = false,
        });

        XAxes.Clear();
        XAxes.Add(new Axis { Labels = labels, LabelsRotation = 0 });
    }
}
