using Tirki.ViewModels;

namespace Tirki.Views;

public partial class TransactionEditPage : ContentPage
{
    public TransactionEditPage(TransactionEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
