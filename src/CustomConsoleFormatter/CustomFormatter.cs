using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace CustomConsoleFormatter;

public static class ConsoleLoggerExtensions
{
    public static ILoggingBuilder AddCustomFormatter(
        this ILoggingBuilder builder,
        Action<CustomOptions> configure)
    {
        builder
            .AddConsole(options => options.FormatterName = "customName")
            .AddConsoleFormatter<CustomFormatter, CustomOptions>(configure);

        return builder;
    }
}

public sealed class CustomOptions : ConsoleFormatterOptions
{
    public string? CustomPrefix { get; set; } = "\"";

    public string? CustomSuffix { get; set; } = "\"";

    public AnsiColorTheme? Theme { get; set; } = AnsiColorThemes.Code;
}

public sealed class CustomFormatter : ConsoleFormatter, IDisposable
{
    private const string DefaultForegroundColor = "\x1B[39m\x1B[22m"; // reset to default foreground color
    private const string DefaultBackgroundColor = "\x1B[49m"; // reset to the background color
    private const string ConsoleThemeStyleString = "\x1b[38;5;0216m";
    private readonly IDisposable? optionsReloadToken;
    private CustomOptions formatterOptions;

    public CustomFormatter(IOptionsMonitor<CustomOptions> options) : base("customName")
    {
        optionsReloadToken = options.OnChange(options => formatterOptions = options);
        formatterOptions = options.CurrentValue;
    }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        Func<TextWriter, TState, Exception?, string?> formatter = (writer, state, exception) =>
        {
            if (state is IReadOnlyCollection<KeyValuePair<string, object>> stateProperties)
            {
                var messageFormat = stateProperties.FirstOrDefault(k => k.Key == "{OriginalFormat}").Value.ToString();
                var sb = new StringBuilder($"{GetForegroundColorEscapeCode(ConsoleColor.White)}{messageFormat}");

                foreach (var item in stateProperties)
                {
                    if (item.Key.Equals("{OriginalFormat}"))
                    {
                        continue;
                    }

                    var formattedItem = $"{GetForegroundColorEscapeCode(GetItemColor(item))}{formatterOptions.CustomPrefix}{item.Value}{formatterOptions.CustomSuffix}{GetForegroundColorEscapeCode(ConsoleColor.White)}";
                    sb.Replace($"{{{item.Key}}}", formattedItem);
                }

                return sb.ToString();
            }

            return state!.ToString();
        };

        var message = formatter.Invoke(textWriter, logEntry.State, logEntry.Exception);

        if (message is null)
        {
            return;
        }

        textWriter.Write(GetForegroundColorEscapeCode(ConsoleColor.Blue)); // reset to default foreground color
        textWriter.Write("[");
        textWriter.Write(DefaultForegroundColor); // reset to default foreground color
        WriteTimeStamp(textWriter);

        var logLevelString = GetLogLevelString(logEntry);
        if (logLevelString != null)
        {
            var logLevelColors = GetLogLevelConsoleColors(logEntry.LogLevel);
            WriteColoredMessage(textWriter, logLevelString, logLevelColors?.Foreground, logLevelColors?.Background);
        }

