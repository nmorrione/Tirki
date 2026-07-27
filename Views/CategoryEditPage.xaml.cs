using Tirki.ViewModels;

namespace Tirki.Views;

public partial class CategoryEditPage : ContentPage
{
    public CategoryEditPage(CategoryEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
