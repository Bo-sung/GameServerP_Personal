using AuthServer.Data;
using AuthServer.Data.Repositories;
using AuthServer.Models;
using AuthServer.Settings;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AuthServer.Services.Tokens
{
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

            // Access token is stateless and not stored in Redis
            if (type == ITokenService.TokenType.Access)
                return token.GetTokenString();

            var redis = _redisFactory.GetDatabase();
            await redis.StringSetAsync(token.GetRedisKey(), token.GetRedisValue(), TimeSpan.FromSeconds(token.GetTTL()));

            return token.GetTokenString();
        }

        public async Task<bool> RevokeTokenAsync(string token, ITokenService.TokenType type)
        {
            // Parse and validate token
            var parseResult = JwtHelper.ParseToken(token, _jwtSettings);
            if (!parseResult.IsValid || parseResult.Principal == null)
            {
                Console.WriteLine($"[TokenService] Token parse failed: {parseResult.ErrorMessage}");
                return false;
            }

            switch (type)
            {
                case ITokenService.TokenType.Access:
                    {
                        // Access token is not stored in Redis, no revoke action needed
                        break;
                    }
                case ITokenService.TokenType.Login:
                    {
                        Claim? claim_type = parseResult.Principal.FindFirst("type");
                        if (claim_type == null || claim_type.Value != "login")
                        {
                            Console.WriteLine($"[TokenService] Token type mismatch for login token.");
                            return false;
                        }
                        Claim? claim_jti = parseResult.Principal.FindFirst(JwtRegisteredClaimNames.Jti);
                        if (claim_jti == null)
                        {
                            Console.WriteLine($"[TokenService] JTI claim not found in login token.");
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
                            Console.WriteLine($"[TokenService] Token type mismatch for refresh token.");
                            return false;
                        }
                        Claim? claim_userId = parseResult.Principal.FindFirst("userId");
                        if (claim_userId == null || !int.TryParse(claim_userId.Value, out int userId))
                        {
                            Console.WriteLine($"[TokenService] JTI claim not found in refresh token.");
                            return false;
                        }
                        Claim? claim_deviceId = parseResult.Principal.FindFirst("deviceId");
                        if (claim_deviceId == null)
                        {
                            Console.WriteLine($"[TokenService] JTI claim not found in refresh token.");
                            return false;
                        }
                        string deviceId = claim_deviceId.Value;
                        var redis = _redisFactory.GetDatabase();
                        await redis.KeyDeleteAsync(RefreshToken.BuildRedisKey(userId, deviceId));
                        return true;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), "Invalid token type");
            }
            return false;
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            // Parse and validate JWT token
            var parseResult = JwtHelper.ParseToken(token, _jwtSettings);
            if (!parseResult.IsValid || parseResult.Principal == null)
            {
                Console.WriteLine($"[TokenService] Token parse failed: {parseResult.ErrorMessage}");
                return false;
            }

            Claim? claim_type = parseResult.Principal.FindFirst("type");
            if (claim_type == null)
            {
                Console.WriteLine($"[TokenService] Token type mismatch for access token.");
                return false;
            }

            switch (claim_type.Value)
            {
                case "access":
                    {
                        // Access tokens are stateless; if parsing succeeded, it's valid
                    }
                    break;
                case "login":
                    {
                        Claim? claim_jti = parseResult.Principal.FindFirst(JwtRegisteredClaimNames.Jti);
                        if (claim_jti == null)
                        {
                            Console.WriteLine($"[TokenService] JTI claim not found in login token.");
                            return false;
                        }
                        string jti = claim_jti.Value;
                        var redis = _redisFactory.GetDatabase();

                        string usedKey = LogintToken.BuildUsedRedisKey(jti);
                        if (await redis.KeyExistsAsync(usedKey))
                        {
                            Console.WriteLine($"[TokenService] WARNING: Login token reuse attempt detected! JTI: {jti}");
                            Console.WriteLine($"[TokenService] This token was already exchanged. Possible token theft!");
                            return false;
                        }

                        string activeKey = LogintToken.BuildActiveRedisKey(jti);
                        if (!await redis.KeyExistsAsync(activeKey))
                        {
                            Console.WriteLine($"[TokenService] Login token not found in active tokens. JTI: {jti}");
                            return false;
                        }
                    }
                    break;
                case "refresh":
                    {
                        Claim? claim_userId = parseResult.Principal.FindFirst("userId");
                        if (claim_userId == null || !int.TryParse(claim_userId.Value, out int userId))
                        {
                            Console.WriteLine($"[TokenService] userId claim not found in refresh token.");
                            return false;
                        }
                        Claim? claim_deviceId = parseResult.Principal.FindFirst("deviceId");
                        if (claim_deviceId == null)
                        {
                            Console.WriteLine($"[TokenService] deviceId claim not found in refresh token.");
                            return false;
                        }
                        string deviceId = claim_deviceId.Value;

                        // Get Redis connection
                        var redis = _redisFactory.GetDatabase();

                        string redisKey = RefreshToken.BuildRedisKey(userId, deviceId);

                        // 레디스에 토큰이 존재하는지 확인
                        if (!await redis.KeyExistsAsync(redisKey))
                        {
                            Console.WriteLine($"[TokenService] Refresh token not found in Redis.");
                            return false;
                        }
                    }
                    break;
            }
            return true;
        }

        public async Task<bool> ValidateTokenAsync(string token, ITokenService.TokenType type)
        {
            // Parse and validate JWT token
            var parseResult = JwtHelper.ParseToken(token, _jwtSettings);
            if (!parseResult.IsValid || parseResult.Principal == null)
            {
                Console.WriteLine($"[TokenService] Token parse failed: {parseResult.ErrorMessage}");
                return false;
            }
            switch (type)
            {
                case ITokenService.TokenType.Access:
                    {
                        Claim? claim_type = parseResult.Principal.FindFirst("type");
                        if (claim_type == null || claim_type.Value != "access")
                        {
                            Console.WriteLine($"[TokenService] Token type mismatch for access token.");
                            return false;
                        }
                    }
                    break;
                case ITokenService.TokenType.Login:
                    {
                        Claim? claim_type = parseResult.Principal.FindFirst("type");
                        if (claim_type == null || claim_type.Value != "login")
                        {
                            Console.WriteLine($"[TokenService] Token type mismatch for login token.");
                            return false;
                        }
                        Claim? claim_jti = parseResult.Principal.FindFirst(JwtRegisteredClaimNames.Jti);
                        if (claim_jti == null)
                        {
                            Console.WriteLine($"[TokenService] JTI claim not found in login token.");
                            return false;
                        }
                        string jti = claim_jti.Value;
                        var redis = _redisFactory.GetDatabase();

                        string usedKey = LogintToken.BuildUsedRedisKey(jti);
                        if (await redis.KeyExistsAsync(usedKey))
                        {
                            Console.WriteLine($"[TokenService] WARNING: Login token reuse attempt detected! JTI: {jti}");
                            Console.WriteLine($"[TokenService] This token was already exchanged. Possible token theft!");
                            return false;
                        }

                        string activeKey = LogintToken.BuildActiveRedisKey(jti);
                        if (!await redis.KeyExistsAsync(activeKey))
                        {
                            Console.WriteLine($"[TokenService] Login token not found in active tokens. JTI: {jti}");
                            return false;
                        }
                    }
                    break;
                case ITokenService.TokenType.Refresh:
                    {
                        Claim? claim_type = parseResult.Principal.FindFirst("type");
                        if (claim_type == null || claim_type.Value != "refresh")
                        {
                            Console.WriteLine($"[TokenService] Token type mismatch for refresh token.");
                            return false;
                        }
                        Claim? claim_userId = parseResult.Principal.FindFirst("userId");
                        if (claim_userId == null || !int.TryParse(claim_userId.Value, out int userId))
                        {
                            Console.WriteLine($"[TokenService] userId claim not found in refresh token.");
                            return false;
                        }
                        Claim? claim_deviceId = parseResult.Principal.FindFirst("deviceId");
                        if (claim_deviceId == null)
                        {
                            Console.WriteLine($"[TokenService] deviceId claim not found in refresh token.");
                            return false;
                        }
                        string deviceId = claim_deviceId.Value;

                        // Get Redis connection
                        var redis = _redisFactory.GetDatabase();

                        string redisKey = RefreshToken.BuildRedisKey(userId, deviceId);

                        // 레디스에 토큰이 존재하는지 확인
                        if (!await redis.KeyExistsAsync(redisKey))
                        {
                            Console.WriteLine($"[TokenService] Refresh token not found in Redis.");
                            return false;
                        }
                    }break;
            }
            return true;
        }

        public async Task<bool> MarkLoginTokenAsUsedAsync(string token)
        {
            var parseResult = JwtHelper.ParseToken(token, _jwtSettings);
            if (!parseResult.IsValid || parseResult.Principal == null)
            {
                Console.WriteLine($"[TokenService] Token parse failed: {parseResult.ErrorMessage}");
                return false;
            }

            Claim? claim_type = parseResult.Principal.FindFirst("type");
            if (claim_type == null || claim_type.Value != "login")
            {
                Console.WriteLine($"[TokenService] Token type mismatch. Expected 'login' token.");
                return false;
            }

            Claim? claim_jti = parseResult.Principal.FindFirst(JwtRegisteredClaimNames.Jti);
            if (claim_jti == null)
            {
                Console.WriteLine($"[TokenService] JTI claim not found in login token.");
                return false;
            }
            string jti = claim_jti.Value;

            var redis = _redisFactory.GetDatabase();

            string activeKey = LogintToken.BuildActiveRedisKey(jti);
            bool wasActive = await redis.KeyDeleteAsync(activeKey);

            if (!wasActive)
            {
                Console.WriteLine($"[TokenService] Login token was not in active list. JTI: {jti}");
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

            Console.WriteLine($"[TokenService] Login token marked as used. JTI: {jti}, Retention: {_jwtSettings.UsedLoginTokenRetentionHours}h");
            return true;
        }
    }
}
