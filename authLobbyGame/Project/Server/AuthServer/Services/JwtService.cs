using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AuthServer.Services
{
    /// <summary>
    /// JWT 토큰 생성 및 검증 서비스
    /// Access Token 발급과 토큰 유효성 검증을 담당
    /// </summary>
    public class JwtService
    {
        private readonly string _key;
        private readonly string _issuer;
        private readonly string _audience;

        /// <summary>
        /// JWT 서비스 생성자
        /// </summary>
        /// <param name="key">토큰 서명에 사용할 비밀키</param>
        /// <param name="issuer">토큰 발급자</param>
        /// <param name="audience">토큰 대상</param>
        public JwtService(string key, string issuer, string audience)
        {
            _key = key;
            _issuer = issuer;
            _audience = audience;
        }

        /// <summary>
        /// Access Token 생성
        /// 사용자 ID를 포함한 JWT 토큰을 생성하여 반환
        /// </summary>
        /// <param name="userId">토큰에 포함할 사용자 ID</param>
        /// <param name="expiresMinutes">토큰 만료 시간 (분 단위, 기본값 60분)</param>
        /// <returns>생성된 JWT 토큰 문자열</returns>
        public string GenerateAccessToken(string userId, int expiresMinutes = 60)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_key);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                }),
                Expires = DateTime.UtcNow.AddMinutes(expiresMinutes),
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// JWT 토큰의 유효성을 검증하고 클레임 정보 반환
        /// 토큰 서명, 발급자, 대상, 만료 시간 등을 확인
        /// </summary>
        /// <param name="token">검증할 JWT 토큰 문자열</param>
        /// <returns>유효한 토큰일 경우 ClaimsPrincipal 객체, 그렇지 않으면 null</returns>
        public ClaimsPrincipal? ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_key);
            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                }, out SecurityToken validatedToken);

                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}
