using System.Text.RegularExpressions;

namespace Knode.Services;

/// <summary>Light cleanup so the model does not litter the panel with “as of” timestamps.</summary>
public static partial class AnswerDisplayFormatter
{
    [GeneratedRegex(@"\bas of \d{4}-\d{2}-\d{2}\b", RegexOptions.IgnoreCase)]
    private static partial Regex AsOfIsoRegex();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2})?(\.\d+)?(Z|[+-]\d{2}:\d{2})?\b")]
    private static partial Regex IsoDateTimeRegex();

    [GeneratedRegex(@"\b(?:today|right now),? \d{1,2}:\d{2}\s*(?:AM|PM)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex ColloquialTimeRegex();

    public static string ForDisplay(string? answer)
    {
        if (string.IsNullOrEmpty(answer))
            return answer ?? "";

        var t = answer;
        t = AsOfIsoRegex().Replace(t, "");
        t = IsoDateTimeRegex().Replace(t, "");
        t = ColloquialTimeRegex().Replace(t, "");
        return t.Replace("  ", " ").Trim();
    }
}
