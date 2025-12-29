using System;
using System.Windows.Controls;

namespace GMTool.Services.Navigation
{
    public class NavigationService : INavigationService
    {
        private Frame? _frame;

        public bool CanGoBack => _frame?.CanGoBack ?? false;
        public bool CanGoForward => _frame?.CanGoForward ?? false;

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
