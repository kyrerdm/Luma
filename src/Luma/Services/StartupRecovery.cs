using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

namespace Luma.Services;

internal sealed record StartupRetry(ProcessStartInfo Start, TimeSpan Delay, int Attempt);
internal enum StartupOutcome { Ready, Retrying, Failed, RetryLaunchFailed }
internal enum StartupError { None, AudioOrCom, FileAccess, Windows, Initialization, Other }

internal static class StartupRecovery
{
    internal const int MaximumRetries = 3;
    private const string RetryArgument = "--startup-retry";
    private sealed record Health(Version Version, DateTimeOffset Utc, StartupOutcome Outcome, int Attempt, StartupError Error, int ErrorCode);

    internal static bool TryAcquire(Mutex mutex)
    {
        try { return mutex.WaitOne(0); }
        catch (AbandonedMutexException) { return true; }
    }

    internal static int Attempt(string[] args)
    {
        var index = Array.IndexOf(args, RetryArgument);
        if (index < 0) return 0;
        return index + 1 < args.Length && int.TryParse(args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            && value is >= 0 and <= MaximumRetries ? value : MaximumRetries;
    }

    internal static StartupRetry? Retry(string? executable, string[] args, Exception error, bool packaged = false)
    {
        var attempt = Attempt(args);
        if (packaged || Array.IndexOf(args, "--background") < 0 || Array.IndexOf(args, "--ready-event") >= 0 || attempt >= MaximumRetries
            || string.IsNullOrEmpty(executable) || !Path.IsPathFullyQualified(executable)
            || error is not (COMException or IOException or Win32Exception or InvalidOperationException)) return null;
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable), WindowStyle = ProcessWindowStyle.Hidden };
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] == RetryArgument) { index++; continue; }
            start.ArgumentList.Add(args[index]);
        }
        if (Array.IndexOf(args, "--wait-for-instance") < 0) start.ArgumentList.Add("--wait-for-instance");
        start.ArgumentList.Add(RetryArgument);
        start.ArgumentList.Add((attempt + 1).ToString(CultureInfo.InvariantCulture));
        return new(start, TimeSpan.FromSeconds(2 << attempt), attempt + 1);
    }

    internal static void Record(string directory, StartupOutcome outcome, int attempt, Exception? error = null)
    {
        var category = error switch { null => StartupError.None, COMException => StartupError.AudioOrCom,
            IOException => StartupError.FileAccess, Win32Exception => StartupError.Windows,
            InvalidOperationException => StartupError.Initialization, _ => StartupError.Other };
        try
        {
            Directory.CreateDirectory(directory);
            var target = Path.Combine(directory, "startup-health.json");
            var staged = Path.Combine(directory, ".startup-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(staged, JsonSerializer.Serialize(new Health(AppVersion.Current, DateTimeOffset.UtcNow,
                    outcome, Math.Clamp(attempt, 0, MaximumRetries), category, error?.HResult ?? 0)));
                File.Move(staged, target, true);
            }
            finally { if (File.Exists(staged)) File.Delete(staged); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
    }

    internal static string Summary(string directory)
    {
        try
        {
            var path = Path.Combine(directory, "startup-health.json");
            if (!File.Exists(path) || new FileInfo(path).Length > 4096) return "Background startup: no recorded attempt";
            var value = JsonSerializer.Deserialize<Health>(File.ReadAllText(path));
            if (value is null || !Enum.IsDefined(value.Outcome) || !Enum.IsDefined(value.Error) || value.Attempt is < 0 or > MaximumRetries) return "Background startup: record unavailable";
            return $"Background startup: {value.Outcome}; retries: {value.Attempt}; error: {value.Error}; code: 0x{value.ErrorCode:X8}; UTC: {value.Utc.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or System.Security.SecurityException) { return "Background startup: record unavailable"; }
    }
}
