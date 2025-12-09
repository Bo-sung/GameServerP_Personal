using LobbyServer.Core.Game.Entities;
using LobbyServer.Core.Game.Session;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LobbyServer.Core.Game.Managers
{
    /// <summary>
    /// 룸 관리 매니저 (싱글톤)
    /// </summary>
    public class RoomManager
    {
        private static RoomManager m_instance;
        private static readonly object m_lockObj = new object();

        private ConcurrentDictionary<string, Room> m_rooms;
        private int m_roomIdCounter = 0;
        private Timer m_cleanupTimer;

        private RoomManager()
        {
            m_rooms = new ConcurrentDictionary<string, Room>();

            // 60초마다 빈 룸 정리
            m_cleanupTimer = new Timer(CleanupEmptyRooms, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        }

        public static RoomManager Instance
        {
            get
            {
                if (m_instance == null)
                {
                    lock (m_lockObj)
                    {
                        if (m_instance == null)
                        {
                            m_instance = new RoomManager();
                        }
                    }
                }
                return m_instance;
            }
        }

        private void LogWithTimestamp(string message)
        {
            var timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            System.Console.WriteLine($"[{timestamp}] {message}");
        }

        /// <summary>
        /// 새로운 룸 생성
        /// </summary>
        public Room CreateRoom()
        {
            int roomId = Interlocked.Increment(ref m_roomIdCounter);
            string roomIdString = $"ROOM_{roomId:D4}";

            Room room = new Room(roomIdString);

            if (m_rooms.TryAdd(roomIdString, room))
            {
                LogWithTimestamp($"[RoomManager] Room created: {roomIdString}");
                return room;
            }

            return null;
        }

        /// <summary>
        /// 룸 ID로 룸 가져오기
        /// </summary>
        public Room? GetRoom(string _roomId)
        {
            m_rooms.TryGetValue(_roomId, out Room room);
            return room;
        }

        /// <summary>
        /// 사용 가능한 룸 찾기 (빈 자리가 있는 룸)
        /// </summary>
        public Room FindAvailableRoom()
        {
            foreach (var kvp in m_rooms)
            {
                if (!kvp.Value.IsFull)
                {
                    return kvp.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// 룸 제거
        /// </summary>
        public bool RemoveRoom(string _roomId)
        {
            if (m_rooms.TryRemove(_roomId, out Room room))
            {
                LogWithTimestamp($"[RoomManager] Room removed: {_roomId}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 빈 룸 자동 정리
        /// </summary>
        private void CleanupEmptyRooms(object _state)
        {
            var emptyRooms = m_rooms.Where(kvp => kvp.Value.IsEmpty).ToList();

            foreach (var kvp in emptyRooms)
            {
                if (m_rooms.TryRemove(kvp.Key, out Room room))
                {
                    LogWithTimestamp($"[RoomManager] Cleaned up empty room: {kvp.Key}");
                }
            }

            if (emptyRooms.Count > 0)
            {
                LogWithTimestamp($"[RoomManager] Total rooms: {m_rooms.Count}");
            }
        }

        /// <summary>
        /// 모든 룸 리스트 가져오기 (RoomInfo 배열)
        /// </summary>
        public CommonLib.RoomInfo[] GetRoomList()
        {
            return m_rooms.Values.Select(room => room.RoomInfo).ToArray();
        }

        /// <summary>
        /// 페이지네이션된 룸 리스트 가져오기
        /// </summary>
        /// <param name="page">페이지 번호 (0이면 전체)</param>
        /// <param name="pageSize">페이지당 아이템 수</param>
        public CommonLib.RoomInfo[] GetRoomList(int page, int pageSize = 10)
        {
            if (page <= 0)
            {
                // 페이지 0이면 전체 반환
                return GetRoomList();
            }

            return m_rooms.Values
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(room => room.RoomInfo)
                .ToArray();
        }

        /// <summary>
        /// 현재 룸 상태 출력
        /// </summary>
        public void PrintStatus()
        {
            LogWithTimestamp($"[RoomManager] Total rooms: {m_rooms.Count}");
            foreach (var kvp in m_rooms)
            {
                LogWithTimestamp($"  - {kvp.Key}: {kvp.Value.PlayerCount}/{Room.MaxPlayers} players");
            }
        }

        /// <summary>
        /// 매니저 종료 (정리)
        /// </summary>
        public void Shutdown()
        {
            m_cleanupTimer?.Dispose();

            foreach (var kvp in m_rooms)
            {
                kvp.Value.CloseAllConnections();
            }

            m_rooms.Clear();
            LogWithTimestamp("[RoomManager] Shutdown complete");
        }

    }
}
