using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GMTool.Services.Navigation;
using System;

namespace GMTool.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private string _currentUserDisplay = "관리자: admin";

        public event EventHandler? LogoutRequested;

        public MainWindowViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        [RelayCommand]
        private void NavigateToDashboard()
        {
            _navigationService.NavigateTo("Dashboard");
        }

        [RelayCommand]
        private void NavigateToUsers()
        {
            _navigationService.NavigateTo("Users");
        }

        [RelayCommand]
        private void NavigateToSettings()
        {
            _navigationService.NavigateTo("Settings");
        }

        [RelayCommand]
        private void Logout()
        {
            LogoutRequested?.Invoke(this, EventArgs.Empty);
        }

        public void SetCurrentUser(string username, string role)
        {
            CurrentUserDisplay = $"관리자: {username} ({role})";
        }
    }
}
