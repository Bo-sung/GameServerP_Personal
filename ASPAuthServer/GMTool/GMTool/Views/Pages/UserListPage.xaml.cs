using GMTool.ViewModels;
using System.Windows.Controls;

namespace GMTool.Views.Pages
{
    public partial class UserListPage : Page
    {
        private readonly UserListViewModel _viewModel;

        public UserListPage(UserListViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            Loaded += UserListPage_Loaded;
        }

        private async void UserListPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }
    }
}
