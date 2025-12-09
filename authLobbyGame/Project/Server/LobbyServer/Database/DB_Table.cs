using System.Reflection;
using MySql.Data.MySqlClient;

namespace LobbyServer.Database
{
    /// <summary>
    /// 테이블 데이터 저장용
    /// </summary>
    public sealed class DB_Table
    {
        //private Dictionary<int, FleetInfoData> m_dic_fleet_info = new Dictionary<int, FleetInfoData>();
        //private Dictionary<int, MapInfoData> m_dic_map_info = new Dictionary<int, MapInfoData>();
        //private Dictionary<int, MapPlanetInfoData> m_dic_map_planet_info = new Dictionary<int, MapPlanetInfoData>();
        //private Dictionary<int, MapRouteInfoData> m_dic_map_route_info = new Dictionary<int, MapRouteInfoData>();
        //private Dictionary<int, PlanetInfoData> m_dic_planet_info = new Dictionary<int, PlanetInfoData>();
        //private Dictionary<int, ProductionInfoData> m_dic_production_info = new Dictionary<int, ProductionInfoData>();
        //
        //public Dictionary<int, FleetInfoData> Fleet_info => m_dic_fleet_info;
        //public Dictionary<int, MapInfoData> Map_info => m_dic_map_info;
        //public Dictionary<int, MapPlanetInfoData> Map_Planet_info => m_dic_map_planet_info;
        //public Dictionary<int, MapRouteInfoData> Map_Route_info => m_dic_map_route_info;
        //public Dictionary<int, PlanetInfoData> Planet_info => m_dic_planet_info;
        //public Dictionary<int, ProductionInfoData> Production_info => m_dic_production_info;

        private void LogWithTimestamp(string message)
        {
            var timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            System.Console.WriteLine($"[{timestamp}] {message}");
        }

        private void LogErrorWithTimestamp(string message)
        {
            var timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            System.Console.WriteLine($"[{timestamp}] {message}");
        }

        public void UpdateTable(string _connectionString)
        {
            LoadAllTables(_connectionString);
        }

        // 제네릭 데이터 로더 메소드 - Convert 메소드가 없을 경우 로그 기록 및 빈 딕셔너리 반환
        private Dictionary<int, T> LoadDataTable<T>(string connectionString, string tableName)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    string query = $"SELECT * FROM {tableName}";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        // T 타입의 정적 메소드 호출을 위한 리플렉션
                        MethodInfo convertMethod = typeof(T).GetMethod("Convert",
                            BindingFlags.Public | BindingFlags.Static,
                            null,
                            new[] { typeof(MySqlDataReader) },
                            null);

                        if (convertMethod == null)
                        {
                            // Convert 메소드가 없는 경우 로그 기록
                            LogError($"Type {typeof(T).Name} does not have a static Convert method. Table {tableName} will not be loaded.");
                            return new Dictionary<int, T>();
                        }

                        try
                        {
                            // 메소드 호출 시도
                            return (Dictionary<int, T>)convertMethod.Invoke(null, new object[] { reader });
                        }
                        catch (Exception ex)
                        {
                            // 변환 중 오류 발생 시 로그 기록
                            LogError($"Error converting data for table {tableName}: {ex.Message}");
                            return new Dictionary<int, T>();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // DB 연결이나 쿼리 실행 중 오류 발생 시 로그 기록
                LogError($"Error loading table {tableName}: {ex.Message}");
                return new Dictionary<int, T>();
            }
        }

        // 로그 기록 메소드 (실제 로깅 시스템에 맞게 수정 필요)
        private void LogError(string message)
        {
            // 사용하는 로깅 시스템에 맞게 구현
            LogErrorWithTimestamp($"[ERROR] {DateTime.Now}: {message}");

            // 파일에 로깅하는 예시
            try
            {
                string logPath = "db_error.log";
                using (StreamWriter writer = File.AppendText(logPath))
                {
                    writer.WriteLine($"{DateTime.Now}: {message}");
                }
            }
            catch
            {
                // 로그 파일 작성 실패는 무시
            }
        }

        // 모든 테이블 로드 메소드 - 오류가 있어도 계속 진행
        private void LoadAllTables(string connectionString)
        {
            //try
            //{
            //    m_dic_fleet_info = LoadDataTable<FleetInfoData>(connectionString, "fleet_info");
            //    LogInfo($"Loaded {m_dic_fleet_info.Count} fleet info records");
            //}
            //catch (Exception ex)
            //{
            //    LogError($"Failed to load fleet_info table: {ex.Message}");
            //}
            //
            //try
            //{
            //    m_dic_map_info = LoadDataTable<MapInfoData>(connectionString, "map_info");
            //    LogInfo($"Loaded {m_dic_map_info.Count} map info records");
            //}
            //catch (Exception ex)
            //{
            //    LogError($"Failed to load map_info table: {ex.Message}");
            //}
            //
            //try
            //{
            //    m_dic_map_planet_info = LoadDataTable<MapPlanetInfoData>(connectionString, "map_planet_info");
            //    LogInfo($"Loaded {m_dic_map_planet_info.Count} map planet info records");
            //}
            //catch (Exception ex)
            //{
            //    LogError($"Failed to load map_planet_info table: {ex.Message}");
            //}
            //
            //// 나머지 테이블도 동일한 패턴으로 로드
            //try
            //{
            //    m_dic_map_route_info = LoadDataTable<MapRouteInfoData>(connectionString, "map_route_info");
            //    LogInfo($"Loaded {m_dic_map_route_info.Count} map route info records");
            //}
            //catch (Exception ex)
            //{
            //    LogError($"Failed to load map_route_info table: {ex.Message}");
            //}
            //
            //try
            //{
            //    m_dic_planet_info = LoadDataTable<PlanetInfoData>(connectionString, "planet_info");
            //    LogInfo($"Loaded {m_dic_planet_info.Count} planet info records");
            //}
            //catch (Exception ex)
            //{
            //    LogError($"Failed to load planet_info table: {ex.Message}");
            //}
            //
            //try
            //{
            //    m_dic_production_info = LoadDataTable<ProductionInfoData>(connectionString, "production_info");
            //    LogInfo($"Loaded {m_dic_production_info.Count} production info records");
            //}
            //catch (Exception ex)
            //{
            //    LogError($"Failed to load production_info table: {ex.Message}");
            //}
        }

        // 정보 로깅 메소드
        private void LogInfo(string message)
        {
            LogWithTimestamp($"[INFO] {DateTime.Now}: {message}");

            // 파일에 로깅하는 예시
            try
            {
                string logPath = "db_info.log";
                using (StreamWriter writer = File.AppendText(logPath))
                {
                    writer.WriteLine($"{DateTime.Now}: {message}");
                }
            }
            catch
            {
                // 로그 파일 작성 실패는 무시
            }
        }
    }
}