        textWriter.Write(GetForegroundColorEscapeCode(ConsoleColor.Blue)); // reset to default foreground color
        textWriter.Write("]");
        textWriter.Write(" ");
        textWriter.Write(message);
        textWriter.WriteLine();
    }

    public void Dispose() => optionsReloadToken?.Dispose();

    private void FormatLogEntry(TextWriter textWriter)
    {
        textWriter.Write(formatterOptions.CustomPrefix);
    }

    private void WriteTimeStamp(TextWriter textWriter)
    {
        var now = formatterOptions.UseUtcTimestamp
            ? DateTime.UtcNow
            : DateTime.Now;

        textWriter.Write($"""
            {now.ToString(formatterOptions.TimestampFormat)}
            """);
    }

    public static void WriteColoredString(StringBuilder sb, string message, ConsoleColor? foreground, ConsoleColor? background = null)
    {
        if (background.HasValue)
        {
            sb.Append(GetBackgroundColorEscapeCode(background.Value));
        }

        if (foreground.HasValue)
        {
            sb.Append(GetForegroundColorEscapeCode(foreground.Value));
        }

        sb.Append(message);

        if (foreground.HasValue)
        {
            sb.Append(DefaultForegroundColor); // reset to default foreground color
        }

        if (background.HasValue)
        {
            sb.Append(DefaultBackgroundColor); // reset to the background color
        }
    }

    public static void WriteColoredMessage(TextWriter textWriter, string message, ConsoleColor? foreground, ConsoleColor? background = null)
    {
        if (background.HasValue)
        {
            textWriter.Write(GetBackgroundColorEscapeCode(background.Value));
        }

        if (foreground.HasValue)
        {
            textWriter.Write(GetForegroundColorEscapeCode(foreground.Value));
        }

        textWriter.Write(message);
        if (foreground.HasValue)
        {
            textWriter.Write(DefaultForegroundColor); // reset to default foreground color
        }

        if (background.HasValue)
        {
            textWriter.Write(DefaultBackgroundColor); // reset to the background color
        }
    }

    private static string GetLogLevelString<TState>(LogEntry<TState> logEntry)
    {
        return logEntry.LogLevel switch
        {
            LogLevel.Information => "INF",
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DBG",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRI",
            _ => throw new NotImplementedException()
        };
    }

    private static string GetForegroundColorEscapeCode(ConsoleColor color) =>
        color switch
        {
            ConsoleColor.Black => "\x1B[30m",
            ConsoleColor.DarkRed => "\x1B[31m",
            ConsoleColor.DarkGreen => "\x1B[32m",
            ConsoleColor.DarkYellow => "\x1B[33m",
            ConsoleColor.DarkBlue => "\x1B[34m",
            ConsoleColor.DarkMagenta => "\x1B[35m",
            ConsoleColor.DarkCyan => "\x1B[36m",
            ConsoleColor.Gray => "\x1B[37m",
            ConsoleColor.Red => "\x1B[1m\x1B[31m",
            ConsoleColor.Green => "\x1B[1m\x1B[32m",
            ConsoleColor.Yellow => "\x1B[1m\x1B[33m",
            ConsoleColor.Blue => "\x1B[1m\x1B[34m",
            ConsoleColor.Magenta => "\x1B[1m\x1B[35m",
            ConsoleColor.Cyan => "\x1B[1m\x1B[36m",
            ConsoleColor.White => "\x1B[1m\x1B[37m",

            _ => DefaultForegroundColor
        };

    private static string GetBackgroundColorEscapeCode(ConsoleColor? color) =>
        color switch
        {
            ConsoleColor.Black => "\x1B[40m",
            ConsoleColor.DarkRed => "\x1B[41m",
            ConsoleColor.DarkGreen => "\x1B[42m",
            ConsoleColor.DarkYellow => "\x1B[43m",
            ConsoleColor.DarkBlue => "\x1B[44m",
            ConsoleColor.DarkMagenta => "\x1B[45m",
            ConsoleColor.DarkCyan => "\x1B[46m",
            ConsoleColor.Gray => "\x1B[47m",
            _ => DefaultBackgroundColor // Use default background color
        };

    private static (ConsoleColor Foreground, ConsoleColor? Background)? GetLogLevelConsoleColors(LogLevel logLevel)
    {
        // We must explicitly set the background color if we are setting the foreground color,
        // since just setting one can look bad on the users console.
        return logLevel switch
        {
            LogLevel.Trace => (ConsoleColor.Blue, ConsoleColor.Black),
            LogLevel.Debug => (ConsoleColor.Blue, ConsoleColor.Black),
            LogLevel.Information => (ConsoleColor.White, null),
            LogLevel.Warning => (ConsoleColor.Yellow, ConsoleColor.Black),
            LogLevel.Error => (ConsoleColor.Black, ConsoleColor.DarkRed),
            LogLevel.Critical => (ConsoleColor.White, ConsoleColor.DarkRed),
            _ => null
        };
    }

    private static string? ApplyTheme(KeyValuePair<string, object> item, AnsiColorTheme theme)
    {
        var result = item.Value switch
        {
            bool boolValue => WriteBoolean(boolValue),
            int intValue => WriteInt(intValue),
            string stringValue => WriteString(stringValue, theme),
            double doubleValue => WriteDouble(doubleValue),
            decimal decimalValue => WriteDecimal(decimalValue),
            DateTime dateTimeValue => WriteDateTime(dateTimeValue),
            TimeSpan timeSpanValue => WriteTimeSpan(timeSpanValue),
            Guid guidValue => WriteGuid(guidValue),
            _ => item.Value.ToString(),
        };

        return result;
    }

    private static ConsoleColor GetItemColor(KeyValuePair<string, object> item) =>
        item.Value switch
        {
            bool _ => ConsoleColor.Blue,
            int _ => ConsoleColor.DarkGreen,
            string _ => ConsoleColor.DarkYellow,
            double _ => ConsoleColor.DarkBlue,
            decimal _ => ConsoleColor.DarkMagenta,
            DateTime _ => ConsoleColor.DarkCyan,
            TimeSpan _ => ConsoleColor.Gray,
            Guid _ => ConsoleColor.Red,
            _ => ConsoleColor.White,
        };

    private static string WriteBoolean(bool value) => value.ToString();

    private static string WriteInt(int value) => WriteInt(value);

    private static string WriteString(string value, AnsiColorTheme theme)
    {
        //var stringStyle = theme.GetStyle(ThemeStyle.String);
        //var textStyle = theme.GetStyle(ThemeStyle.Text);

        return $"{ConsoleThemeStyleString}{value}{GetForegroundColorEscapeCode(ConsoleColor.White)}";
    }

    private static string WriteDouble(double value) => value.ToString();

    private static string WriteDecimal(decimal value) => value.ToString();

    private static string WriteDateTime(DateTime value) => value.ToString();

    private static string WriteTimeSpan(TimeSpan value) => value.ToString();

    private static string WriteGuid(Guid value) => value.ToString();
}
