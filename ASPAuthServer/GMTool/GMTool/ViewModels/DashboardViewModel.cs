using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GMTool.Services.Statistics;
using System.Threading.Tasks;

namespace GMTool.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IStatisticsService _statisticsService;

        [ObservableProperty]
        private int _totalUsers;

        [ObservableProperty]
        private int _activeUsers;

        [ObservableProperty]
        private int _lockedUsers;

        [ObservableProperty]
        private int _onlineUsers;

        [ObservableProperty]
        private int _todayRegistrations;

        [ObservableProperty]
        private int _todayLogins;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public DashboardViewModel(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
        }

        [RelayCommand]
        private async Task LoadStatisticsAsync()
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;

            try
            {
                var stats = await _statisticsService.GetStatisticsAsync();

                if (stats != null)
                {
                    TotalUsers = stats.TotalUsers;
                    ActiveUsers = stats.ActiveUsers;
                    LockedUsers = stats.LockedUsers;
                    OnlineUsers = stats.OnlineUsers;
                    TodayRegistrations = stats.TodayRegistrations;
                    TodayLogins = stats.TodayLogins;
                }
                else
                {
                    HasError = true;
                    ErrorMessage = "통계 데이터를 가져올 수 없습니다.";
                }
            }
            catch (System.Exception ex)
            {
                HasError = true;
                ErrorMessage = $"통계 로드 실패: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task InitializeAsync()
        {
            await LoadStatisticsAsync();
        }
    }
}
