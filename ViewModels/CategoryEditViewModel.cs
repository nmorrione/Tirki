using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tirki.Models;
using Tirki.Services;

namespace Tirki.ViewModels;

[QueryProperty(nameof(Id), "Id")]
public partial class CategoryEditViewModel : ObservableObject
{
    /// <summary>Tavolozza fissa: evita di dover costruire un color-picker completo per un caso d'uso semplice.</summary>
    public static readonly string[] Palette =
    [
        "#4CAF50", "#FF7043", "#2196F3", "#795548", "#E53935", "#FFB300",
        "#8E24AA", "#EC407A", "#26A69A", "#009688", "#3949AB", "#9E9E9E",
    ];

    private readonly LocalDatabaseService _database;
    private readonly AutoSyncService _autoSync;
    private Category _category = new();

    public CategoryEditViewModel(LocalDatabaseService database, AutoSyncService autoSync)
    {
        _database = database;
        _autoSync = autoSync;
        _category.ColorHex = Palette[0];
        SelectedColorHex = Palette[0];
    }

    public ObservableCollection<string> Colors { get; } = new(Palette);

    [ObservableProperty]
    private string? id;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string selectedColorHex = Palette[0];

    [ObservableProperty]
    private bool isExisting;

    [ObservableProperty]
    private string title = "Nuova categoria";

    partial void OnIdChanged(string? value)
    {
        _ = LoadAsync(value);
    }

    private async Task LoadAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !Guid.TryParse(id, out var guid))
        {
            _category = new Category { ColorHex = Palette[0] };
            IsExisting = false;
            Title = "Nuova categoria";
            Name = string.Empty;
            SelectedColorHex = Palette[0];
            return;
        }

        var categories = await _database.GetCategoriesAsync();
        var existing = categories.FirstOrDefault(c => c.Id == guid);
        if (existing is null) return;

        _category = existing;
        IsExisting = true;
        Title = "Modifica categoria";
        Name = existing.Name;
        SelectedColorHex = existing.ColorHex;
    }

    [RelayCommand]
    private void SelectColor(string colorHex)
    {
        SelectedColorHex = colorHex;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlertAsync("Attenzione", "Inserisci un nome per la categoria.", "OK");
            return;
        }

        _category.Name = Name.Trim();
        _category.ColorHex = SelectedColorHex;

        await _database.SaveCategoryAsync(_category);
        _autoSync.TriggerDebouncedSync();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var confirm = await Shell.Current.DisplayAlertAsync(
            "Elimina",
            $"Eliminare la categoria \"{Name}\"? I movimenti già assegnati resteranno senza categoria.",
            "Elimina",
            "Annulla");

        if (!confirm) return;

        await _database.DeleteCategoryAsync(_category);
        _autoSync.TriggerDebouncedSync();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
