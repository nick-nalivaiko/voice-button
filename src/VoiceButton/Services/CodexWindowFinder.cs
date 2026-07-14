using System.Diagnostics;
using System.Windows.Automation;
using VoiceButton.Models;

namespace VoiceButton.Services;

public sealed class CodexWindowFinder(AppSettings? settings = null)
{
    public CodexWindow? FindBestWindow(AssistantAppKind? preferredApp = null)
    {
        var currentProcessId = Environment.ProcessId;
        var candidates = new List<CodexWindow>();
        var keywords = GetKeywords();
        var foregroundWindow = NativeMethods.GetForegroundWindow();

        NativeMethods.EnumWindows((handle, _) =>
        {
            if (!NativeMethods.IsWindowVisible(handle))
            {
                return true;
            }

            var title = NativeMethods.GetWindowTitle(handle);
            NativeMethods.GetWindowThreadProcessId(handle, out var processId);
            if (processId == 0 || processId == currentProcessId)
            {
                return true;
            }

            var (processName, processPath) = GetProcessInfo((int)processId);
            var appKind = ClassifyApp(title, processName, processPath, keywords);
            if (appKind is null)
            {
                return true;
            }

            try
            {
                var element = AutomationElement.FromHandle(handle);
                if (element is not null)
                {
                    candidates.Add(new CodexWindow(handle, title, processName, processPath, appKind.Value, element));
                }
            }
            catch
            {
                // Ignore windows that disappear during enumeration.
            }

            return true;
        }, IntPtr.Zero);

        var eligibleCandidates = preferredApp is null
            ? candidates
            : candidates.Where(candidate => candidate.AppKind == preferredApp.Value).ToList();

        return eligibleCandidates.FirstOrDefault(candidate => candidate.Handle == foregroundWindow)
            ?? eligibleCandidates.FirstOrDefault();
    }

    private static AssistantAppKind? ClassifyApp(
        string title,
        string processName,
        string processPath,
        IReadOnlyList<string> codexKeywords)
    {
        if (ContainsKeyword(processName, "codex-computer-use")
            || ContainsKeyword(title, "Cursor Overlay"))
        {
            return null;
        }

        if (ContainsKeyword(processPath, "OpenAI.Codex_")
            || ContainsKeyword(processPath, @"\OpenAI\Codex\")
            || string.Equals(processName, "Codex", StringComparison.OrdinalIgnoreCase))
        {
            return AssistantAppKind.Codex;
        }

        if (ContainsKeyword(processPath, "OpenAI.ChatGPT-Desktop_")
            || ContainsKeyword(processName, "ChatGPT Classic")
            || ContainsKeyword(title, "ChatGPT"))
        {
            return AssistantAppKind.ChatGPT;
        }

        if (codexKeywords.Any(keyword => ContainsKeyword(title, keyword) || ContainsKeyword(processName, keyword)))
        {
            return AssistantAppKind.Codex;
        }

        return null;
    }

    private static bool ContainsKeyword(string value, string keyword)
    {
        return value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<string> GetKeywords()
    {
        var value = settings?.CodexWindowKeywords;
        var keywords = (value ?? string.Empty)
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return keywords.Length == 0 ? ["Codex"] : keywords;
    }

    private static (string Name, string Path) GetProcessInfo(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var path = string.Empty;
            try
            {
                path = process.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                // Packaged apps can deny access to MainModule; title and process name remain usable.
            }

            return (process.ProcessName, path);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }
}

public enum AssistantAppKind
{
    Codex,
    ChatGPT
}

public sealed record CodexWindow(
    IntPtr Handle,
    string Title,
    string ProcessName,
    string ProcessPath,
    AssistantAppKind AppKind,
    AutomationElement Element)
{
    public string AppName => AppKind == AssistantAppKind.Codex ? "Codex" : "ChatGPT";
}
