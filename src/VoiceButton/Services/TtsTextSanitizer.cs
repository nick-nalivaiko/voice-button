using System.Text.RegularExpressions;
using VoiceButton.Models;

namespace VoiceButton.Services;

public static partial class TtsTextSanitizer
{
    private const string CodeReplacement = "[фрагмент кода скрыт]";
    private const string TableReplacement = "[таблица скрыта]";
    private const string StackTraceReplacement = "[лог или stack trace скрыт]";
    private const string StructuredDataReplacement = "[структурированные данные скрыты]";
    private const string CommandReplacement = "[команда скрыта]";
    private const string SecretReplacement = "[секрет скрыт]";
    private const string LongFragmentReplacement = "[длинный технический фрагмент скрыт]";

    public static string Sanitize(string text)
    {
        return Sanitize(text, TtsTextSanitizerOptions.PathOnly);
    }

    public static string Sanitize(string text, TtsTextSanitizerOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var sanitized = text;

        if (options.HideSecrets)
        {
            sanitized = HideSecrets(sanitized);
        }

        if (options.HideCodeBlocks)
        {
            sanitized = HideCodeBlocks(sanitized);
        }

        if (options.HideInlineCode)
        {
            sanitized = HideInlineCode(sanitized);
        }

        if (options.CollapseTables)
        {
            sanitized = CollapseMatchingLineRuns(sanitized, IsMarkdownTableLine, TableReplacement, minimumRunLength: 2);
        }

        if (options.CollapseStackTraces)
        {
            sanitized = CollapseMatchingLineRuns(sanitized, IsStackTraceOrLogLine, StackTraceReplacement, minimumRunLength: 2);
        }

        if (options.CollapseStructuredData)
        {
            sanitized = CollapseMatchingLineRuns(sanitized, IsStructuredDataLine, StructuredDataReplacement, minimumRunLength: 2);
        }

        if (options.ShortenShellCommands)
        {
            sanitized = CollapseMatchingLineRuns(sanitized, IsShellCommandLine, CommandReplacement, minimumRunLength: 1);
        }

        if (options.HideFilePaths)
        {
            sanitized = SanitizeFilePaths(sanitized);
        }

        if (options.ShortenLinks)
        {
            sanitized = ShortenLinks(sanitized);
        }

        if (options.ShortenHashes)
        {
            sanitized = ShortenHashes(sanitized);
        }

        if (options.HideLongNumbers)
        {
            sanitized = HideLongTechnicalFragments(sanitized);
        }

        if (options.RemoveMarkdownNoise)
        {
            sanitized = RemoveMarkdownNoise(sanitized);
        }

        return NormalizeWhitespace(sanitized);
    }

    private static string SanitizeFilePaths(string text)
    {
        var sanitized = MarkdownFileLinkRegex().Replace(text, match =>
        {
            var label = match.Groups["label"].Value;
            var target = match.Groups["target"].Value.Trim('<', '>');
            return LooksLikeFilePath(target) ? Sanitize(label, TtsTextSanitizerOptions.PathOnly) : match.Value;
        });

        sanitized = AnglePathRegex().Replace(sanitized, match => PathTail(match.Groups["path"].Value));
        sanitized = WindowsAbsoluteFilePathRegex().Replace(sanitized, match => PathTail(match.Groups["path"].Value));
        sanitized = UnixAbsoluteFilePathRegex().Replace(sanitized, match => PathTail(match.Groups["path"].Value));
        sanitized = RelativeFilePathRegex().Replace(sanitized, match => PathTail(match.Groups["path"].Value));

        return sanitized;
    }

    private static string HideSecrets(string text)
    {
        var sanitized = OpenAiKeyRegex().Replace(text, SecretReplacement);
        sanitized = JwtRegex().Replace(sanitized, SecretReplacement);
        sanitized = BearerTokenRegex().Replace(sanitized, "Bearer " + SecretReplacement);
        sanitized = SecretAssignmentRegex().Replace(sanitized, match => $"{match.Groups["key"].Value}={SecretReplacement}");
        return sanitized;
    }

    private static string HideCodeBlocks(string text)
    {
        var sanitized = FencedCodeBlockRegex().Replace(text, CodeReplacement);
        return CollapseMatchingLineRuns(sanitized, IsIndentedCodeLine, CodeReplacement, minimumRunLength: 2);
    }

