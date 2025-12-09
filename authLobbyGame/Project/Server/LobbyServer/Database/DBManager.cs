using LobbyServer.Utils;

namespace LobbyServer.Database
{
    public sealed class DBManager : SingletonBase<DBManager>
    {
        private readonly object _Updatelock = new object();
        private DB_Table m_db_Table = new DB_Table();
        private DB_Auth m_db_auth = new DB_Auth();
        public DB_Table Table => m_db_Table;
        public DB_Auth Auth => m_db_auth;

        public void UpdateTable()
        {
            lock (_Updatelock)
            {
                // AppConfig에서 테이블 DB 연결 문자열 가져오기
                string connectionString = CommonLib.AppConfig.Instance.TableDatabaseConnectionString;
                m_db_Table.UpdateTable(connectionString);
            }
        }
    }
}
