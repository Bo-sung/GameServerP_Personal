using System;

namespace GMTool.Services.Logging
{
    public enum LogLevel
    {
        Debug,    // 🔍 디버그 (회색)
        Info,     // ℹ️ 정보 (파란색)
        Success,  // ✅ 성공 (초록색)
        Warning,  // ⚠️ 경고 (주황색)
        Error     // ❌ 에러 (빨간색)
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public string? Details { get; set; }

        public LogEntry(LogLevel level, string message, string? details = null)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message;
            Details = details;
        }

        // UI 표시용 포맷
        public string FormattedMessage =>
            $"[{Timestamp:HH:mm:ss}] {GetLevelIcon()} {Message}";

        private string GetLevelIcon() => Level switch
        {
            LogLevel.Debug => "🔍",
            LogLevel.Info => "ℹ️",
            LogLevel.Success => "✅",
            LogLevel.Warning => "⚠️",
            LogLevel.Error => "❌",
            _ => ""
        };
    }
}
