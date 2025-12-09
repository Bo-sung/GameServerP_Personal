using System;
using System.IO;
using System.Text.Json;

namespace CommonLib
{
    /// <summary>
    /// 애플리케이션 설정 관리 클래스
    /// appsettings.json에서 설정을 읽어옵니다.
    /// </summary>
    public class AppConfig : SingletonBase<AppConfig>
    {
        private ConfigData? _config;
        private readonly object _configLock = new object();

        /// <summary>
        /// 테이블 데이터베이스 연결 문자열 (자동 생성)
        /// </summary>
        public string TableDatabaseConnectionString
        {
            get
            {
                EnsureConfigLoaded();

                // ConnectionString이 직접 지정되어 있으면 그것을 사용
                if (!string.IsNullOrEmpty(_config?.Databases?.Table?.ConnectionString))
                {
                    return _config.Databases.Table.ConnectionString;
                }

                // 아니면 개별 항목으로 ConnectionString 생성
                var db = _config?.Databases?.Table;
                if (db != null)
                {
                    return $"server={db.Server ?? "localhost"};" +
                           $"user={db.UserId ?? "root"};" +
                           $"password={db.Password ?? ""};" +
                           $"database={db.DatabaseName ?? "interplanetery_tabledb_local"};" +
                           $"port={db.Port ?? 3306};";
                }

                return GetDefaultTableConnectionString();
            }
        }

        /// <summary>
        /// 인증 데이터베이스 연결 문자열 (자동 생성)
        /// </summary>
        public string AuthDatabaseConnectionString
        {
            get
            {
                EnsureConfigLoaded();

                // ConnectionString이 직접 지정되어 있으면 그것을 사용
                if (!string.IsNullOrEmpty(_config?.Databases?.Auth?.ConnectionString))
                {
                    return _config.Databases.Auth.ConnectionString;
                }

                // 아니면 개별 항목으로 ConnectionString 생성
                var db = _config?.Databases?.Auth;
                if (db != null)
                {
                    return $"server={db.Server ?? "localhost"};" +
                           $"user={db.UserId ?? "root"};" +
                           $"password={db.Password ?? ""};" +
                           $"database={db.DatabaseName ?? "interplanetery_authdb_local"};" +
                           $"port={db.Port ?? 3306};";
                }

                return GetDefaultAuthConnectionString();
            }
        }

        /// <summary>
        /// 데이터베이스 연결 문자열 (하위 호환성 - TableDatabaseConnectionString 반환)
        /// </summary>
        [Obsolete("Use TableDatabaseConnectionString or AuthDatabaseConnectionString instead")]
        public string DatabaseConnectionString => TableDatabaseConnectionString;

        /// <summary>
        /// 테이블 데이터베이스 서버 주소
        /// </summary>
        public string TableDatabaseServer
        {
            get
            {
                EnsureConfigLoaded();
                return _config?.Databases?.Table?.Server ?? "localhost";
            }
        }

        /// <summary>
        /// 테이블 데이터베이스 사용자 ID
        /// </summary>
        public string TableDatabaseUserId
        {
            get
            {
                EnsureConfigLoaded();
                return _config?.Databases?.Table?.UserId ?? "root";
            }
        }

        /// <summary>
        /// 테이블 데이터베이스 비밀번호
        /// </summary>
        public string TableDatabasePassword
        {
            get
            {
                EnsureConfigLoaded();
                return _config?.Databases?.Table?.Password ?? "";
            }
        }

        /// <summary>
        /// 테이블 데이터베이스 이름
        /// </summary>
        public string TableDatabaseName
        {
            get
            {
                EnsureConfigLoaded();
                return _config?.Databases?.Table?.DatabaseName ?? "interplanetery_tabledb_local";
            }
        }

        /// <summary>
        /// 테이블 데이터베이스 포트
        /// </summary>
        public int TableDatabasePort
        {
            get
            {
                EnsureConfigLoaded();
                return _config?.Databases?.Table?.Port ?? 3306;
            }
        }

