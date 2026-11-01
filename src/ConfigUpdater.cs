using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;

namespace Playervotes;

/// <summary>
/// Brings the server's config file up to date with the config class after a plugin update.
/// Properties the class has and the file lacks are added with their default values. The text is
/// edited in place, so comments, order, formatting and every value the server owner set stay as they are.
/// Nothing is removed or overwritten; keys the class no longer knows are only reported.
/// </summary>
internal static class ConfigUpdater
{
    /// <summary>Same reading rules as the config loader: comments and trailing commas are allowed.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public sealed class Report
    {
        /// <summary>Dotted paths of the properties written into the file.</summary>
        public List<string> Added { get; } = [];

        /// <summary>Dotted paths of keys in the file that the config class does not have (typos, removed settings).</summary>
        public List<string> Unknown { get; } = [];
    }

    /// <summary>
    /// Returns the text with the missing properties added. <paramref name="defaults"/> is a fresh instance of
    /// the config class, so property initializers are the only source of default values.
    /// Nested objects are completed key by key. Dictionaries and lists are added whole when missing and never
    /// merged, so example entries (e.g. a default group) do not land next to the server's own entries.
    /// </summary>
    public static string AddMissing<T>(string text, T defaults, JsonSerializerOptions options, Report report)
        where T : class
    {
        var read = new JsonSerializerOptions(options)
        {
            TypeInfoResolver = options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()
        };
        var unit = IndentUnit(text);
        var write = new JsonSerializerOptions(read)
        {
            WriteIndented = true,
            IndentCharacter = unit[0],
            IndentSize = unit.Length
        };

        var edits = new List<Edit>();
        Walk(text, Parse(text), read.GetTypeInfo(typeof(T)), defaults, "", read, write, edits, report);
        if (edits.Count == 0)
            return text;

        foreach (var edit in edits.OrderByDescending(edit => edit.Start))
            text = text[..edit.Start] + edit.Replacement + text[edit.End..];

        Parse(text);
        return text;
    }

