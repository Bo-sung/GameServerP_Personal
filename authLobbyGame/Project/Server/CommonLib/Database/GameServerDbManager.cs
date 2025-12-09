using CommonLib.Database;
using MySql.Data.MySqlClient;
using System;
using System.Threading.Tasks;

namespace CommonLib.Database
{
    /// <summary>
    /// 게임 서버 테이블 전용 매니저
    /// </summary>
    public class GameServerDbManager : DbManager
    {
        public GameServerDbManager(string connectionString) : base(connectionString) { }

        public async Task<bool> RegisterOrUpdateServerAsync(string serverId, string serverName, string ipAddress, int tcpPort, int udpPort, int maxCapacity)
        {
            try
            {
                // 존재하는지 확인
                string selectQuery = "SELECT server_id FROM game_servers WHERE server_id = @id";
                var selectParam = new MySqlParameter("@id", serverId);
                var result = await ExecuteScalarAsync(selectQuery, selectParam);

                if (result == null)
                {
                    // 신규 등록
                    string insertQuery = @"INSERT INTO game_servers (server_id, server_name, ip_address, tcp_port, udp_port, max_capacity, current_load, status, registered_at, last_heartbeat)
VALUES (@id, @name, @ip, @tcp, @udp, @max, @load, @status, @reg, @hb)";
                    await ExecuteNonQueryAsync(insertQuery,
                        new MySqlParameter("@id", serverId),
                        new MySqlParameter("@name", serverName),
                        new MySqlParameter("@ip", ipAddress),
                        new MySqlParameter("@tcp", tcpPort),
                        new MySqlParameter("@udp", udpPort),
                        new MySqlParameter("@max", maxCapacity),
                        new MySqlParameter("@load", 0),
                        new MySqlParameter("@status", "online"),
                        new MySqlParameter("@reg", DateTime.UtcNow),
                        new MySqlParameter("@hb", DateTime.UtcNow));
                }
                else
                {
                    // 기존 데이터 업데이트
                    string updateQuery = "UPDATE game_servers SET server_name=@name, ip_address=@ip, tcp_port=@tcp, udp_port=@udp, max_capacity=@max, status=@status, last_heartbeat=@hb WHERE server_id=@id";
                    await ExecuteNonQueryAsync(updateQuery,
                        new MySqlParameter("@id", serverId),
                        new MySqlParameter("@name", serverName),
                        new MySqlParameter("@ip", ipAddress),
                        new MySqlParameter("@tcp", tcpPort),
                        new MySqlParameter("@udp", udpPort),
                        new MySqlParameter("@max", maxCapacity),
                        new MySqlParameter("@status", "online"),
                        new MySqlParameter("@hb", DateTime.UtcNow));
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameServerDbManager] RegisterOrUpdateServer error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendHeartbeatAsync(string serverId)
        {
            try
            {
                string updateQuery = "UPDATE game_servers SET last_heartbeat = @hb WHERE server_id = @id";
                await ExecuteNonQueryAsync(updateQuery,
                    new MySqlParameter("@hb", DateTime.UtcNow),
                    new MySqlParameter("@id", serverId));
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameServerDbManager] SendHeartbeat error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetStatusOfflineAsync(string serverId)
        {
            try
            {
                string updateQuery = "UPDATE game_servers SET status = @status WHERE server_id = @id";
                await ExecuteNonQueryAsync(updateQuery,
                    new MySqlParameter("@status", "offline"),
                    new MySqlParameter("@id", serverId));
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameServerDbManager] SetStatusOffline error: {ex.Message}");
                return false;
            }
        }
    }
}