        /// <summary>
        /// 인증 데이터베이스 서버 주소
        /// </summary>
        public string AuthDatabaseServer
        {
            get
            {
                EnsureConfigLoaded();
                return _config?.Databases?.Auth?.Server ?? "localhost";
            }
        }

        /// <summary>
        /// 인증 데이터베이스 사용자 ID
        /// </summary>
        public string AuthDatabaseUserId
        {
            get
            {
                EnsureConfigLoaded();
                return _config?.Databases?.Auth?.UserId ?? "root";
            }
        }

        /// <summary>
        /// 인증 데이터베이스 비밀번호
        /// </summary>
        public string AuthDatabasePassword
        {
            get
            {
                EnsureConfigLoaded();
                return _config?.Databases?.Auth?.Password ?? "";
            }
        }

        /// <summary>
        /// 인증 데이터베이스 이름
        /// </summary>
        public string AuthDatabaseName
        {
            get
            {
                EnsureConfigLoaded();
                return _config?.Databases?.Auth?.DatabaseName ?? "interplanetery_authdb_local";
            }
        }

        /// <summary>
        /// 인증 데이터베이스 포트
        /// </summary>
        public int AuthDatabasePort
        {
            get
            {
                EnsureConfigLoaded();
                return _config?.Databases?.Auth?.Port ?? 3306;
            }
        }

        /// <summary>
        /// 게임 서버 목록
        /// </summary>
        public List<GameServerConfig> GameServers
        {
            get
            {
                EnsureConfigLoaded();
                return _config?.GameServers ?? new List<GameServerConfig>();
            }
        }

        /// <summary>
        /// 로그 레벨
        /// </summary>
        public string LogLevel
        {
            get
            {
                EnsureConfigLoaded();
                return _config?.Logging?.LogLevel ?? "Information";
            }
        }

        /// <summary>
        /// 설정 파일 경로
        /// </summary>
        private string ConfigFilePath
        {
            get
            {
                // 실행 파일이 있는 디렉토리에서 appsettings.json 찾기
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(baseDir, "appsettings.json");
            }
        }

        /// <summary>
        /// 초기화 메서드
        /// </summary>
        protected override void OnInitialize()
        {
            LoadConfig();
        }

        /// <summary>
        /// 설정 파일을 로드합니다. 파일이 없으면 자동으로 생성합니다.
        /// </summary>
        public void LoadConfig()
        {
            lock (_configLock)
            {
                try
                {
                    string configPath = ConfigFilePath;
                    System.Diagnostics.Debug.WriteLine($"=== AppConfig LoadConfig ===");
                    System.Diagnostics.Debug.WriteLine($"Config file path: {configPath}");
                    System.Diagnostics.Debug.WriteLine($"File exists: {File.Exists(configPath)}");

                    if (!File.Exists(configPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[INFO] appsettings.json not found at: {configPath}");
                        System.Diagnostics.Debug.WriteLine("[INFO] Creating default configuration file...");
                        Console.WriteLine($"[INFO] Creating appsettings.json at: {configPath}");

                        _config = GetDefaultConfig();
                        CreateDefaultConfigFile(configPath);
                        LogCurrentConfig();
                        return;
                    }

                    string json = File.ReadAllText(configPath);
                    System.Diagnostics.Debug.WriteLine($"JSON content length: {json.Length} characters");
                    System.Diagnostics.Debug.WriteLine($"JSON content:\n{json}");

                    _config = JsonSerializer.Deserialize<ConfigData>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });

                    System.Diagnostics.Debug.WriteLine($"[INFO] Configuration loaded successfully from: {configPath}");
                    Console.WriteLine($"[INFO] Configuration loaded from: {configPath}");
                    LogCurrentConfig();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to load config: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
                    Console.WriteLine($"[ERROR] Failed to load config: {ex.Message}");
                    _config = GetDefaultConfig();
                    LogCurrentConfig();
                }
            }
        }

