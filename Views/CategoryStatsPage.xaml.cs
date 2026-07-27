using Tirki.ViewModels;

namespace Tirki.Views;

public partial class CategoryStatsPage : ContentPage
{
    private readonly CategoryStatsViewModel _viewModel;

    public CategoryStatsPage(CategoryStatsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
