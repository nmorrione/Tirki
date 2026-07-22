using Tirki.ViewModels;

namespace Tirki.Views;

public partial class MainPage : ContentPage
{
    private readonly TransactionsViewModel _viewModel;

    public MainPage(TransactionsViewModel viewModel)
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
