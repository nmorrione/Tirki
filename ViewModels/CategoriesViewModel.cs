using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tirki.Models;
using Tirki.Services;

namespace Tirki.ViewModels;

public partial class CategoriesViewModel : ObservableObject
{
    private readonly LocalDatabaseService _database;
    private readonly AutoSyncService _autoSync;

    public CategoriesViewModel(LocalDatabaseService database, AutoSyncService autoSync)
    {
        _database = database;
        _autoSync = autoSync;
    }

    public ObservableCollection<Category> Categories { get; } = new();

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var categories = await _database.GetCategoriesAsync();
            Categories.Clear();
            foreach (var category in categories)
                Categories.Add(category);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.CategoryEditPage));
    }

    [RelayCommand]
    private async Task EditCategoryAsync(Category category)
    {
        await Shell.Current.GoToAsync(nameof(Views.CategoryEditPage), new Dictionary<string, object>
        {
            ["Id"] = category.Id.ToString()
        });
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(Category category)
    {
        var confirm = await Shell.Current.DisplayAlertAsync(
            "Elimina",
            $"Eliminare la categoria \"{category.Name}\"? I movimenti già assegnati resteranno senza categoria.",
            "Elimina",
            "Annulla");

        if (!confirm) return;

        await _database.DeleteCategoryAsync(category);
        _autoSync.TriggerDebouncedSync();
        Categories.Remove(category);
    }
}
