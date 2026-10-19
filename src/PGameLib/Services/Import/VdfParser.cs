using System.Text;

namespace PGameLib.Services.Import;

/// <summary>
/// A small parser for Valve's KeyValues (VDF/ACF) text format, used by Steam's
/// libraryfolders.vdf and appmanifest_*.acf files. Handles quoted keys/values, nested
/// objects, escape sequences and // comments. Deliberately lenient — Steam files are
/// well-formed, and anything odd just yields a partial tree rather than throwing.
/// </summary>
public static class VdfParser
{
    public sealed class VdfNode
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, VdfNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? GetValue(string key) => Values.TryGetValue(key, out var v) ? v : null;
        public VdfNode? GetChild(string key) => Children.TryGetValue(key, out var c) ? c : null;
    }

    public static VdfNode Parse(string text)
    {
        int pos = 0;
        var root = new VdfNode();
        ParseInto(text, ref pos, root);
        return root;
    }

    private static void ParseInto(string text, ref int pos, VdfNode node)
    {
        while (pos < text.Length)
        {
            SkipWhitespaceAndComments(text, ref pos);
            if (pos >= text.Length) return;

            if (text[pos] == '}')
            {
                pos++; // close this object
                return;
            }

            var key = ReadToken(text, ref pos);
            if (key is null) return;

            SkipWhitespaceAndComments(text, ref pos);
            if (pos >= text.Length) return;

            if (text[pos] == '{')
            {
                pos++; // open object
                var child = new VdfNode();
                ParseInto(text, ref pos, child);
                node.Children[key] = child;
            }
            else
            {
                var value = ReadToken(text, ref pos);
                if (value is not null)
                    node.Values[key] = value;
            }
        }
    }

    private static void SkipWhitespaceAndComments(string text, ref int pos)
    {
        while (pos < text.Length)
        {
            var c = text[pos];
            if (char.IsWhiteSpace(c))
            {
                pos++;
            }
            else if (c == '/' && pos + 1 < text.Length && text[pos + 1] == '/')
            {
                while (pos < text.Length && text[pos] != '\n') pos++;
            }
            else
            {
                break;
            }
        }
    }

    private static string? ReadToken(string text, ref int pos)
    {
        SkipWhitespaceAndComments(text, ref pos);
        if (pos >= text.Length) return null;

        if (text[pos] == '"')
        {
            pos++; // opening quote
            var sb = new StringBuilder();
            while (pos < text.Length && text[pos] != '"')
            {
                if (text[pos] == '\\' && pos + 1 < text.Length)
                {
                    pos++;
                    sb.Append(text[pos] switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        '\\' => '\\',
                        '"' => '"',
                        var other => other
                    });
                }
                else
                {
                    sb.Append(text[pos]);
                }
                pos++;
            }
            pos++; // closing quote
            return sb.ToString();
        }

        // Unquoted token (rare in these files but tolerated).
        var start = pos;
        while (pos < text.Length && !char.IsWhiteSpace(text[pos]) && text[pos] != '{' && text[pos] != '}')
            pos++;
        return pos > start ? text[start..pos] : null;
    }
}
