using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Tirki.Services;

namespace Tirki.ViewModels;

public partial class CategoryStatsViewModel : ObservableObject
{
    private const string UncategorizedName = "Senza categoria";
    private const string UncategorizedColorHex = "#9E9E9E";

    private static readonly CultureInfo ItalianCulture = new("it-IT");

    private readonly LocalDatabaseService _database;
    private readonly AutoSyncService _autoSync;
    private bool _suppressFilterReload;
    private string? _lastLoadedSignature;

    public CategoryStatsViewModel(LocalDatabaseService database, AutoSyncService autoSync)
    {
        _database = database;
        _autoSync = autoSync;

        var today = DateTime.Today;
        _suppressFilterReload = true;
        FilterFrom = new DateTime(today.Year, today.Month, 1);
        FilterTo = FilterFrom.AddMonths(1).AddDays(-1);
        _suppressFilterReload = false;

        _autoSync.SyncCompleted += () => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());
    }

    public ObservableCollection<ISeries> Series { get; } = new();

    public ObservableCollection<CategoryStatRow> Rows { get; } = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private DateTime filterFrom;

    [ObservableProperty]
    private DateTime filterTo;

    [ObservableProperty]
    private decimal totalIncome;

    [ObservableProperty]
    private decimal totalExpense;

    [ObservableProperty]
    private bool hasExpenses;

    partial void OnFilterFromChanged(DateTime value) => ReloadFromFilterChange();

    partial void OnFilterToChanged(DateTime value) => ReloadFromFilterChange();

    private void ReloadFromFilterChange()
    {
        if (_suppressFilterReload) return;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task SetCurrentMonthAsync()
    {
        var today = DateTime.Today;
        _suppressFilterReload = true;
        FilterFrom = new DateTime(today.Year, today.Month, 1);
        FilterTo = FilterFrom.AddMonths(1).AddDays(-1);
        _suppressFilterReload = false;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ManageCategoriesAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.CategoriesPage));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var from = FilterFrom.Date;
            var to = FilterTo.Date.AddDays(1).AddTicks(-1);
            var transactions = await _database.GetTransactionsAsync(from, to);
            var categories = await _database.GetCategoriesAsync();

            var signature = BuildSignature(transactions);
            if (signature == _lastLoadedSignature) return;
            _lastLoadedSignature = signature;

            TotalIncome = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
            TotalExpense = Math.Abs(transactions.Where(t => t.Amount < 0).Sum(t => t.Amount));

            var categoriesById = categories.ToDictionary(c => c.Id);
            var expenseRows = transactions
                .Where(t => t.Amount < 0)
                .GroupBy(t => t.CategoryId)
                .Select(g =>
                {
                    var (name, colorHex) = g.Key is { } categoryId && categoriesById.TryGetValue(categoryId, out var category)
                        ? (category.Name, category.ColorHex)
                        : (UncategorizedName, UncategorizedColorHex);
                    return new CategoryStatRow { Name = name, ColorHex = colorHex, Total = Math.Abs(g.Sum(t => t.Amount)) };
                })
                .OrderByDescending(r => r.Total)
                .ToList();

            HasExpenses = expenseRows.Count > 0;

            Rows.Clear();
            foreach (var row in expenseRows)
                Rows.Add(row);

            Series.Clear();
            foreach (var row in expenseRows)
            {
                Series.Add(new PieSeries<double>
                {
                    Values = new[] { (double)row.Total },
                    Name = row.Name,
                    Fill = new SolidColorPaint(SKColor.Parse(row.ColorHex)),
                });
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string BuildSignature(List<Models.Transaction> transactions)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var t in transactions)
            sb.Append(t.Id).Append(':').Append(t.Amount).Append(':').Append(t.CategoryId).Append(';');
        return sb.ToString();
    }
}
