using Tirki.ViewModels;

namespace Tirki.Views;

public partial class TrendPage : ContentPage
{
    private readonly TrendViewModel _viewModel;

    public TrendPage(TrendViewModel viewModel)
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
