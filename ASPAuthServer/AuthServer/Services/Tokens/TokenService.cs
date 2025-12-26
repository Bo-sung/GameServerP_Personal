using AuthServer.Data;
using AuthServer.Data.Repositories;
using AuthServer.Models;
using AuthServer.Settings;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AuthServer.Services.Tokens
{
    /// <summary>
    /// 토큰 생성, 검증, 폐기를 담당하는 서비스
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly TokenFactory tokenFactory;

        private readonly IRedisConnectionFactory _redisFactory;
        private readonly JwtSettings _jwtSettings;

        public TokenService(
            IRedisConnectionFactory redisFactory,
            IOptions<JwtSettings> jwtSettings)
        {
            this._jwtSettings = jwtSettings.Value;
            this._redisFactory = redisFactory;
            this.tokenFactory = new TokenFactory(jwtSettings);
        }

        /// <summary>
        /// 토큰 생성 (타입에 따라 Login/Access/Refresh 토큰 생성)
        /// </summary>
        public async Task<string?> CreateToken(ITokenService.TokenType type, int user_id, string deviceId)
        {
            IToken token = type switch
            {
                ITokenService.TokenType.Login => tokenFactory.CreateLoginToken(user_id, deviceId),
                ITokenService.TokenType.Access => tokenFactory.CreateAccessToken(user_id),
                ITokenService.TokenType.Refresh => tokenFactory.CreateRefreshToken(user_id, deviceId),
                _ => throw new ArgumentOutOfRangeException(nameof(type), "Invalid token type"),
            };

            if (token == null)
                return null;

            // Access 토큰은 상태가 없으므로 Redis에 저장하지 않음
            if (type == ITokenService.TokenType.Access)
                return token.GetTokenString();

            var redis = _redisFactory.GetDatabase();
            await redis.StringSetAsync(token.GetRedisKey(), token.GetRedisValue(), TimeSpan.FromSeconds(token.GetTTL()));

            return token.GetTokenString();
        }

        /// <summary>
        /// 토큰 폐기 (Redis에서 토큰 삭제)
        /// </summary>
        public async Task<bool> RevokeTokenAsync(string token, ITokenService.TokenType type)
        {
            // 토큰 파싱 및 검증
            var parseResult = JwtHelper.ParseToken(token, _jwtSettings);
            if (!parseResult.IsValid || parseResult.Principal == null)
            {
                Console.WriteLine($"[TokenService] 토큰 파싱 실패: {parseResult.ErrorMessage}");
                return false;
            }

            switch (type)
            {
                case ITokenService.TokenType.Access:
                    {
                        // Access 토큰은 Redis에 저장되지 않으므로 폐기 작업 불필요
                        break;
                    }
                case ITokenService.TokenType.Login:
                    {
                        Claim? claim_type = parseResult.Principal.FindFirst("type");
                        if (claim_type == null || claim_type.Value != "login")
                        {
                            Console.WriteLine($"[TokenService] 로그인 토큰의 타입 불일치");
                            return false;
                        }
                        Claim? claim_jti = parseResult.Principal.FindFirst(JwtRegisteredClaimNames.Jti);
                        if (claim_jti == null)
                        {
                            Console.WriteLine($"[TokenService] 로그인 토큰에서 JTI claim을 찾을 수 없음");
                            return false;
                        }
                        string jti = claim_jti.Value;
                        var redis = _redisFactory.GetDatabase();
                        await redis.KeyDeleteAsync(LogintToken.BuildActiveRedisKey(jti));
                        return true;
                    }
                case ITokenService.TokenType.Refresh:
                    {
                        Claim? claim_type = parseResult.Principal.FindFirst("type");
                        if (claim_type == null || claim_type.Value != "refresh")
                        {
                            Console.WriteLine($"[TokenService] 리프레시 토큰의 타입 불일치");
                            return false;
                        }
                        Claim? claim_userId = parseResult.Principal.FindFirst("userId");
                        if (claim_userId == null || !int.TryParse(claim_userId.Value, out int userId))
                        {
                            Console.WriteLine($"[TokenService] 리프레시 토큰에서 userId claim을 찾을 수 없음");
                            return false;
                        }
                        Claim? claim_deviceId = parseResult.Principal.FindFirst("deviceId");
                        if (claim_deviceId == null)
                        {
                            Console.WriteLine($"[TokenService] 리프레시 토큰에서 deviceId claim을 찾을 수 없음");
                            return false;
                        }
                        string deviceId = claim_deviceId.Value;
                        var redis = _redisFactory.GetDatabase();
                        await redis.KeyDeleteAsync(RefreshToken.BuildRedisKey(userId, deviceId));
                        return true;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), "잘못된 토큰 타입");
            }
            return false;
        }

        /// <summary>
        /// 토큰 검증 (타입 자동 감지)
        /// </summary>
        public async Task<bool> ValidateTokenAsync(string token)
        {
            // JWT 토큰 파싱 및 검증
            var parseResult = JwtHelper.ParseToken(token, _jwtSettings);
            if (!parseResult.IsValid || parseResult.Principal == null)
            {
                Console.WriteLine($"[TokenService] 토큰 파싱 실패: {parseResult.ErrorMessage}");
                return false;
            }

            Claim? claim_type = parseResult.Principal.FindFirst("type");
            if (claim_type == null)
            {
                Console.WriteLine($"[TokenService] 토큰 타입 claim을 찾을 수 없음");
                return false;
            }

            switch (claim_type.Value)
            {
                case "access":
                    {
                        // Access 토큰은 상태가 없으므로 파싱 성공 시 유효함
                    }
                    break;
                case "login":
                    {
                        Claim? claim_jti = parseResult.Principal.FindFirst(JwtRegisteredClaimNames.Jti);
                        if (claim_jti == null)
                        {
                            Console.WriteLine($"[TokenService] 로그인 토큰에서 JTI claim을 찾을 수 없음");
                            return false;
                        }
                        string jti = claim_jti.Value;
                        var redis = _redisFactory.GetDatabase();

                        string usedKey = LogintToken.BuildUsedRedisKey(jti);
                        if (await redis.KeyExistsAsync(usedKey))
                        {
                            Console.WriteLine($"[TokenService] 경고: 로그인 토큰 재사용 시도 감지! JTI: {jti}");
                            Console.WriteLine($"[TokenService] 이미 교환된 토큰입니다. 토큰 탈취 가능성!");
                            return false;
                        }

                        string activeKey = LogintToken.BuildActiveRedisKey(jti);
                        if (!await redis.KeyExistsAsync(activeKey))
                        {
                            Console.WriteLine($"[TokenService] 활성 토큰 목록에서 로그인 토큰을 찾을 수 없음. JTI: {jti}");
                            return false;
                        }
                    }
                    break;
                case "refresh":
                    {
                        Claim? claim_userId = parseResult.Principal.FindFirst("userId");
                        if (claim_userId == null || !int.TryParse(claim_userId.Value, out int userId))
                        {
                            Console.WriteLine($"[TokenService] 리프레시 토큰에서 userId claim을 찾을 수 없음");
                            return false;
                        }
                        Claim? claim_deviceId = parseResult.Principal.FindFirst("deviceId");
                        if (claim_deviceId == null)
                        {
                            Console.WriteLine($"[TokenService] 리프레시 토큰에서 deviceId claim을 찾을 수 없음");
                            return false;
                        }
                        string deviceId = claim_deviceId.Value;

                        // Redis 연결 가져오기
                        var redis = _redisFactory.GetDatabase();

                        string redisKey = RefreshToken.BuildRedisKey(userId, deviceId);

                        // 레디스에 토큰이 존재하는지 확인
                        if (!await redis.KeyExistsAsync(redisKey))
                        {
                            Console.WriteLine($"[TokenService] Redis에서 리프레시 토큰을 찾을 수 없음");
                            return false;
                        }
                    }
                    break;
            }
            return true;
        }

        /// <summary>
        /// 토큰 검증 (타입 명시)
        /// </summary>
        public async Task<bool> ValidateTokenAsync(string token, ITokenService.TokenType type)
        {
            // JWT 토큰 파싱 및 검증
            var parseResult = JwtHelper.ParseToken(token, _jwtSettings);
            if (!parseResult.IsValid || parseResult.Principal == null)
            {
                Console.WriteLine($"[TokenService] 토큰 파싱 실패: {parseResult.ErrorMessage}");
                return false;
            }
            switch (type)
            {
                case ITokenService.TokenType.Access:
                    {
                        Claim? claim_type = parseResult.Principal.FindFirst("type");
                        if (claim_type == null || claim_type.Value != "access")
                        {
                            Console.WriteLine($"[TokenService] Access 토큰의 타입 불일치");
                            return false;
                        }
                    }
                    break;
                case ITokenService.TokenType.Login:
                    {
                        Claim? claim_type = parseResult.Principal.FindFirst("type");
                        if (claim_type == null || claim_type.Value != "login")
                        {
                            Console.WriteLine($"[TokenService] 로그인 토큰의 타입 불일치");
                            return false;
                        }
                        Claim? claim_jti = parseResult.Principal.FindFirst(JwtRegisteredClaimNames.Jti);
                        if (claim_jti == null)
                        {
                            Console.WriteLine($"[TokenService] 로그인 토큰에서 JTI claim을 찾을 수 없음");
                            return false;
                        }
                        string jti = claim_jti.Value;
                        var redis = _redisFactory.GetDatabase();

                        string usedKey = LogintToken.BuildUsedRedisKey(jti);
                        if (await redis.KeyExistsAsync(usedKey))
                        {
                            Console.WriteLine($"[TokenService] 경고: 로그인 토큰 재사용 시도 감지! JTI: {jti}");
                            Console.WriteLine($"[TokenService] 이미 교환된 토큰입니다. 토큰 탈취 가능성!");
                            return false;
                        }

                        string activeKey = LogintToken.BuildActiveRedisKey(jti);
                        if (!await redis.KeyExistsAsync(activeKey))
                        {
                            Console.WriteLine($"[TokenService] 활성 토큰 목록에서 로그인 토큰을 찾을 수 없음. JTI: {jti}");
                            return false;
                        }
                    }
                    break;
                case ITokenService.TokenType.Refresh:
                    {
                        Claim? claim_type = parseResult.Principal.FindFirst("type");
                        if (claim_type == null || claim_type.Value != "refresh")
                        {
                            Console.WriteLine($"[TokenService] 리프레시 토큰의 타입 불일치");
                            return false;
                        }
                        Claim? claim_userId = parseResult.Principal.FindFirst("userId");
                        if (claim_userId == null || !int.TryParse(claim_userId.Value, out int userId))
                        {
                            Console.WriteLine($"[TokenService] 리프레시 토큰에서 userId claim을 찾을 수 없음");
                            return false;
                        }
                        Claim? claim_deviceId = parseResult.Principal.FindFirst("deviceId");
                        if (claim_deviceId == null)
                        {
                            Console.WriteLine($"[TokenService] 리프레시 토큰에서 deviceId claim을 찾을 수 없음");
                            return false;
                        }
                        string deviceId = claim_deviceId.Value;

                        // Redis 연결 가져오기
                        var redis = _redisFactory.GetDatabase();

                        string redisKey = RefreshToken.BuildRedisKey(userId, deviceId);

                        // 레디스에 토큰이 존재하는지 확인
                        if (!await redis.KeyExistsAsync(redisKey))
                        {
                            Console.WriteLine($"[TokenService] Redis에서 리프레시 토큰을 찾을 수 없음");
                            return false;
                        }
                    }break;
            }
            return true;
        }

        /// <summary>
        /// 로그인 토큰을 사용됨으로 표시 (1회용 토큰 방지)
        /// </summary>
        public async Task<bool> MarkLoginTokenAsUsedAsync(string token)
        {
            var parseResult = JwtHelper.ParseToken(token, _jwtSettings);
            if (!parseResult.IsValid || parseResult.Principal == null)
            {
                Console.WriteLine($"[TokenService] 토큰 파싱 실패: {parseResult.ErrorMessage}");
                return false;
            }

            Claim? claim_type = parseResult.Principal.FindFirst("type");
            if (claim_type == null || claim_type.Value != "login")
            {
                Console.WriteLine($"[TokenService] 토큰 타입 불일치. 'login' 토큰이 필요함");
                return false;
            }

            Claim? claim_jti = parseResult.Principal.FindFirst(JwtRegisteredClaimNames.Jti);
            if (claim_jti == null)
            {
                Console.WriteLine($"[TokenService] 로그인 토큰에서 JTI claim을 찾을 수 없음");
                return false;
            }
            string jti = claim_jti.Value;

            var redis = _redisFactory.GetDatabase();

            string activeKey = LogintToken.BuildActiveRedisKey(jti);
            bool wasActive = await redis.KeyDeleteAsync(activeKey);

            if (!wasActive)
            {
                Console.WriteLine($"[TokenService] 활성 목록에 로그인 토큰이 없음. JTI: {jti}");
                return false;
            }

            string usedKey = LogintToken.BuildUsedRedisKey(jti);
            string usedValue = System.Text.Json.JsonSerializer.Serialize(new
            {
                jti = jti,
                usedAt = DateTime.UtcNow,
                originalToken = token.Substring(0, Math.Min(20, token.Length)) + "..."
            });

            var retentionTime = TimeSpan.FromHours(_jwtSettings.UsedLoginTokenRetentionHours);
            await redis.StringSetAsync(usedKey, usedValue, retentionTime);

            Console.WriteLine($"[TokenService] 로그인 토큰을 사용됨으로 표시. JTI: {jti}, 보관시간: {_jwtSettings.UsedLoginTokenRetentionHours}시간");
            return true;
        }
    }
}
