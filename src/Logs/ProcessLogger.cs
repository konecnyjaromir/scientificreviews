using System;
using System.Diagnostics;

namespace ScientificReviews.Logs
{
    public static class ProcessLogger
    {
        public static ProcessLogScope Begin(string processName, string details = null)
        {
            return new ProcessLogScope(processName, details);
        }
    }

    public sealed class ProcessLogScope : IDisposable
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly string _processName;
        private readonly string _instanceId;
        private bool _finished;

        internal ProcessLogScope(string processName, string details)
        {
            _processName = string.IsNullOrWhiteSpace(processName) ? "Process" : processName.Trim();
            _instanceId = Guid.NewGuid().ToString("N").Substring(0, 8);
            Write("START", details, AppLog.MessageType.Info);
        }

        public void Step(string message, AppLog.MessageType type = AppLog.MessageType.Info)
        {
            Write("STEP", message, type);
        }

        public void Complete(string message = null)
        {
            if (_finished)
                return;

            _finished = true;
            string summary = string.IsNullOrWhiteSpace(message)
                ? $"Completed in {_stopwatch.Elapsed}."
                : $"{message} Completed in {_stopwatch.Elapsed}.";
            Write("COMPLETE", summary, AppLog.MessageType.Info);
        }

        public void Fail(Exception exception, string message = null)
        {
            if (_finished)
                return;

            _finished = true;
            string summary = string.IsNullOrWhiteSpace(message) ? "Failed." : message.Trim();
            if (exception != null)
                summary += " " + exception.Message;

            summary += $" Elapsed {_stopwatch.Elapsed}.";
            Write("FAIL", summary, AppLog.MessageType.Error);
        }

        public void Fail(string message)
        {
            Fail(null, message);
        }

        public void Dispose()
        {
            if (_finished)
                return;

            _finished = true;
            Write("DISPOSE", $"Disposed after {_stopwatch.Elapsed} without explicit completion.", AppLog.MessageType.Exclamation);
        }

        private void Write(string stage, string message, AppLog.MessageType type)
        {
            string suffix = string.IsNullOrWhiteSpace(message) ? string.Empty : " | " + message.Trim();
            AppLog.Log($"[{_processName}#{_instanceId}] {stage}{suffix}", type);
        }
    }
}