        /// <summary>
        /// 현재 설정값을 로그로 출력합니다.
        /// </summary>
        private void LogCurrentConfig()
        {
            if (_config?.Databases?.Table != null)
            {
                System.Diagnostics.Debug.WriteLine($"Databases.Table.Server: {_config.Databases.Table.Server}");
                System.Diagnostics.Debug.WriteLine($"Databases.Table.UserId: {_config.Databases.Table.UserId}");
                System.Diagnostics.Debug.WriteLine($"Databases.Table.DatabaseName: {_config.Databases.Table.DatabaseName}");
                System.Diagnostics.Debug.WriteLine($"Databases.Table.Port: {_config.Databases.Table.Port}");
                System.Diagnostics.Debug.WriteLine($"Generated Table ConnectionString: {TableDatabaseConnectionString}");
            }
            if (_config?.Databases?.Auth != null)
            {
                System.Diagnostics.Debug.WriteLine($"Databases.Auth.Server: {_config.Databases.Auth.Server}");
                System.Diagnostics.Debug.WriteLine($"Databases.Auth.UserId: {_config.Databases.Auth.UserId}");
                System.Diagnostics.Debug.WriteLine($"Databases.Auth.DatabaseName: {_config.Databases.Auth.DatabaseName}");
                System.Diagnostics.Debug.WriteLine($"Databases.Auth.Port: {_config.Databases.Auth.Port}");
                System.Diagnostics.Debug.WriteLine($"Generated Auth ConnectionString: {AuthDatabaseConnectionString}");
            }
        }

        /// <summary>
        /// 설정을 강제로 다시 로드합니다.
        /// </summary>
        public void Reload()
        {
            System.Diagnostics.Debug.WriteLine("=== AppConfig.Reload() called ===");
            LoadConfig();
        }

