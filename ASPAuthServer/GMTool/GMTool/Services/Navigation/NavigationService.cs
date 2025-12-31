using GMTool.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows.Controls;

namespace GMTool.Services.Navigation
{
    public class NavigationService : INavigationService
    {
        private Frame? _frame;
        private readonly IServiceProvider _serviceProvider;

        public bool CanGoBack => _frame?.CanGoBack ?? false;
        public bool CanGoForward => _frame?.CanGoForward ?? false;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void SetFrame(Frame frame)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
        }

        public void NavigateTo<T>() where T : Page, new()
        {
            if (_frame == null)
                throw new InvalidOperationException("Frame이 설정되지 않았습니다. SetFrame()을 먼저 호출하세요.");

            var page = new T();
            _frame.Navigate(page);
        }

        public void NavigateTo(string pageKey)
        {
            if (_frame == null)
                throw new InvalidOperationException("Frame이 설정되지 않았습니다. SetFrame()을 먼저 호출하세요.");

            Page? page = pageKey switch
            {
                "Dashboard" => _serviceProvider.GetService<DashboardPage>(),
                "Users" => _serviceProvider.GetService<UserListPage>(),
                "Settings" => _serviceProvider.GetService<SettingsPage>(),
                _ => null
            };

            if (page != null)
            {
                _frame.Navigate(page);
            }
        }

        public void GoBack()
        {
            if (_frame?.CanGoBack == true)
            {
                _frame.GoBack();
            }
        }

        public void GoForward()
        {
            if (_frame?.CanGoForward == true)
            {
                _frame.GoForward();
            }
        }
    }
}
