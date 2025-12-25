namespace AuthServer.Models
{
    // Request 모델들
    public record RegisterRequest(string Username, string Password, string? Email = null);

    public record LoginRequest(string Username, string Password, string DeviceId);

    public record RefreshTokenRequest(string RefreshToken);

    public record ChangePasswordRequest(string OldPassword, string NewPassword);

    // Response 모델들
    public record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn, string TokenType = "Bearer");

    public record ErrorResponse(string ErrorCode, string Message, object? Details = null);

    public record UserInfoResponse(string UserId, string Username, string? Email, DateTime CreatedAt);
}