    /// <summary>
    /// Rewrites the file only when <paramref name="update"/> changed its text. Keeps the encoding and BOM,
    /// writes a temporary file first and skips the swap when the file changed in the meantime.
    /// A failure is logged as a warning; the plugin keeps running with the config it already read.
    /// </summary>
    public static bool UpdateFile(string path, Func<string, string> update, ILogger logger)
    {
        string? temporary = null;
        try
        {
            if (!File.Exists(path))
                return false;

            var originalBytes = File.ReadAllBytes(path);
            using var reader = new StreamReader(new MemoryStream(originalBytes), Encoding.UTF8, true);
            var original = reader.ReadToEnd();
            var updated = update(original);
            if (updated == original)
                return false;

            var encoding = reader.CurrentEncoding;
            var preamble = encoding.GetPreamble();
            var keepPreamble = preamble.Length > 0 && originalBytes.AsSpan().StartsWith(preamble);
            var payload = encoding.GetBytes(updated);
            temporary = path + ".tmp." + Guid.NewGuid().ToString("N");
            File.WriteAllBytes(temporary, keepPreamble ? preamble.Concat(payload).ToArray() : payload);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(temporary, File.GetUnixFileMode(path));

            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(originalBytes))
            {
                logger.LogWarning("{Path} changed while it was being updated; leaving it untouched until the next load", path);
                return false;
            }

            File.Move(temporary, path, overwrite: true);
            temporary = null;
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Could not update {Path}: {Error}", path, ex.GetBaseException().Message);
            return false;
        }
        finally
        {
            if (temporary != null)
                try { File.Delete(temporary); } catch { }
        }
    }

    private static void Walk(string text, ValueSpan obj, JsonTypeInfo info, object instance, string path,
        JsonSerializerOptions read, JsonSerializerOptions write, List<Edit> edits, Report report)
    {
        var comparer = read.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var newline = Newline(text);
        var childIndent = ChildIndent(text, obj);
        var missing = new List<string>();

        foreach (var property in info.Properties)
        {
            if (property.Get == null)
                continue;

            var value = property.Get(instance);
            var name = path + property.Name;
            var kind = read.GetTypeInfo(property.PropertyType).Kind;
            var existing = obj.Properties!.LastOrDefault(item => comparer.Equals(item.Name, property.Name));

            if (existing == null)
            {
                var json = JsonSerializer.Serialize(value, property.PropertyType, write);
                missing.Add(JsonSerializer.Serialize(property.Name) + ": " + json.ReplaceLineEndings(newline + childIndent));
                report.Added.Add(name);
            }
            else if (existing.Value.Kind == JsonTokenType.Null && value != null && kind == JsonTypeInfoKind.Object)
            {
                var json = JsonSerializer.Serialize(value, property.PropertyType, write);
                edits.Add(new Edit(existing.Value.Start, existing.Value.End,
                    json.ReplaceLineEndings(newline + LineIndent(text, existing.Start))));
                report.Added.Add(name);
            }
            else if (existing.Value.Kind == JsonTokenType.StartObject && kind == JsonTypeInfoKind.Object && value != null)
            {
                Walk(text, existing.Value, read.GetTypeInfo(property.PropertyType), value, name + ".", read, write, edits, report);
            }
        }

        foreach (var item in obj.Properties!)
        {
            if (!info.Properties.Any(property => comparer.Equals(property.Name, item.Name)))
                report.Unknown.Add(path + item.Name);
        }

        if (missing.Count > 0)
            edits.Add(Insert(text, obj, missing, childIndent, newline));
    }

    /// <summary>
    /// New properties go after the last one. A comment at the end of that line stays on that line.
    /// </summary>
    private static Edit Insert(string text, ValueSpan obj, List<string> properties, string childIndent, string newline)
    {
        var block = string.Concat(properties.Select((property, i) => (i == 0 ? "" : ",") + newline + childIndent + property));

        if (obj.Properties!.Count == 0)
        {
            var open = obj.Start + 1;
            var close = obj.End - 1;
            return string.IsNullOrWhiteSpace(text[open..close])
                ? new Edit(open, close, block + newline + LineIndent(text, obj.Start))
                : new Edit(open, open, block);
        }

        var valueEnd = obj.Properties[^1].Value.End;
        var position = SkipHorizontal(text, valueEnd);
        var trailingComma = position < text.Length && text[position] == ',';
        position = trailingComma ? position + 1 : valueEnd;

        var rest = SkipHorizontal(text, position);
        if (rest >= text.Length || text[rest] is '\r' or '\n' || text.AsSpan(rest).StartsWith("//", StringComparison.Ordinal))
            position = LineEnd(text, rest);

        var kept = text[valueEnd..position];
        return new Edit(valueEnd, position, (trailingComma ? "" : ",") + kept + block + (trailingComma ? "," : ""));
    }

    private static string ChildIndent(string text, ValueSpan obj)
    {
        if (obj.Properties!.Count != 0)
        {
            var position = obj.Properties[0].Start;
            var prefix = text[LineStart(text, position)..position];
            if (prefix.Length != 0 && prefix.All(c => c is ' ' or '\t'))
                return prefix;
        }

        return LineIndent(text, obj.Start) + IndentUnit(text);
    }

    /// <summary>Indentation of the first indented property line, two spaces when there is none.</summary>
    private static string IndentUnit(string text)
    {
        for (var position = 0; position < text.Length; position++)
        {
            if (position != 0 && text[position - 1] is not ('\r' or '\n'))
                continue;

            var end = SkipHorizontal(text, position);
            if (end > position && end < text.Length && text[end] == '"')
            {
                var unit = text[position..end];
                return unit.All(c => c == unit[0]) && unit.Length <= 127 ? unit : "  ";
            }
        }

        return "  ";
    }

    private static string LineIndent(string text, int position)
    {
        var start = LineStart(text, position);
        return text[start..SkipHorizontal(text, start)];
    }

    private static int LineStart(string text, int position)
    {
        while (position > 0 && text[position - 1] is not ('\r' or '\n'))
            position--;
        return position;
    }

    private static int LineEnd(string text, int position)
    {
        while (position < text.Length && text[position] is not ('\r' or '\n'))
            position++;
        return position;
    }

    private static int SkipHorizontal(string text, int position)
    {
        while (position < text.Length && text[position] is ' ' or '\t')
            position++;
        return position;
    }

    private static string Newline(string text) =>
        text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static ValueSpan Parse(string text)
    {
        var offset = text.StartsWith('﻿') ? 1 : 0;
        var bytes = Encoding.UTF8.GetBytes(text[offset..]);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("The config must be an object.");

        var root = ReadValue(ref reader, bytes, offset);
        if (reader.Read())
            throw new JsonException("Unexpected content after the config.");
        return root;
    }

    private static ValueSpan ReadValue(ref Utf8JsonReader reader, byte[] bytes, int offset)
    {
        var start = CharacterOffset(bytes, reader.TokenStartIndex, offset);
        var kind = reader.TokenType;
        List<PropertySpan>? properties = null;
        if (kind == JsonTokenType.StartObject)
        {
            properties = [];
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Expected a config property.");

                var name = reader.GetString()!;
                var keyStart = CharacterOffset(bytes, reader.TokenStartIndex, offset);
                if (!reader.Read())
                    throw new JsonException("Expected a config value.");
                properties.Add(new PropertySpan(name, keyStart, ReadValue(ref reader, bytes, offset)));
            }

            if (reader.TokenType != JsonTokenType.EndObject)
                throw new JsonException("The config object is incomplete.");
        }
        else if (kind == JsonTokenType.StartArray)
        {
            reader.Skip();
        }

        return new ValueSpan(kind, start, CharacterOffset(bytes, reader.BytesConsumed, offset), properties);
    }

    private static int CharacterOffset(byte[] bytes, long position, int offset) =>
        offset + Encoding.UTF8.GetCharCount(bytes.AsSpan(0, checked((int)position)));

    private sealed record Edit(int Start, int End, string Replacement);
    private sealed record ValueSpan(JsonTokenType Kind, int Start, int End, List<PropertySpan>? Properties);
    private sealed record PropertySpan(string Name, int Start, ValueSpan Value);
}
