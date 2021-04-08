using System.Windows;
using DicingBlade.ViewModels;

namespace DicingBlade.Views
{
    /// <summary>
    /// Логика взаимодействия для MainWindowView.xaml
    /// </summary>
    public partial class MainWindowView : Window
    {
        public MainWindowView(IMainViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;
        }

    }
}
