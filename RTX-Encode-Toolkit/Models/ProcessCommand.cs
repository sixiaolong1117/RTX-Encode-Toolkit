using System.Collections.Generic;
using System.Linq;

namespace RTX_Encode_Toolkit.Models;

public sealed record ProcessCommand(string FileName, IReadOnlyList<string> Arguments)
{
    public string ToCommandLine()
    {
        var parts = new[] { FileName }.Concat(Arguments);
        return string.Join(" ", parts.Select(Quote));
    }

    private static string Quote(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (!value.Any(char.IsWhiteSpace) && !value.Contains('"'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