    private static string HideInlineCode(string text)
    {
        return InlineCodeRegex().Replace(text, CodeReplacement);
    }

    private static string ShortenLinks(string text)
    {
        return UrlRegex().Replace(text, match =>
        {
            var rawUrl = match.Groups["url"].Value;
            var suffix = string.Empty;
            while (rawUrl.Length > 0 && ".,;:!?)]}".Contains(rawUrl[^1]))
            {
                suffix = rawUrl[^1] + suffix;
                rawUrl = rawUrl[..^1];
            }

            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
            {
                return "ссылка" + suffix;
            }

            var label = uri.Host;
            var segments = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length > 0)
            {
                var last = Uri.UnescapeDataString(segments[^1]).Replace('-', ' ').Replace('_', ' ');
                if (!string.IsNullOrWhiteSpace(last))
                {
                    label += "/" + last;
                }
            }

            return "ссылка " + label + suffix;
        });
    }

    private static string ShortenHashes(string text)
    {
        var sanitized = GuidRegex().Replace(text, match => match.Value[..8] + "...");
        sanitized = LongHexHashRegex().Replace(sanitized, match => match.Value[..8] + "...");
        return sanitized;
    }

    private static string HideLongTechnicalFragments(string text)
    {
        var sanitized = HexDumpRegex().Replace(text, LongFragmentReplacement);
        sanitized = Base64BlobRegex().Replace(sanitized, LongFragmentReplacement);
        sanitized = LongNumberRegex().Replace(sanitized, match => match.Value[..4] + "..." + match.Value[^2..]);
        return sanitized;
    }

    private static string RemoveMarkdownNoise(string text)
    {
        var sanitized = MarkdownImageRegex().Replace(text, match =>
        {
            var alt = match.Groups["alt"].Value.Trim();
            return string.IsNullOrWhiteSpace(alt) ? "[изображение]" : alt;
        });
        sanitized = MarkdownLinkRegex().Replace(sanitized, match => match.Groups["label"].Value);
        sanitized = MarkdownHeadingRegex().Replace(sanitized, string.Empty);
        sanitized = MarkdownQuoteRegex().Replace(sanitized, string.Empty);
        sanitized = MarkdownBulletRegex().Replace(sanitized, string.Empty);
        sanitized = MarkdownEmphasisRegex().Replace(sanitized, string.Empty);
        sanitized = sanitized.Replace("`", string.Empty);
        return sanitized;
    }

    private static string CollapseMatchingLineRuns(string text, Func<string, bool> isMatch, string replacement, int minimumRunLength)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var result = new List<string>(lines.Length);

        for (var index = 0; index < lines.Length;)
        {
            if (!isMatch(lines[index]))
            {
                result.Add(lines[index]);
                index++;
                continue;
            }

            var start = index;
            while (index < lines.Length && isMatch(lines[index]))
            {
                index++;
            }

            var count = index - start;
            if (count >= minimumRunLength)
            {
                if (result.Count == 0 || !string.Equals(result[^1], replacement, StringComparison.Ordinal))
                {
                    result.Add(replacement);
                }
            }
            else
            {
                for (var lineIndex = start; lineIndex < index; lineIndex++)
                {
                    result.Add(lines[lineIndex]);
                }
            }
        }

        return string.Join('\n', result);
    }

    private static bool LooksLikeFilePath(string value)
    {
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return value.Contains('\\') ||
               value.Contains('/') ||
               DriveRootRegex().IsMatch(value);
    }

    private static string PathTail(string rawPath)
    {
        var path = rawPath.Trim().Trim('<', '>', '`', '"', '\'', '“', '”');
        var suffix = string.Empty;

        var lineMatch = LineSuffixRegex().Match(path);
        if (lineMatch.Success)
        {
            suffix = lineMatch.Groups["suffix"].Value;
            path = path[..^suffix.Length];
        }

        path = path.TrimEnd('.', ',', ';', ':', ')', ']', '}');
        path = path.Replace('\\', '/');

        var lastSlash = path.LastIndexOf('/');
        var tail = lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
        tail = tail.Trim();

        return string.IsNullOrWhiteSpace(tail) ? rawPath : tail + suffix;
    }

    private static bool IsMarkdownTableLine(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Count(character => character == '|') >= 2 &&
               (trimmed.StartsWith('|') || MarkdownTableSeparatorRegex().IsMatch(trimmed));
    }

    private static bool IsStackTraceOrLogLine(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        return StackTraceLineRegex().IsMatch(trimmed) || LogLineRegex().IsMatch(trimmed);
    }

    private static bool IsStructuredDataLine(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        return JsonBraceLineRegex().IsMatch(trimmed) || StructuredKeyValueLineRegex().IsMatch(line);
    }

    private static bool IsShellCommandLine(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        return ShellPromptLineRegex().IsMatch(trimmed) || CommonShellCommandRegex().IsMatch(trimmed);
    }

    private static bool IsIndentedCodeLine(string line)
    {
        return line.StartsWith("    ", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(line);
    }

    private static string NormalizeWhitespace(string text)
    {
        var lines = text.ReplaceLineEndings("\n")
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToArray();
        var joined = string.Join('\n', lines);
        joined = ExcessiveBlankLinesRegex().Replace(joined, "\n\n");
        joined = ExcessiveSpacesRegex().Replace(joined, " ");
        return joined.Trim();
    }

    [GeneratedRegex(@"\[(?<label>[^\]]+)\]\((?<target><[^>]+>|[^\)]+)\)")]
    private static partial Regex MarkdownFileLinkRegex();

    [GeneratedRegex(@"<(?<path>(?:[A-Za-z]:[\\/]|/|\.\.?[\\/])[^>\r\n]+?)>")]
    private static partial Regex AnglePathRegex();

    [GeneratedRegex(@"(?<path>[A-Za-z]:[\\/][^\r\n]+?[^\\/\r\n]+\.[A-Za-z0-9]{1,12}(?::\d+)?)")]
    private static partial Regex WindowsAbsoluteFilePathRegex();

    [GeneratedRegex(@"(?<![\w\.:/])(?<path>/(?!/)[^\r\n]+?/[^/\r\n]+\.[A-Za-z0-9]{1,12}(?::\d+)?)")]
    private static partial Regex UnixAbsoluteFilePathRegex();

    [GeneratedRegex(@"(?<![\w:])(?<path>(?:\.{1,2}[\\/]|[A-Za-z0-9_. -]+[\\/])(?:[A-Za-z0-9_. -]+[\\/])*[A-Za-z0-9_. -]+\.[A-Za-z0-9]{1,12}(?::\d+)?)")]
    private static partial Regex RelativeFilePathRegex();

    [GeneratedRegex(@"^[A-Za-z]:[\\/]")]
    private static partial Regex DriveRootRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_. -]+\.[A-Za-z0-9]{1,12}(?::\d+)?$")]
    private static partial Regex FileNameRegex();

    [GeneratedRegex(@"(?<suffix>:\d+)$")]
    private static partial Regex LineSuffixRegex();

    [GeneratedRegex(@"sk-[A-Za-z0-9_\-]{20,}")]
    private static partial Regex OpenAiKeyRegex();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}\b")]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"(?i)\bBearer\s+[A-Za-z0-9._\-+/=]{16,}")]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"(?i)\b(?<key>[A-Z0-9_]*(?:KEY|TOKEN|SECRET|PASSWORD|PASS|AUTH)[A-Z0-9_]*)\s*[:=]\s*['""`]?[^'""`\s,;]+")]
    private static partial Regex SecretAssignmentRegex();

    [GeneratedRegex(@"```[\s\S]*?```")]
    private static partial Regex FencedCodeBlockRegex();

    [GeneratedRegex(@"`[^`\r\n]{1,220}`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"(?<url>https?://[^\s<>)\]]+)")]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"\b[0-9a-fA-F]{16,64}\b")]
    private static partial Regex LongHexHashRegex();

    [GeneratedRegex(@"(?:\b[0-9A-Fa-f]{2}\s+){16,}[0-9A-Fa-f]{2}\b")]
    private static partial Regex HexDumpRegex();

    [GeneratedRegex(@"\b[A-Za-z0-9+/]{80,}={0,2}\b")]
    private static partial Regex Base64BlobRegex();

    [GeneratedRegex(@"\b\d{12,}\b")]
    private static partial Regex LongNumberRegex();

    [GeneratedRegex(@"^\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?$")]
    private static partial Regex MarkdownTableSeparatorRegex();

    [GeneratedRegex(@"^(?:at\s+.+\(|--- End of|File \"".+\"", line \d+|Traceback \(most recent call last\)|Caused by:|\s*at\s+.+)")]
    private static partial Regex StackTraceLineRegex();

    [GeneratedRegex(@"^(?:\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}|\[[A-Z]+\]|(?:ERROR|WARN|INFO|DEBUG|TRACE)\b)")]
    private static partial Regex LogLineRegex();

    [GeneratedRegex(@"^[\{\}\[\]],?$")]
    private static partial Regex JsonBraceLineRegex();

    [GeneratedRegex(@"^\s*(?:\""[^\""\r\n]+\""|[A-Za-z_][A-Za-z0-9_.-]*)\s*:\s*(?:[\""'\[\{]|-?\d|true\b|false\b|null\b|$)")]
    private static partial Regex StructuredKeyValueLineRegex();

    [GeneratedRegex(@"^(?:\$|>|PS\s+.+>|[A-Za-z]:\\.+>)\s*\S+")]
    private static partial Regex ShellPromptLineRegex();

    [GeneratedRegex(@"^(?:dotnet|npm|node|python|py|git|rg|curl|pwsh|powershell|docker|kubectl|taskkill|Get-Process|Get-ChildItem|Select-String)\b")]
    private static partial Regex CommonShellCommandRegex();

    [GeneratedRegex(@"!\[(?<alt>[^\]]*)\]\([^\)]*\)")]
    private static partial Regex MarkdownImageRegex();

    [GeneratedRegex(@"\[(?<label>[^\]]+)\]\([^\)]*\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"(?m)^\s{0,3}#{1,6}\s*")]
    private static partial Regex MarkdownHeadingRegex();

    [GeneratedRegex(@"(?m)^\s*>\s?")]
    private static partial Regex MarkdownQuoteRegex();

    [GeneratedRegex(@"(?m)^\s*(?:[-*+] |\d+\.\s+)")]
    private static partial Regex MarkdownBulletRegex();

    [GeneratedRegex(@"[*_~]{1,3}")]
    private static partial Regex MarkdownEmphasisRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessiveBlankLinesRegex();

    [GeneratedRegex(@"[ \t]{3,}")]
    private static partial Regex ExcessiveSpacesRegex();
}

public sealed record TtsTextSanitizerOptions(
    bool HideFilePaths,
    bool HideCodeBlocks,
    bool HideInlineCode,
    bool ShortenLinks,
    bool HideSecrets,
    bool ShortenHashes,
    bool CollapseStackTraces,
    bool RemoveMarkdownNoise,
    bool CollapseTables,
    bool CollapseStructuredData,
    bool ShortenShellCommands,
    bool HideLongNumbers)
{
    public static TtsTextSanitizerOptions PathOnly { get; } = new(
        HideFilePaths: true,
        HideCodeBlocks: false,
        HideInlineCode: false,
        ShortenLinks: false,
        HideSecrets: false,
        ShortenHashes: false,
        CollapseStackTraces: false,
        RemoveMarkdownNoise: false,
        CollapseTables: false,
        CollapseStructuredData: false,
        ShortenShellCommands: false,
        HideLongNumbers: false);

    public static TtsTextSanitizerOptions FromSettings(AppSettings settings)
    {
        return new TtsTextSanitizerOptions(
            settings.HideFilePathsInSpeech,
            settings.HideCodeBlocksInSpeech,
            settings.HideInlineCodeInSpeech,
            settings.ShortenLinksInSpeech,
            settings.HideSecretsInSpeech,
            settings.ShortenHashesInSpeech,
            settings.CollapseStackTracesInSpeech,
            settings.RemoveMarkdownNoiseInSpeech,
            settings.CollapseTablesInSpeech,
            settings.CollapseStructuredDataInSpeech,
            settings.ShortenShellCommandsInSpeech,
            settings.HideLongNumbersInSpeech);
    }
}
