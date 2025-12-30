using GMTool.Infrastructure.Config;
using GMTool.Services.Auth;
using GMTool.Services.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace GMTool.Services.User
{

    public record User(
        int Id,
        string Username,
        string Email,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? LastLoginAt,
        int LoginAttempts,
        DateTime? LockedUntil
        );

    public record UserLockRequest
        (
            bool Islock,
            int? DurationMinitues
        );

    public record ResetPasswordRequest
        (
            string NewPassword
        );

    public record UsersResponse
        (
            int TotalCount,
            int Page,
            int PageSize,
            int TotalPages,
            User[] Users
        );

    public record UserLockResponse
        (
            string Message,
            int UserId,
            DateTime? LockedUntil
        );

    public record TerminateSessionsResponse
        (
            string Message,
            int UserId,
            int SessionsTerminated
        );

    public record DeleteUserResponse
        (
            string Message,
            int UserId
        );

    public class UserService : IUserService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogService _logService;
        private readonly AppSettings _appSettings;

        public UserService(
            HttpClient httpClient,
            ILogService logService,
            AppSettings appSettings)
        {
            _httpClient = httpClient;
            _logService = logService;
            _appSettings = appSettings;
        }

        public async Task<UsersResponse> GetUsersAsync(int page = 1, int pageSize = 20, string? search = null, bool? isActive = null)
        {
            // GET /api/admin/users?page={page}&pageSize={pageSize}&search={search}&isActive={isActive}
            try
            {
                var response = await _httpClient.GetAsync($"/api/admin/users?page={page}&pageSize={pageSize}&search={search}&isActive={isActive}");

                if (response == null)
                    throw new NotImplementedException();

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logService.Error($"사용자 목록 조회 실패: {response.StatusCode}", errorContent);
                    throw new HttpRequestException($"사용자 목록 조회 실패: {response.StatusCode}");
                }
                var result = await response.Content.ReadFromJsonAsync<UsersResponse>();
                return result ?? throw new InvalidOperationException("사용자 목록 응답이 유효하지 않습니다.");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "사용자 목록 조회 중 예외 발생");
                throw;
            }
        }

        public async Task<User> GetUserByIdAsync(int userId)
        {
            // GET /api/admin/users/{userId}
            try
            {
                var response = await _httpClient.GetAsync($"/api/admin/users/{userId}");

                if (response == null)
                    throw new NotImplementedException();

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logService.Error($"사용자 조회 실패: {response.StatusCode}", errorContent);
                    throw new HttpRequestException($"사용자 조회 실패: {response.StatusCode}");
                }
                var result = await response.Content.ReadFromJsonAsync<User>();
                return result ?? throw new InvalidOperationException("사용자 응답이 유효하지 않습니다.");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "사용자 조회 중 예외 발생");
                throw;
            }
        }

        public async Task LockUserAsync(int userId, bool lockAccount, int? durationMinutes = null)
        {
            // PATCH /api/admin/users/{userId}/lock
            try
            {
                var action = lockAccount ? "잠금" : "해제";
                _logService.Warning($"사용자 #{userId} 계정 {action} 시도" +
                    (durationMinutes.HasValue ? $": {durationMinutes}분" : ""));

                var request = new UserLockRequest
                (
                    Islock: lockAccount,
                    DurationMinitues: durationMinutes
                );

                var url = $"/api/admin/users/{userId}/lock";
                var response = await _httpClient.PatchAsJsonAsync(url, request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logService.Error($"계정 {action} 실패: {response.StatusCode}", errorContent);
                    throw new HttpRequestException($"계정 {action} 실패: {response.StatusCode}");
                }

                var result = await response.Content.ReadFromJsonAsync<UserLockResponse>();

                _logService.Success($"사용자 #{userId} 계정 {action} 완료");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "사용자 조회 중 예외 발생");
                throw;
            }
        }

        public async Task ResetPasswordAsync(int userId, string newPassword)
        {
            // POST /api/admin/users/{userId}/reset-password
            try
            {
                _logService.Warning($"사용자 #{userId} 비밀번호 초기화 시도");

                var request = new ResetPasswordRequest(
                    NewPassword: newPassword
                    );

                var url = $"/api/admin/users/{userId}/reset-password";
                var response = await _httpClient.PostAsJsonAsync(url, request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logService.Error($"비밀번호 초기화 실패: {response.StatusCode}", errorContent);
                    throw new HttpRequestException($"비밀번호 초기화 실패: {response.StatusCode}");
                }

                _logService.Success($"사용자 #{userId} 비밀번호 초기화 완료");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, $"비밀번호 초기화 중 예외 발생: userId={userId}");
                throw;
            }
        }

        public async Task TerminateSessionsAsync(int userId, string? deviceId = null)
        {
            try
            {
                var target = string.IsNullOrEmpty(deviceId) ? "모든 세션" : $"디바이스 {deviceId}";
                _logService.Warning($"사용자 #{userId} {target} 강제 종료 시도");

                var url = $"/api/admin/users/{userId}/sessions";

                if (!string.IsNullOrEmpty(deviceId))
                    url += $"?deviceId={Uri.EscapeDataString(deviceId)}";

                var response = await _httpClient.DeleteAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logService.Error($"세션 종료 실패: {response.StatusCode}", errorContent);
                    throw new HttpRequestException($"세션 종료 실패: {response.StatusCode}");
                }

                var result = await response.Content.ReadFromJsonAsync<TerminateSessionsResponse>();

                if (result != null)
                {
                    _logService.Success($"사용자 #{userId} 세션 {result.SessionsTerminated}개 종료 완료");
                }
            }
            catch (Exception ex)
            {
                _logService.Error(ex, $"세션 종료 중 예외 발생: userId={userId}");
                throw;
            }
        }

        public async Task DeleteUserAsync(int userId)
        {
            // DELETE /api/admin/users/{userId}
            try
            {
                _logService.Warning($"사용자 #{userId} 삭제 시도");

                var url = $"/api/admin/users/{userId}";
                var response = await _httpClient.DeleteAsync(url);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logService.Warning($"사용자를 찾을 수 없음: userId={userId}");
                    throw new KeyNotFoundException($"사용자 ID {userId}를 찾을 수 없습니다.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logService.Error($"사용자 삭제 실패: {response.StatusCode}", errorContent);
                    throw new HttpRequestException($"사용자 삭제 실패: {response.StatusCode}");
                }

                _logService.Success($"사용자 #{userId} 삭제 완료");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, $"사용자 삭제 중 예외 발생: userId={userId}");
                throw;
            }
        }
    }
}
