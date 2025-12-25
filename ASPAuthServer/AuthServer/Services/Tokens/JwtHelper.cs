using AuthServer.Settings;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace AuthServer.Services.Tokens
{
    /// <summary>
    /// JWT 토큰 생성을 위한 공통 헬퍼 클래스
    /// </summary>
    public static class JwtHelper
    {
        public static string GenerateToken(Claim[] claims, JwtSettings settings, DateTime expires)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(settings.SecretKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expires,
                Issuer = settings.Issuer,
                Audience = settings.Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// JWT 토큰 검증 및 파싱
        /// </summary>
        /// <param name="token">검증할 JWT 토큰</param>
        /// <param name="settings">JWT 설정</param>
        /// <returns>(검증 성공 여부, ClaimsPrincipal, 에러 메시지)</returns>
        public static (bool IsValid, ClaimsPrincipal? Principal, string? ErrorMessage) ParseToken(
            string token,
            JwtSettings settings)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(settings.SecretKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),

                ValidateIssuer = true,
                ValidIssuer = settings.Issuer,

                ValidateAudience = true,
                ValidAudience = settings.Audience,

                ValidateLifetime = true,        // 만료 시간 검증
                ClockSkew = TimeSpan.Zero      // 시간 허용 오차 0 (기본 5분)
            };

            try
            {
                var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                return (true, principal, null);
            }
            catch (SecurityTokenExpiredException)
            {
                return (false, null, "토큰이 만료되었습니다.");
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                return (false, null, "토큰 서명이 유효하지 않습니다.");
            }
            catch (SecurityTokenException ex)
            {
                return (false, null, $"토큰이 유효하지 않습니다: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, null, $"토큰 파싱 중 오류 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// JWT 토큰에서 특정 Claim 값 추출 (검증 없이)
        /// </summary>
        /// <param name="token">JWT 토큰</param>
        /// <param name="claimType">Claim 타입</param>
        /// <returns>Claim 값 (없으면 null)</returns>
        public static string? GetClaimValue(string token, string claimType)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                return jwtToken.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// JWT 토큰의 만료 시간 확인
        /// </summary>
        /// <param name="token">JWT 토큰</param>
        /// <returns>만료 여부</returns>
        public static bool IsTokenExpired(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                return DateTime.UtcNow > jwtToken.ValidTo;
            }
            catch
            {
                return true;
            }
        }
    }
}
