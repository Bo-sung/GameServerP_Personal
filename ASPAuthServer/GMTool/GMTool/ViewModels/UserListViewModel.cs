using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GMTool.Services.User;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace GMTool.ViewModels
{
    public partial class UserListViewModel : ObservableObject
    {
        private readonly IUserService _userService;

        [ObservableProperty]
        private ObservableCollection<User> _users = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private int _statusFilter = 0; // 0: 전체, 1: 활성, 2: 비활성, 3: 잠김

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _totalPages = 1;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private int _pageSize = 20;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public UserListViewModel(IUserService userService)
        {
            _userService = userService;
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            CurrentPage = 1;
            await LoadUsersAsync();
        }

        [RelayCommand]
        private async Task LoadUsersAsync()
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;

            try
            {
                bool? isActive = StatusFilter switch
                {
                    1 => true,  // 활성
                    2 => false, // 비활성
                    _ => null   // 전체
                };

                var result = await _userService.GetUsersAsync(CurrentPage, PageSize, SearchQuery, isActive);

                if (result != null)
                {
                    Users.Clear();
                    if (result.Users != null)
                    {
                        foreach (var user in result.Users)
                        {
                            Users.Add(user);
                        }
                    }

                    TotalCount = result.TotalCount;
                    TotalPages = (TotalCount + PageSize - 1) / PageSize;
                }
                else
                {
                    HasError = true;
                    ErrorMessage = "사용자 목록을 가져올 수 없습니다.";
                }
            }
            catch (System.Exception ex)
            {
                HasError = true;
                ErrorMessage = $"사용자 목록 로드 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanGoPreviousPage))]
        private async Task PreviousPageAsync()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                await LoadUsersAsync();
            }
        }

        private bool CanGoPreviousPage() => CurrentPage > 1 && !IsLoading;

        [RelayCommand(CanExecute = nameof(CanGoNextPage))]
        private async Task NextPageAsync()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                await LoadUsersAsync();
            }
        }

        private bool CanGoNextPage() => CurrentPage < TotalPages && !IsLoading;

        public async Task InitializeAsync()
        {
            await LoadUsersAsync();
        }
    }
}
