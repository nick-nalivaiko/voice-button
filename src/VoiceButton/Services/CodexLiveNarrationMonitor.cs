using System.Text.RegularExpressions;
using System.Windows.Automation;
using WpfRect = System.Windows.Rect;

namespace VoiceButton.Services;

public sealed record LiveNarrationParagraph(int Index, string Text);

public sealed record LiveNarrationSnapshot(
    string SessionId,
    IReadOnlyList<LiveNarrationParagraph> Paragraphs,
    bool IsWorking)
{
    public static LiveNarrationSnapshot Empty { get; } = new(string.Empty, [], false);
}

public sealed class CodexLiveNarrationMonitor(
    CodexWindowFinder windowFinder,
    DiagnosticsLogService diagnosticsLog) : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(850);
    private static readonly TimeSpan SessionDisappearDelay = TimeSpan.FromSeconds(4);

    private readonly object _gate = new();
    private CancellationTokenSource? _run;
    private Task? _monitorTask;
    private LiveNarrationSnapshot _snapshot = LiveNarrationSnapshot.Empty;
    private string? _logicalSessionId;
    private string? _trackedScopeKey;
    private List<string> _observedParagraphs = [];
    private readonly List<LiveNarrationParagraph> _publishedParagraphs = [];
    private DateTime _lastSessionSeenUtc;
    private bool _lastExtractionWasWorking;
    private string? _lastLoggedError;
    private DateTime _lastErrorLoggedUtc;
    private bool _missingWindowLogged;

    public event EventHandler<LiveNarrationSnapshot>? SnapshotChanged;

    public LiveNarrationSnapshot CurrentSnapshot
    {
        get
        {
            lock (_gate)
            {
                return _snapshot;
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_run is not null)
            {
                return;
            }

            _run = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorAsync(_run.Token));
        }

        diagnosticsLog.Info("Codex live narration monitor", "started");
    }

    public void Stop()
    {
        CancellationTokenSource? run;
        lock (_gate)
        {
            run = _run;
            _run = null;
            _monitorTask = null;
        }

        run?.Cancel();
        run?.Dispose();
        ResetSession(publish: true);
    }

    public void Dispose()
    {
        Stop();
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var extraction = TryExtractCurrentActivity();
                UpdateObservation(extraction);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogMonitorError(ex);
            }

            try
            {
                await Task.Delay(PollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private LiveExtraction? TryExtractCurrentActivity()
    {
        var window = windowFinder.FindBestWindow(AssistantAppKind.Codex);
        CodexWindow? fallback = null;
        if (window is null)
        {
            fallback = windowFinder.FindBestWindow();
            if (fallback is not null && LooksLikeCodexWindow(fallback))
            {
                window = fallback;
            }
        }

        if (window is null)
        {
            if (!_missingWindowLogged)
            {
                _missingWindowLogged = true;
                var fallbackDetail = fallback is null
                    ? "none"
                    : $"kind={fallback.AppKind}, process={fallback.ProcessName}, title='{fallback.Title}'";
                diagnosticsLog.Info(
                    "Codex live narration monitor",
                    $"Codex window not found; fallback={fallbackDetail}");
            }

            return null;
        }

        _missingWindowLogged = false;

        AutomationElementCollection descendants;
        try
        {
            descendants = window.Element.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        }
        catch
        {
            return null;
        }

        var activeMarkers = FindMarkers(descendants, LooksLikeActiveMarker);
        if (activeMarkers.Count > 0)
        {
            var marker = activeMarkers[^1];
            var scope = FindResponseScope(window.Element, marker);
            return new LiveExtraction(
                GetRuntimeKey(scope),
                ExtractParagraphs(scope, marker),
                true);
        }

        if (string.IsNullOrWhiteSpace(_trackedScopeKey))
        {
            return null;
        }

        var completedMarkers = FindMarkers(descendants, LooksLikeCompletedMarker);
        for (var index = completedMarkers.Count - 1; index >= 0; index--)
        {
            var marker = completedMarkers[index];
            var scope = FindResponseScope(window.Element, marker);
            var scopeKey = GetRuntimeKey(scope);
            if (string.Equals(scopeKey, _trackedScopeKey, StringComparison.Ordinal))
            {
                return new LiveExtraction(scopeKey, ExtractParagraphs(scope, marker), false);
            }
        }

        return null;
    }

    private void UpdateObservation(LiveExtraction? extraction)
    {
        var now = DateTime.UtcNow;
        if (extraction is null)
        {
            if (_logicalSessionId is not null
                && now - _lastSessionSeenUtc >= SessionDisappearDelay)
            {
                ResetSession(publish: true);
            }

            return;
        }

        var paragraphs = extraction.Paragraphs;
        if (!extraction.IsWorking
            && _lastExtractionWasWorking
            && _observedParagraphs.Count > 0
            && !SequencesOverlap(_observedParagraphs, paragraphs))
        {
            paragraphs = [
                .. _observedParagraphs,
                .. paragraphs.Where(paragraph => !_observedParagraphs.Contains(paragraph, StringComparer.Ordinal))
            ];
        }

        var needsNewSession = _logicalSessionId is null
            || (extraction.IsWorking
                && !_lastExtractionWasWorking
                && !SequencesOverlap(_observedParagraphs, paragraphs))
            || (!string.Equals(extraction.ScopeKey, _trackedScopeKey, StringComparison.Ordinal)
                && !SequencesOverlap(_observedParagraphs, paragraphs));

        if (needsNewSession)
        {
            BeginSession(extraction.ScopeKey, extraction.IsWorking, now);
        }
        else
        {
            _trackedScopeKey = extraction.ScopeKey;
        }

        _lastSessionSeenUtc = now;
        _lastExtractionWasWorking = extraction.IsWorking;

        if (!SequenceEquals(_observedParagraphs, paragraphs))
        {
            _observedParagraphs = [.. paragraphs];
        }

        // While Codex is working, only a following paragraph proves that the current one is complete.
        // This deliberately trades a small delay for never narrating a paragraph in partial fragments.
        var publishCount = Math.Max(0, paragraphs.Count - 1);
        if (!extraction.IsWorking)
        {
            publishCount = paragraphs.Count;
        }

        var changed = false;
        for (var index = _publishedParagraphs.Count; index < publishCount; index++)
        {
            _publishedParagraphs.Add(new LiveNarrationParagraph(index, paragraphs[index]));
            changed = true;
        }

        if (changed || _snapshot.IsWorking != extraction.IsWorking)
        {
            PublishSnapshot(extraction.IsWorking);
        }
    }

    private void BeginSession(string scopeKey, bool isWorking, DateTime now)
    {
        _logicalSessionId = Guid.NewGuid().ToString("N");
        _trackedScopeKey = scopeKey;
        _observedParagraphs.Clear();
        _publishedParagraphs.Clear();
        _lastSessionSeenUtc = now;
        _lastExtractionWasWorking = isWorking;
        diagnosticsLog.Info(
            "Codex live narration session",
            $"session={_logicalSessionId}, scope={scopeKey}, working={isWorking}");
        PublishSnapshot(isWorking);
    }

    private void ResetSession(bool publish)
    {
        _logicalSessionId = null;
        _trackedScopeKey = null;
        _observedParagraphs.Clear();
        _publishedParagraphs.Clear();
        _lastSessionSeenUtc = default;
        _lastExtractionWasWorking = false;

        if (publish)
        {
            lock (_gate)
            {
                _snapshot = LiveNarrationSnapshot.Empty;
            }

            SnapshotChanged?.Invoke(this, LiveNarrationSnapshot.Empty);
        }
    }

    private void PublishSnapshot(bool isWorking)
    {
        if (_logicalSessionId is null)
        {
            return;
        }

        var snapshot = new LiveNarrationSnapshot(
            _logicalSessionId,
            _publishedParagraphs.ToArray(),
            isWorking);
        lock (_gate)
        {
            _snapshot = snapshot;
        }

        SnapshotChanged?.Invoke(this, snapshot);
        diagnosticsLog.Info(
            "Codex live narration snapshot",
            $"session={snapshot.SessionId}, paragraphs={snapshot.Paragraphs.Count}, working={snapshot.IsWorking}");
    }

    private static List<AutomationElement> FindMarkers(
        AutomationElementCollection descendants,
        Func<string, bool> predicate)
    {
        var markers = new List<AutomationElement>();
        foreach (AutomationElement element in descendants)
        {
            var name = SafeName(element);
            if (predicate(name))
            {
                markers.Add(element);
            }
        }

        return markers;
    }

    private static AutomationElement FindResponseScope(AutomationElement root, AutomationElement marker)
    {
        var rootBounds = SafeBounds(root);
        var current = marker;
        var fallback = marker;

        for (var depth = 0; depth < 9; depth++)
        {
            AutomationElement? parent;
            try
            {
                parent = TreeWalker.ControlViewWalker.GetParent(current);
            }
            catch
            {
                break;
            }

            if (parent is null || Automation.Compare(parent, root))
            {
                break;
            }

            current = parent;
            fallback = parent;
            var bounds = SafeBounds(parent);
            if (bounds.IsEmpty)
            {
                continue;
            }

            if (bounds.Width >= Math.Min(360, rootBounds.Width * 0.25)
                && bounds.Height >= 60
                && (rootBounds.IsEmpty || bounds.Height <= rootBounds.Height * 0.86))
            {
                fallback = parent;
                if (CountTextAfterMarker(parent, marker) > 0)
                {
                    return parent;
                }
            }
        }

        return fallback;
    }

    private static int CountTextAfterMarker(AutomationElement scope, AutomationElement marker)
    {
        var elements = FindAllElements(scope);
        var markerSeen = false;
        var count = 0;
        foreach (AutomationElement element in elements)
        {
            if (!markerSeen)
            {
                markerSeen = Automation.Compare(element, marker);
                continue;
            }

            var type = SafeControlType(element);
            if (IsNarrationBoundary(SafeName(element)))
            {
                break;
            }

            if ((type == ControlType.Text || type == ControlType.Document)
                && IsNarrationText(SafeName(element)))
            {
                count++;
            }
        }

        return count;
    }

    private static List<string> ExtractParagraphs(AutomationElement scope, AutomationElement marker)
    {
        var elements = FindAllElements(scope);
        var markerSeen = false;
        var fragments = new List<NarrationFragment>();
        var unique = new HashSet<string>(StringComparer.Ordinal);

        foreach (AutomationElement element in elements)
        {
            if (!markerSeen)
            {
                markerSeen = Automation.Compare(element, marker);
                continue;
            }

            if (HasExcludedAncestor(element, scope))
            {
                continue;
            }

            var name = SafeName(element);
            if (IsNarrationBoundary(name))
            {
                break;
            }

            var type = SafeControlType(element);
            if (type != ControlType.Text && type != ControlType.Document)
            {
                continue;
            }

            var segments = SplitParagraphs(name).ToArray();
            for (var index = 0; index < segments.Length; index++)
            {
                var segment = segments[index];
                if (IsNarrationText(segment) && unique.Add(segment))
                {
                    fragments.Add(new NarrationFragment(
                        segment,
                        SafeBounds(element),
                        GetTextContainerKey(element, scope),
                        ForceBreakBefore: index > 0));
                }
            }
        }

        return MergeInlineFragments(fragments);
    }

    private static List<AutomationElement> FindAllElements(AutomationElement scope)
    {
        try
        {
            var elements = scope.FindAll(TreeScope.Descendants, Condition.TrueCondition);
            return elements.Cast<AutomationElement>().ToList();
        }
        catch
        {
            return [];
        }
    }

    private static bool HasExcludedAncestor(AutomationElement element, AutomationElement scope)
    {
        var current = element;
        for (var depth = 0; depth < 7; depth++)
        {
            AutomationElement? parent;
            try
            {
                parent = TreeWalker.ControlViewWalker.GetParent(current);
            }
            catch
            {
                return false;
            }

            if (parent is null || Automation.Compare(parent, scope))
            {
                return false;
            }

            var type = SafeControlType(parent);
            if (type == ControlType.Button || type == ControlType.Edit || type == ControlType.ComboBox)
            {
                return true;
            }

            var parentName = SafeName(parent);
            if (IsNarrationBoundary(parentName) || IsServiceActivityText(parentName))
            {
                return true;
            }

            current = parent;
        }

        return false;
    }

    private static string GetTextContainerKey(AutomationElement element, AutomationElement scope)
    {
        var current = element;
        for (var depth = 0; depth < 4; depth++)
        {
            AutomationElement? parent;
            try
            {
                parent = TreeWalker.ControlViewWalker.GetParent(current);
            }
            catch
            {
                break;
            }

            if (parent is null || Automation.Compare(parent, scope))
            {
                break;
            }

            var type = SafeControlType(parent);
            if (type != ControlType.Text && type != ControlType.Document)
            {
                return GetRuntimeKey(parent);
            }

            current = parent;
        }

        return string.Empty;
    }

    private static List<string> MergeInlineFragments(IReadOnlyList<NarrationFragment> fragments)
    {
        var paragraphs = new List<string>();
        NarrationFragment? previous = null;
        var current = string.Empty;

        foreach (var fragment in fragments)
        {
            if (previous is null || fragment.ForceBreakBefore || !ShouldJoin(previous, fragment))
            {
                if (!string.IsNullOrWhiteSpace(current))
                {
                    paragraphs.Add(current);
                }

                current = fragment.Text;
            }
            else
            {
                current += NeedsSpace(current, fragment.Text) ? " " + fragment.Text : fragment.Text;
            }

            previous = fragment;
        }

        if (!string.IsNullOrWhiteSpace(current))
        {
            paragraphs.Add(current);
        }

        return paragraphs;
    }

    private static bool ShouldJoin(NarrationFragment previous, NarrationFragment current)
    {
        if (!previous.Bounds.IsEmpty && !current.Bounds.IsEmpty)
        {
            var verticalOverlap = current.Bounds.Top <= previous.Bounds.Bottom + 2
                && current.Bounds.Bottom >= previous.Bounds.Top - 2;
            if (verticalOverlap)
            {
                return true;
            }

            var verticalGap = current.Bounds.Top - previous.Bounds.Bottom;
            return verticalGap >= -2
                && verticalGap <= 6
                && !string.IsNullOrWhiteSpace(previous.ContainerKey)
                && string.Equals(previous.ContainerKey, current.ContainerKey, StringComparison.Ordinal);
        }

        return !string.IsNullOrWhiteSpace(previous.ContainerKey)
            && string.Equals(previous.ContainerKey, current.ContainerKey, StringComparison.Ordinal);
    }

    private static bool NeedsSpace(string previous, string current)
    {
        if (string.IsNullOrEmpty(previous) || string.IsNullOrEmpty(current))
        {
            return false;
        }

        return !"/\\([{—–-".Contains(previous[^1])
            && !",.;:!?)]}%»".Contains(current[0]);
    }

    private static IEnumerable<string> SplitParagraphs(string text)
    {
        return Regex.Split(text.Replace('\u00A0', ' ').Trim(), @"(?:\r?\n){2,}")
            .Select(NormalizeWhitespace)
            .Where(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string NormalizeWhitespace(string value)
    {
        return Regex.Replace(value, @"[ \t\r\n]+", " ").Trim();
    }

    private static bool IsNarrationText(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 4)
        {
            return false;
        }

        var text = value.Trim();
        return !LooksLikeActiveMarker(text)
            && !LooksLikeCompletedMarker(text)
            && !IsServiceActivityText(text)
            && !Regex.IsMatch(
                text,
                @"^(Awaiting approval|Computer Use|Background processes|Sources|Outputs|View all|Show more)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool IsServiceActivityText(string value)
    {
        var text = NormalizeWhitespace(value).TrimEnd('>', '›', '…').Trim();
        if (text.Length == 0 || text.Length > 120)
        {
            return false;
        }

        var segments = text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length > 0 && segments.All(IsServiceActivitySegment);
    }

    private static bool IsServiceActivitySegment(string value)
    {
        return Regex.IsMatch(
            value,
            @"^(?:(?:run|ran|running)\s+(?:(?:a|the|\d+)\s+)?commands?|(?:edit|edited|editing|read|reading|view|viewed|open|opened|list|listed|listing)\s+(?:\d+\s+)?files?|(?:search|searched|searching)\s+(?:the\s+)?web|(?:apply|applied|applying)\s+(?:a\s+)?patch|(?:run|ran|running)\s+(?:the\s+)?tests?|(?:use|used|using|call|called|calling)\s+(?:(?:a|the|\d+)\s+)?tools?|(?:view|viewed|viewing|analyze|analyzed|analyzing)\s+(?:(?:an?|the|\d+)\s+)?images?|(?:take|took|taking)\s+(?:(?:a|the|\d+)\s+)?screenshots?)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool IsNarrationBoundary(string value)
    {
        return Regex.IsMatch(
            value.Trim(),
            @"^(Sources|Outputs|Background processes|Ask for follow-up changes|Message Codex|Describe a task|Напишите сообщение|Опишите задачу)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool LooksLikeActiveMarker(string value)
    {
        return Regex.IsMatch(
            value.Trim(),
            @"^(Working for|Thinking for|Размышляю|Работаю|Працюю|Міркую)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool LooksLikeCompletedMarker(string value)
    {
        return Regex.IsMatch(
            value.Trim(),
            @"^(Worked for|Completed in|Готово за|Выполнено за|Завершено за)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool LooksLikeCodexWindow(CodexWindow window)
    {
        return window.Title.Contains("Codex", StringComparison.OrdinalIgnoreCase)
            || window.ProcessName.Contains("Codex", StringComparison.OrdinalIgnoreCase)
            || window.ProcessPath.Contains("Codex", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SequenceEquals(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        return left.Count == right.Count
            && left.Select((value, index) => string.Equals(value, right[index], StringComparison.Ordinal)).All(equal => equal);
    }

    private static bool SequencesOverlap(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return false;
        }

        var common = Math.Min(left.Count, right.Count);
        for (var index = 0; index < common; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
            {
                return index > 0;
            }
        }

        return true;
    }

    private static string GetRuntimeKey(AutomationElement element)
    {
        try
        {
            return string.Join(".", element.GetRuntimeId());
        }
        catch
        {
            var bounds = SafeBounds(element);
            return $"{SafeControlType(element).ProgrammaticName}:{bounds.Left:F0}:{bounds.Top:F0}:{bounds.Width:F0}:{bounds.Height:F0}";
        }
    }

    private static string SafeName(AutomationElement element)
    {
        try
        {
            return element.Current.Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static ControlType SafeControlType(AutomationElement element)
    {
        try
        {
            return element.Current.ControlType;
        }
        catch
        {
            return ControlType.Custom;
        }
    }

    private static WpfRect SafeBounds(AutomationElement element)
    {
        try
        {
            return element.Current.BoundingRectangle;
        }
        catch
        {
            return WpfRect.Empty;
        }
    }

    private void LogMonitorError(Exception exception)
    {
        var now = DateTime.UtcNow;
        if (string.Equals(_lastLoggedError, exception.Message, StringComparison.Ordinal)
            && now - _lastErrorLoggedUtc < TimeSpan.FromSeconds(30))
        {
            return;
        }

        _lastLoggedError = exception.Message;
        _lastErrorLoggedUtc = now;
        diagnosticsLog.Error("Codex live narration monitor", exception);
    }

    private sealed record NarrationFragment(
        string Text,
        WpfRect Bounds,
        string ContainerKey,
        bool ForceBreakBefore);

    private sealed record LiveExtraction(string ScopeKey, List<string> Paragraphs, bool IsWorking);
}
