using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System;
using System.IO;

namespace ZeroGdk.Server
{
    internal sealed class ZeroConsoleFormatter : ConsoleFormatter, IDisposable
    {
        private const string LoglevelPadding = ": ";

        private static readonly string _messagePadding = new string(' ', 9 + GetLogLevelString(LogLevel.Information).Length + LoglevelPadding.Length);
        private static readonly string _newLineWithMessagePadding = Environment.NewLine + _messagePadding;

        private readonly IDisposable? _optionsReloadToken;

        public ZeroConsoleFormatter(IOptionsMonitor<ConsoleFormatterOptions> options) : base(nameof(ZeroConsoleFormatter))
		{
			FormatterOptions = options.CurrentValue;
            _optionsReloadToken = options.OnChange(ReloadLoggerOptions);
        }

        private void ReloadLoggerOptions(ConsoleFormatterOptions options)
        {
            FormatterOptions = options;
        }

        public void Dispose()
        {
            _optionsReloadToken?.Dispose();
        }

        internal ConsoleFormatterOptions FormatterOptions { get; set; }

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
        {
            string message = logEntry.Formatter(logEntry.State, logEntry.Exception);
            if (logEntry.Exception == null && message == null)
            {
                return;
            }
            LogLevel logLevel = logEntry.LogLevel;
            ConsoleColors logLevelColors = GetLogLevelConsoleColors(logLevel);
            string logLevelString = GetLogLevelString(logLevel);

            var dateTimeOffset = GetCurrentDateTime();
            var timestamp = dateTimeOffset.ToString("HH:mm:ss ");
            if (timestamp != null)
            {
                textWriter.Write(timestamp);
            }
            if (logLevelString != null)
            {
                WriteColoredMessage(textWriter, logLevelString, logLevelColors.Background, logLevelColors.Foreground);
                textWriter.Write(LoglevelPadding);
            }
            CreateDefaultLogMessage(textWriter, logEntry, message, scopeProvider);
        }

        private static void CreateDefaultLogMessage<TState>(TextWriter textWriter, in LogEntry<TState> logEntry, string message, IExternalScopeProvider? scopeProvider)
        {
            // scope information
            WriteMessage(textWriter, message);

            var exception = logEntry.Exception;
            if (exception != null)
            {
                // exception message
                WriteMessage(textWriter, exception.ToString());
            }
        }

        private static string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "trce",
                LogLevel.Debug => "dbug",
                LogLevel.Information => "info",
                LogLevel.Warning => "warn",
                LogLevel.Error => "fail",
                LogLevel.Critical => "crit",
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
            };
        }

        private static void WriteColoredMessage(TextWriter textWriter, string message, ConsoleColor? background, ConsoleColor? foreground)
        {
            // Order: backgroundcolor, foregroundcolor, Message, reset foregroundcolor, reset backgroundcolor
            if (background.HasValue)
            {
                textWriter.Write(ColorCodes.GetBackgroundColorEscapeCode(background.Value));
            }
            if (foreground.HasValue)
            {
                textWriter.Write(ColorCodes.GetForegroundColorEscapeCode(foreground.Value));
            }
            textWriter.Write(message);
            if (foreground.HasValue)
            {
                textWriter.Write(ColorCodes.DefaultForegroundColor); // reset to default foreground color
            }
            if (background.HasValue)
            {
                textWriter.Write(ColorCodes.DefaultBackgroundColor); // reset to the background color
            }
        }

        private static void WriteMessage(TextWriter textWriter, string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                WriteReplacing(textWriter, "\n", _newLineWithMessagePadding, message);
                textWriter.Write(Environment.NewLine);
            }

            static void WriteReplacing(TextWriter writer, string oldValue, string newValue, string message)
            {
                string newMessage = message.Replace(oldValue, newValue);
                writer.Write(newMessage);
            }
        }

        private DateTimeOffset GetCurrentDateTime()
        {
            return FormatterOptions.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
        }

        private ConsoleColors GetLogLevelConsoleColors(LogLevel logLevel)
        {
            // We shouldn't be outputting color codes for Android/Apple mobile platforms,
            // they have no shell (adb shell is not meant for running apps) and all the output gets redirected to some log file.
            bool disableColors = !ConsoleUtils.EmitAnsiColorCodes;
            if (disableColors)
            {
                return new ConsoleColors(null, null);
            }
            // We must explicitly set the background color if we are setting the foreground color,
            // since just setting one can look bad on the users console.
            return logLevel switch
            {
                LogLevel.Trace => new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black),
                LogLevel.Debug => new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black),
                LogLevel.Information => new ConsoleColors(ConsoleColor.DarkGreen, ConsoleColor.Black),
                LogLevel.Warning => new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black),
                LogLevel.Error => new ConsoleColors(ConsoleColor.Black, ConsoleColor.DarkRed),
                LogLevel.Critical => new ConsoleColors(ConsoleColor.White, ConsoleColor.DarkRed),
                _ => new ConsoleColors(null, null)
            };
        }

        private readonly struct ConsoleColors
        {
            public ConsoleColors(ConsoleColor? foreground, ConsoleColor? background)
            {
                Foreground = foreground;
                Background = background;
            }

            public ConsoleColor? Foreground { get; }

            public ConsoleColor? Background { get; }
        }
    }
}
