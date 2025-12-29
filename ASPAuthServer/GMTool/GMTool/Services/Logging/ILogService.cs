using System.Collections.ObjectModel;

namespace GMTool.Services.Logging
{
    public interface ILogService
    {
        ObservableCollection<LogEntry> Logs { get; }

        void Debug(string message, string? details = null);
        void Info(string message, string? details = null);
        void Success(string message, string? details = null);
        void Warning(string message, string? details = null);
        void Error(string message, string? details = null);
        void Error(Exception ex, string message);

        void Clear();
    }
}
