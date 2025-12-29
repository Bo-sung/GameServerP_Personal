using System.Threading.Tasks;

namespace GMTool.Services.User
{
    public interface IUserService
    {
        /// <summary>
        /// 사용자 목록 조회 (페이지네이션)
        /// </summary>
        Task<UsersResponse> GetUsersAsync(int page = 1, int pageSize = 20, string? search = null, bool? isActive = null);

        /// <summary>
        /// 특정 사용자 조회
        /// </summary>
        Task<User> GetUserByIdAsync(int userId);

        /// <summary>
        /// 사용자 계정 잠금/해제
        /// </summary>
        Task LockUserAsync(int userId, bool lockAccount, int? durationMinutes = null);

        /// <summary>
        /// 비밀번호 초기화
        /// </summary>
        Task ResetPasswordAsync(int userId, string newPassword);

        /// <summary>
        /// 세션 강제 종료
        /// </summary>
        Task TerminateSessionsAsync(int userId, string? deviceId = null);

        /// <summary>
        /// 사용자 삭제
        /// </summary>
        Task DeleteUserAsync(int userId);
    }
}
