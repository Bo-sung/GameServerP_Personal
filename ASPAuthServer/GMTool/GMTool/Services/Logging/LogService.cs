using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace GMTool.Services.Logging
{
    public class LogService : ILogService
    {
        private const int MAX_LOG_COUNT = 500;

        public ObservableCollection<LogEntry> Logs { get; }

        public LogService()
        {
            Logs = new ObservableCollection<LogEntry>();
        }

        public void Debug(string message, string? details = null)
        {
            AddLog(LogLevel.Debug, message, details);
        }

        public void Info(string message, string? details = null)
        {
            AddLog(LogLevel.Info, message, details);
        }

        public void Success(string message, string? details = null)
        {
            AddLog(LogLevel.Success, message, details);
        }

        public void Warning(string message, string? details = null)
        {
            AddLog(LogLevel.Warning, message, details);
        }

        public void Error(string message, string? details = null)
        {
            AddLog(LogLevel.Error, message, details);
        }

        public void Error(Exception ex, string message)
        {
            var details = $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
            AddLog(LogLevel.Error, message, details);
        }

        public void Clear()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Logs.Clear();
            });
        }

        private void AddLog(LogLevel level, string message, string? details = null)
        {
            var logEntry = new LogEntry(level, message, details);

            // UI 스레드에서 실행
            Application.Current.Dispatcher.Invoke(() =>
            {
                Logs.Insert(0, logEntry);  // 최신 로그가 위로

                // 최대 개수 제한 (성능)
                while (Logs.Count > MAX_LOG_COUNT)
                {
                    Logs.RemoveAt(Logs.Count - 1);
                }
            });
        }
    }
}
