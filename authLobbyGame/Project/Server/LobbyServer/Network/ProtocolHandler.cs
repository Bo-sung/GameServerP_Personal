using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommonLib;

namespace LobbyServer.Network
{
	/// <summary>
	/// 프로토콜 핸들러 - 딕셔너리 기반 이벤트 처리
	/// </summary>
	public class ProtocolHandler
	{
		// 프로토콜 타입별 핸들러 델리게이트
		public delegate Task ProtocolHandlerDelegate(Protocol _protocol);

		// 핸들러 딕셔너리
		private Dictionary<int, ProtocolHandlerDelegate> m_handlers;

		public ProtocolHandler()
		{
			m_handlers = new Dictionary<int, ProtocolHandlerDelegate>();
		}

        private void LogWithTimestamp(string message)
        {
            var timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            System.Console.WriteLine($"[{timestamp}] {message}");
        }

        /// <summary>
        /// 핸들러 등록
        /// </summary>
        public void RegisterHandler(int _protocolType, ProtocolHandlerDelegate _handler)
		{
			if (m_handlers.ContainsKey(_protocolType))
			{
				LogWithTimestamp($"[ProtocolHandler] Warning: Handler for protocol type {_protocolType} already exists. Overwriting.");
			}

			m_handlers[_protocolType] = _handler;
		}

		/// <summary>
		/// 핸들러 등록 해제
		/// </summary>
		public void UnregisterHandler(int _protocolType)
		{
			m_handlers.Remove(_protocolType);
		}

		/// <summary>
		/// 프로토콜 처리
		/// </summary>
		public async Task HandleProtocol(Protocol _protocol)
		{
			if (_protocol == null)
				return;

			if (m_handlers.TryGetValue(_protocol.Type, out ProtocolHandlerDelegate handler))
			{
				try
				{
					await handler(_protocol);
				}
				catch (Exception e)
				{
					LogWithTimestamp($"[ProtocolHandler] Error handling protocol {_protocol.Type}: {e.Message}");
				}
			}
			else
			{
				LogWithTimestamp($"[ProtocolHandler] No handler registered for protocol type: {_protocol.Type}");
			}
		}

		/// <summary>
		/// 등록된 핸들러 개수
		/// </summary>
		public int HandlerCount => m_handlers.Count;

		/// <summary>
		/// 모든 핸들러 제거
		/// </summary>
		public void ClearHandlers()
		{
			m_handlers.Clear();
		}
	}
}