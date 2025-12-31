using GMTool.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

namespace GMTool.Views
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel;

        public event EventHandler? LoginSucceeded;

        public LoginWindow(LoginViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            // ViewModel의 LoginSucceeded 이벤트를 Window의 이벤트로 전달
            _viewModel.LoginSucceeded += (s, e) => LoginSucceeded?.Invoke(this, e);

            // Enter 키로 로그인 (PasswordBox는 바인딩 불가하므로 코드비하인드에서 처리)
            PasswordBox.KeyDown += PasswordBox_KeyDown;
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Password를 ViewModel에 전달하고 로그인 실행
                _viewModel.Password = PasswordBox.Password;
                if (_viewModel.LoginCommand.CanExecute(null))
                {
                    _viewModel.LoginCommand.Execute(null);
                }
            }
        }

        // PasswordBox는 바인딩 불가하므로 Command 실행 전에 Password 전달
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (PasswordBox.IsFocused || UsernameTextBox.IsFocused)
            {
                _viewModel.Password = PasswordBox.Password;
            }
        }

        // 로그인 버튼 클릭 시에도 Password 전달 보장
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);
            _viewModel.Password = PasswordBox.Password;
        }
    }
}
