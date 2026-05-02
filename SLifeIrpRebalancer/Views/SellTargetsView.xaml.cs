using Microsoft.UI.Xaml.Controls;
using SLifeIrpRebalancer.ViewModels;

namespace SLifeIrpRebalancer.Views;

public sealed partial class SellTargetsView : Page
{
    public SellTargetsViewModel ViewModel { get; } = new();

    public SellTargetsView()
    {
        InitializeComponent();
    }
}
