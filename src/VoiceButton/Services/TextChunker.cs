using System.Text;

namespace VoiceButton.Services;

public static class TextChunker
{
    public static IReadOnlyList<string> Split(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var normalized = text.Replace("\r\n", "\n").Trim();
        if (normalized.Length <= maxLength)
        {
            return [normalized];
        }

        var chunks = new List<string>();
        var current = new StringBuilder(maxLength);

        foreach (var paragraph in normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            AppendSegment(paragraph, maxLength, current, chunks);
        }

        Flush(current, chunks);
        return chunks;
    }

    private static void AppendSegment(string segment, int maxLength, StringBuilder current, List<string> chunks)
    {
        if (segment.Length > maxLength)
        {
            foreach (var part in SplitLongSegment(segment, maxLength))
            {
                AppendSegment(part, maxLength, current, chunks);
            }

            return;
        }

        var separatorLength = current.Length == 0 ? 0 : 2;
        if (current.Length + separatorLength + segment.Length > maxLength)
        {
            Flush(current, chunks);
        }

        if (current.Length > 0)
        {
            current.Append("\n\n");
        }

        current.Append(segment);
    }

    private static IEnumerable<string> SplitLongSegment(string segment, int maxLength)
    {
        var remaining = segment.Trim();
        while (remaining.Length > maxLength)
        {
            var splitAt = FindSplitPoint(remaining, maxLength);
            yield return remaining[..splitAt].Trim();
            remaining = remaining[splitAt..].Trim();
        }

        if (remaining.Length > 0)
        {
            yield return remaining;
        }
    }

    private static int FindSplitPoint(string text, int maxLength)
    {
        var searchStart = Math.Min(maxLength, text.Length - 1);
        var punctuationIndex = text.LastIndexOfAny(['.', '!', '?', ';', ':', '\n'], searchStart);
        if (punctuationIndex > maxLength / 2)
        {
            return punctuationIndex + 1;
        }

        var spaceIndex = text.LastIndexOf(' ', searchStart);
        return spaceIndex > maxLength / 2 ? spaceIndex : maxLength;
    }

    private static void Flush(StringBuilder current, List<string> chunks)
    {
        if (current.Length == 0)
        {
            return;
        }

        chunks.Add(current.ToString().Trim());
        current.Clear();
    }
}
