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
        private readonly IOptionsSnapshot<JwtSettings> _jwtSettingsSnapshot;
        private readonly ILogger<TokenService> _logger;

        public TokenService(
            IRedisConnectionFactory redisFactory,
            IOptionsSnapshot<JwtSettings> jwtSettingsSnapshot,
            ILogger<TokenService> logger)
        {
            this._jwtSettingsSnapshot = jwtSettingsSnapshot;
            this._redisFactory = redisFactory;
            this.tokenFactory = new TokenFactory(jwtSettingsSnapshot);
            this._logger = logger;
        }

        /// <summary>
        /// audience에 따라 올바른 JwtSettings 반환
        /// </summary>
        private JwtSettings GetJwtSettings(string audience)
        {
            return audience switch
            {
                "GameClient" => _jwtSettingsSnapshot.Get("Game"),
                "AdminPanel" => _jwtSettingsSnapshot.Get("Admin"),
                _ => throw new ArgumentException($"Unknown audience: {audience}", nameof(audience))
            };
        }

        /// <summary>
        /// 토큰 생성 (타입에 따라 Login/Access/Refresh 토큰 생성)
        /// </summary>
        public async Task<string?> CreateToken(ITokenService.TokenType type, int user_id, string deviceId, string audience = "GameClient")
        {
            IToken token = type switch
            {
                ITokenService.TokenType.Login => tokenFactory.CreateLoginToken(user_id, deviceId, audience),
                ITokenService.TokenType.Access => tokenFactory.CreateAccessToken(user_id, audience),
                ITokenService.TokenType.Refresh => tokenFactory.CreateRefreshToken(user_id, deviceId, audience),
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
        public async Task<bool> RevokeTokenAsync(string token, string deviceId, ITokenService.TokenType type)
        {
            // 토큰에서 audience 추출 후 해당 JwtSettings로 파싱
            // 우선 Game으로 시도, 실패 시 Admin으로 시도
            var parseResult = JwtHelper.ParseToken(token, GetJwtSettings("GameClient"));
            if (!parseResult.IsValid)
            {
                parseResult = JwtHelper.ParseToken(token, GetJwtSettings("AdminPanel"));
            }

            if (!parseResult.IsValid || parseResult.Principal == null)
            {
                _logger.LogWarning("토큰 파싱 실패: {ErrorMessage}", parseResult.ErrorMessage);
                return false;
            }

            switch (type)
            {
                case ITokenService.TokenType.Access:
                    // Access 토큰은 Redis에 저장되지 않으므로 폐기 작업 불필요
                    break;

                case ITokenService.TokenType.Login:
                    {
                        var (typeSuccess, _) = TryExtractTokenType(parseResult.Principal, "login");
                        if (!typeSuccess) return false;

                        var (jtiSuccess, jti) = TryExtractJti(parseResult.Principal);
                        if (!jtiSuccess) return false;

                        var redis = _redisFactory.GetDatabase();
                        await redis.KeyDeleteAsync(LogintToken.BuildActiveRedisKey(jti));
                        return true;
                    }

                case ITokenService.TokenType.Refresh:
                    {
                        var (typeSuccess, _) = TryExtractTokenType(parseResult.Principal, "refresh");
                        if (!typeSuccess) return false;

                        var (userIdSuccess, userId) = TryExtractUserId(parseResult.Principal);
                        if (!userIdSuccess) return false;

                        var (deviceIdSuccess, did) = TryExtractDeviceId(parseResult.Principal);
                        if (!deviceIdSuccess) return false;

                        if (did != deviceId)
                        {
                            _logger.LogWarning("리프레시 토큰의 deviceId 불일치");
                            return false;
                        }

                        var redis = _redisFactory.GetDatabase();
                        await redis.KeyDeleteAsync(RefreshToken.BuildRedisKey(userId, did));
                        return true;
                    }

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), "잘못된 토큰 타입");
            }
            return false;
        }

        public async Task<(string? AccessToken, string? RefreshToken)> ExchangeTokensAsync(string loginToken, string audience = "GameClient")
        {
            // JWT 로그인 토큰 파싱 후 해당 정보추출. 그 정보로 접속 및 갱신 토큰 생성.
            var jwtSettings = GetJwtSettings(audience);
            var parseResult = JwtHelper.ParseToken(loginToken, jwtSettings);

            // 검증
            if (!parseResult.IsValid || parseResult.Principal == null)
            {
                _logger.LogWarning("토큰 파싱 실패: {ErrorMessage}", parseResult.ErrorMessage);
                return (null, null);
            }

            var (typeSuccess, _) = TryExtractTokenType(parseResult.Principal, "login");
            if (!typeSuccess) return (null, null);

            var (userIdSuccess, userId) = TryExtractUserId(parseResult.Principal);
            if (!userIdSuccess) return (null, null);

            var (deviceIdSuccess, deviceId) = TryExtractDeviceId(parseResult.Principal);
            if (!deviceIdSuccess) return (null, null);

            // 새로운 엑세스 토큰 및 리프레시 토큰 생성 (같은 audience 사용)
            string? accessToken = await CreateToken(ITokenService.TokenType.Access, userId, deviceId, audience);
            if (accessToken == null)
            {
                _logger.LogError("액세스 토큰 생성 실패");
                return (null, null);
            }

            string? refreshToken = await CreateToken(ITokenService.TokenType.Refresh, userId, deviceId, audience);
            if (refreshToken == null)
            {
                _logger.LogError("리프레시 토큰 생성 실패");
                return (null, null);
            }

            return (accessToken, refreshToken);
        }

        /// <summary>
        /// 액세스 토큰 갱신 (리프레시 토큰으로부터 새로운 액세스 토큰 발급)
        /// </summary>
        public async Task<string?> RefreshAccessTokenAsync(string token, string audience = "GameClient")
        {
            // JWT 토큰 파싱 및 검증
            var jwtSettings = GetJwtSettings(audience);
            var parseResult = JwtHelper.ParseToken(token, jwtSettings);
            if (!parseResult.IsValid || parseResult.Principal == null)
            {
                _logger.LogWarning("토큰 파싱 실패: {ErrorMessage}", parseResult.ErrorMessage);
                return null;
            }

            var (typeSuccess, _) = TryExtractTokenType(parseResult.Principal, "refresh");
            if (!typeSuccess) return null;

            var (userIdSuccess, userId) = TryExtractUserId(parseResult.Principal);
            if (!userIdSuccess) return null;

            var (deviceIdSuccess, deviceId) = TryExtractDeviceId(parseResult.Principal);
            if (!deviceIdSuccess) return null;

            // 만료시간 확인 (JwtHelper.ParseToken이 이미 검증하지만 명시적 확인)
            var claim_exp = parseResult.Principal.FindFirst(JwtRegisteredClaimNames.Exp);
            if (claim_exp != null && long.TryParse(claim_exp.Value, out long expUnix))
            {
                DateTime exp = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                if (exp < DateTime.UtcNow)
                {
                    _logger.LogWarning("리프레시 토큰이 만료됨");
                    return null;
                }
            }

            // 새로운 엑세스 토큰 생성 (같은 audience 사용)
            string? newAT = await CreateToken(ITokenService.TokenType.Access, userId, deviceId, audience);
            return newAT;
        }

        /// <summary>
        /// 토큰 검증 (타입 자동 감지)
        /// </summary>
        public async Task<bool> ValidateTokenAsync(string token, string deviceId)
        {
            // 우선 Game으로 시도, 실패 시 Admin으로 시도
            var parseResult = JwtHelper.ParseToken(token, GetJwtSettings("GameClient"));
            if (!parseResult.IsValid)
            {
                parseResult = JwtHelper.ParseToken(token, GetJwtSettings("AdminPanel"));
            }

            if (!parseResult.IsValid || parseResult.Principal == null)
            {
                _logger.LogWarning("토큰 파싱 실패: {ErrorMessage}", parseResult.ErrorMessage);
                return false;
            }

            var claim_type = parseResult.Principal.FindFirst("type");
            if (claim_type == null)
            {
                _logger.LogWarning("토큰 타입 claim을 찾을 수 없음");
                return false;
            }

            return claim_type.Value switch
            {
                "access" => true, // Access 토큰은 상태가 없으므로 파싱 성공 시 유효함
                "login" => await ValidateLoginTokenInternalAsync(parseResult.Principal),
                "refresh" => await ValidateRefreshTokenInternalAsync(parseResult.Principal, deviceId),
                _ => false
            };
        }

        /// <summary>
        /// 토큰 검증 (타입 명시)
        /// </summary>
        public async Task<bool> ValidateTokenAsync(string token, string deviceId, ITokenService.TokenType type)
        {
            // 먼저 자동 감지 방식으로 토큰 검증
            if (!await ValidateTokenAsync(token, deviceId))
                return false;

            // 그 다음 타입이 예상과 일치하는지 확인
            // 우선 Game으로 시도, 실패 시 Admin으로 시도
            var parseResult = JwtHelper.ParseToken(token, GetJwtSettings("GameClient"));
            if (!parseResult.IsValid)
            {
                parseResult = JwtHelper.ParseToken(token, GetJwtSettings("AdminPanel"));
            }

            if (!parseResult.IsValid || parseResult.Principal == null)
                return false;

            string expectedType = type switch
            {
                ITokenService.TokenType.Login => "login",
                ITokenService.TokenType.Access => "access",
                ITokenService.TokenType.Refresh => "refresh",
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };

            var (typeSuccess, _) = TryExtractTokenType(parseResult.Principal, expectedType);
            return typeSuccess;
        }

        /// <summary>
        /// 로그인 토큰을 사용됨으로 표시 (1회용 토큰 방지)
        /// </summary>
        public async Task<bool> MarkLoginTokenAsUsedAsync(string token)
        {
            // 우선 Game으로 시도, 실패 시 Admin으로 시도
            var parseResult = JwtHelper.ParseToken(token, GetJwtSettings("GameClient"));
            if (!parseResult.IsValid)
            {
                parseResult = JwtHelper.ParseToken(token, GetJwtSettings("AdminPanel"));
            }

            if (!parseResult.IsValid || parseResult.Principal == null)
            {
                _logger.LogWarning("토큰 파싱 실패: {ErrorMessage}", parseResult.ErrorMessage);
                return false;
            }

            var (typeSuccess, _) = TryExtractTokenType(parseResult.Principal, "login");
            if (!typeSuccess) return false;

            var (jtiSuccess, jti) = TryExtractJti(parseResult.Principal);
            if (!jtiSuccess) return false;

            var redis = _redisFactory.GetDatabase();

            string activeKey = LogintToken.BuildActiveRedisKey(jti);
            bool wasActive = await redis.KeyDeleteAsync(activeKey);

            if (!wasActive)
            {
                _logger.LogWarning("활성 목록에 로그인 토큰이 없음. JTI: {Jti}", jti);
                return false;
            }

            string usedKey = LogintToken.BuildUsedRedisKey(jti);
            string usedValue = System.Text.Json.JsonSerializer.Serialize(new
            {
                jti,
                usedAt = DateTime.UtcNow,
                originalToken = token.Substring(0, Math.Min(20, token.Length)) + "..."
            });

            // audience 추출하여 적절한 retention 시간 사용
            var audienceClaim = parseResult.Principal.FindFirst("aud");
            var audience = audienceClaim?.Value ?? "GameClient";
            var jwtSettings = GetJwtSettings(audience);

            var retentionTime = TimeSpan.FromHours(jwtSettings.UsedLoginTokenRetentionHours);
            await redis.StringSetAsync(usedKey, usedValue, retentionTime);

            _logger.LogInformation("로그인 토큰을 사용됨으로 표시. JTI: {Jti}, 보관시간: {RetentionHours}시간", jti, jwtSettings.UsedLoginTokenRetentionHours);
            return true;
        }

        /// <summary>
        /// 특정 사용자의 모든 토큰 폐기 (로그아웃)
        /// </summary>
        public async Task<bool> RevokeAllUserTokensAsync(int userId, string deviceId)
        {
            try
            {
                var redis = _redisFactory.GetDatabase();

                // Refresh Token 삭제 (가장 중요)
                string refreshKey = RefreshToken.BuildRedisKey(userId, deviceId);
                bool refreshDeleted = await redis.KeyDeleteAsync(refreshKey);

                _logger.LogInformation("사용자 로그아웃: UserId={UserId}, DeviceId={DeviceId}, RefreshToken 삭제={RefreshDeleted}", userId, deviceId, refreshDeleted);

                // Access Token은 stateless이므로 Redis에 저장되지 않음
                // 만료될 때까지 유효하지만, Refresh Token이 없으면 갱신 불가

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "로그아웃 실패");
                return false;
            }
        }

        /// <summary>
        /// 토큰에서 UserId 추출 (검증 포함)
        /// </summary>
        public async Task<(bool Success, int UserId, string? ErrorMessage)> ExtractUserIdFromTokenAsync(string token, ITokenService.TokenType expectedType)
        {
            await Task.CompletedTask; // async 시그니처 유지

            // JWT 토큰 파싱 및 검증 (Game/Admin 모두 시도)
            var parseResult = JwtHelper.ParseToken(token, GetJwtSettings("GameClient"));
            if (!parseResult.IsValid)
            {
                parseResult = JwtHelper.ParseToken(token, GetJwtSettings("AdminPanel"));
            }

            if (!parseResult.IsValid || parseResult.Principal == null)
            {
                _logger.LogWarning("토큰 파싱 실패: {ErrorMessage}", parseResult.ErrorMessage);
                return (false, 0, "유효하지 않은 토큰입니다.");
            }

            // 토큰 타입 확인
            var tokenTypeClaim = parseResult.Principal.FindFirst("type");
            string expectedTypeStr = expectedType switch
            {
                ITokenService.TokenType.Login => "login",
                ITokenService.TokenType.Access => "access",
                ITokenService.TokenType.Refresh => "refresh",
                _ => throw new ArgumentOutOfRangeException(nameof(expectedType))
            };

            if (tokenTypeClaim == null || tokenTypeClaim.Value != expectedTypeStr)
            {
                _logger.LogWarning("토큰 타입 불일치. 예상: {ExpectedType}, 실제: {ActualType}", expectedTypeStr, tokenTypeClaim?.Value);
                return (false, 0, $"{expectedTypeStr} 토큰이 필요합니다.");
            }

            // userId 추출
            var userIdClaim = parseResult.Principal.FindFirst("userId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                _logger.LogWarning("토큰에서 userId claim을 찾을 수 없음");
                return (false, 0, "토큰에서 사용자 정보를 찾을 수 없습니다.");
            }

            return (true, userId, null);
        }

        #region Private Helper Methods

        /// <summary>
        /// ClaimsPrincipal에서 userId 추출
        /// </summary>
        private (bool Success, int UserId) TryExtractUserId(ClaimsPrincipal principal)
        {
            var claim = principal.FindFirst("userId");
            if (claim == null || !int.TryParse(claim.Value, out int userId))
            {
                _logger.LogWarning("토큰에서 userId claim을 찾을 수 없음");
                return (false, 0);
            }
            return (true, userId);
        }

        /// <summary>
        /// ClaimsPrincipal에서 deviceId 추출
        /// </summary>
        private (bool Success, string DeviceId) TryExtractDeviceId(ClaimsPrincipal principal)
        {
            var claim = principal.FindFirst("deviceId");
            if (claim == null)
            {
                _logger.LogWarning("토큰에서 deviceId claim을 찾을 수 없음");
                return (false, string.Empty);
            }
            return (true, claim.Value);
        }

        /// <summary>
        /// ClaimsPrincipal에서 JTI 추출
        /// </summary>
        private (bool Success, string Jti) TryExtractJti(ClaimsPrincipal principal)
        {
            var claim = principal.FindFirst(JwtRegisteredClaimNames.Jti);
            if (claim == null)
            {
                _logger.LogWarning("토큰에서 JTI claim을 찾을 수 없음");
                return (false, string.Empty);
            }
            return (true, claim.Value);
        }

        /// <summary>
        /// ClaimsPrincipal에서 토큰 타입 확인
        /// </summary>
        private (bool Success, string Type) TryExtractTokenType(ClaimsPrincipal principal, string expectedType)
        {
            var claim = principal.FindFirst("type");
            if (claim == null || claim.Value != expectedType)
            {
                _logger.LogWarning("토큰 타입 불일치. 예상: {ExpectedType}, 실제: {ActualType}", expectedType, claim?.Value);
                return (false, string.Empty);
            }
            return (true, claim.Value);
        }

        /// <summary>
        /// Login 토큰 검증 (Redis 확인 포함)
        /// </summary>
        private async Task<bool> ValidateLoginTokenInternalAsync(ClaimsPrincipal principal)
        {
            var (jtiSuccess, jti) = TryExtractJti(principal);
            if (!jtiSuccess) return false;

            var redis = _redisFactory.GetDatabase();

            string usedKey = LogintToken.BuildUsedRedisKey(jti);
            if (await redis.KeyExistsAsync(usedKey))
            {
                _logger.LogWarning("경고: 로그인 토큰 재사용 시도 감지! JTI: {Jti}. 이미 교환된 토큰입니다. 토큰 탈취 가능성!", jti);
                return false;
            }

            string activeKey = LogintToken.BuildActiveRedisKey(jti);
            if (!await redis.KeyExistsAsync(activeKey))
            {
                _logger.LogWarning("활성 토큰 목록에서 로그인 토큰을 찾을 수 없음. JTI: {Jti}", jti);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Refresh 토큰 검증 (Redis 확인 포함)
        /// </summary>
        private async Task<bool> ValidateRefreshTokenInternalAsync(ClaimsPrincipal principal, string deviceId)
        {
            var (userIdSuccess, userId) = TryExtractUserId(principal);
            if (!userIdSuccess) return false;

            var (deviceIdSuccess, did) = TryExtractDeviceId(principal);
            if (!deviceIdSuccess) return false;

            if (did != deviceId)
            {
                Console.WriteLine($"[TokenService] 리프레시 토큰의 deviceId 불일치");
                return false;
            }

            var redis = _redisFactory.GetDatabase();
            string redisKey = RefreshToken.BuildRedisKey(userId, did);

            if (!await redis.KeyExistsAsync(redisKey))
            {
                _logger.LogWarning("Redis에서 리프레시 토큰을 찾을 수 없음");
                return false;
            }

            return true;
        }

        #endregion
    }
}