        /// <summary>
        /// 현재 설정을 appsettings.json 파일에 저장합니다.
        /// </summary>
        public void SaveConfig(ConfigData config)
        {
            lock (_configLock)
            {
                try
                {
                    string configPath = ConfigFilePath;
                    System.Diagnostics.Debug.WriteLine($"=== AppConfig SaveConfig ===");
                    System.Diagnostics.Debug.WriteLine($"Saving config to: {configPath}");

                    // JSON으로 직렬화 (들여쓰기 포함)
                    string json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    // 파일에 저장
                    File.WriteAllText(configPath, json);

                    // 내부 설정 업데이트
                    _config = config;

                    System.Diagnostics.Debug.WriteLine($"[INFO] Configuration saved successfully to: {configPath}");
                    Console.WriteLine($"[INFO] Configuration saved to: {configPath}");
                    LogCurrentConfig();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to save config: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
                    Console.WriteLine($"[ERROR] Failed to save config: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// 현재 설정 데이터를 반환합니다.
        /// </summary>
        public ConfigData GetCurrentConfig()
        {
            EnsureConfigLoaded();
            return _config ?? GetDefaultConfig();
        }

        /// <summary>
        /// 현재 메모리의 설정을 appsettings.json 파일에 덮어씁니다.
        /// </summary>
        public void OverwriteConfigFile()
        {
            lock (_configLock)
            {
                try
                {
                    EnsureConfigLoaded();

                    string configPath = ConfigFilePath;
                    System.Diagnostics.Debug.WriteLine($"=== AppConfig OverwriteConfigFile ===");
                    System.Diagnostics.Debug.WriteLine($"Overwriting config to: {configPath}");

                    if (_config == null)
                    {
                        throw new InvalidOperationException("No configuration loaded in memory.");
                    }

                    // JSON으로 직렬화 (들여쓰기 포함)
                    string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    // 파일에 덮어쓰기
                    File.WriteAllText(configPath, json);

                    System.Diagnostics.Debug.WriteLine($"[INFO] Configuration file overwritten successfully: {configPath}");
                    Console.WriteLine($"[INFO] Configuration file overwritten: {configPath}");
                    LogCurrentConfig();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to overwrite config file: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
                    Console.WriteLine($"[ERROR] Failed to overwrite config file: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// 설정이 로드되었는지 확인하고, 로드되지 않았으면 로드합니다.
        /// </summary>
        private void EnsureConfigLoaded()
        {
            if (_config == null)
            {
                LoadConfig();
            }
        }

        /// <summary>
        /// 기본 설정값 반환
        /// </summary>
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                Databases = new DatabasesConfig
                {
                    Table = new DatabaseConfig
                    {
                        Server = "localhost",
                        UserId = "root",
                        Password = "asdf1358@@",
                        DatabaseName = "interplanetery_tabledb_local",
                        Port = 3306
                    },
                    Auth = new DatabaseConfig
                    {
                        Server = "localhost",
                        UserId = "root",
                        Password = "asdf1358@@",
                        DatabaseName = "interplanetery_authdb_local",
                        Port = 3306
                    }
                },
                GameServers = new List<GameServerConfig>
                {
                    new GameServerConfig
                    {
                        Name = "로컬 서버",
                        Host = "127.0.0.1",
                        Port = 5000
                    },
                    new GameServerConfig
                    {
                        Name = "개발 서버",
                        Host = "192.168.1.100",
                        Port = 5000
                    },
                    new GameServerConfig
                    {
                        Name = "라이브 서버",
                        Host = "game.example.com",
                        Port = 5000
                    }
                },
                Logging = new LoggingConfig
                {
                    LogLevel = "Information"
                }
            };
        }

        /// <summary>
        /// 기본 설정 파일을 생성합니다.
        /// </summary>
        private void CreateDefaultConfigFile(string configPath)
        {
            try
            {
                // 디렉토리가 없으면 생성
                string directory = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // JSON으로 직렬화
                string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // 파일에 저장
                File.WriteAllText(configPath, json);

                System.Diagnostics.Debug.WriteLine($"[INFO] Default configuration file created successfully: {configPath}");
                Console.WriteLine($"[INFO] Default configuration file created at: {configPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to create default config file: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
                Console.WriteLine($"[ERROR] Failed to create config file: {ex.Message}");
                // 파일 생성 실패해도 계속 진행 (메모리 설정은 로드됨)
            }
        }

        /// <summary>
        /// 기본 테이블 DB 연결 문자열
        /// </summary>
        private string GetDefaultTableConnectionString()
        {
            return "server=localhost;user=root;password=asdf1358@@;database=interplanetery_tabledb_local;port=3306;";
        }

        /// <summary>
        /// 기본 인증 DB 연결 문자열
        /// </summary>
        private string GetDefaultAuthConnectionString()
        {
            return "server=localhost;user=root;password=asdf1358@@;database=interplanetery_authdb_local;port=3306;";
        }

        #region Config Data Classes

        public class ConfigData
        {
            public DatabasesConfig? Databases { get; set; }
            public List<GameServerConfig>? GameServers { get; set; }
            public LoggingConfig? Logging { get; set; }
        }

        public class DatabasesConfig
        {
            public DatabaseConfig? Table { get; set; }
            public DatabaseConfig? Auth { get; set; }
        }

        public class DatabaseConfig
        {
            /// <summary>
            /// 데이터베이스 서버 주소
            /// </summary>
            public string? Server { get; set; }

            /// <summary>
            /// 데이터베이스 사용자 ID
            /// </summary>
            public string? UserId { get; set; }

            /// <summary>
            /// 데이터베이스 비밀번호
            /// </summary>
            public string? Password { get; set; }

            /// <summary>
            /// 데이터베이스 이름
            /// </summary>
            public string? DatabaseName { get; set; }

            /// <summary>
            /// 데이터베이스 포트 (기본값: 3306)
            /// </summary>
            public int? Port { get; set; }

            /// <summary>
            /// 직접 연결 문자열 지정 (옵션, 이것이 있으면 우선 사용)
            /// </summary>
            public string? ConnectionString { get; set; }
        }

        public class GameServerConfig
        {
            /// <summary>
            /// 서버 이름 (표시용)
            /// </summary>
            public string Name { get; set; } = string.Empty;

            /// <summary>
            /// 서버 호스트 주소
            /// </summary>
            public string Host { get; set; } = string.Empty;

            /// <summary>
            /// 서버 포트
            /// </summary>
            public int Port { get; set; } = 5000;
        }

        public class LoggingConfig
        {
            public string LogLevel { get; set; } = "Information";
        }

        #endregion
    }
}
