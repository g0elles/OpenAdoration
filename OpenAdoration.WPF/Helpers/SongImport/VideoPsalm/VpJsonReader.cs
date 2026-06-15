using System.Globalization;
using System.Text;

namespace OpenAdoration.WPF.Helpers.SongImport.VideoPsalm;

/// <summary>
/// Tolerant reader for the relaxed JSON dialect VideoPsalm emits inside <c>.vpagd</c> archives:
/// property keys are <b>unquoted</b> (<c>Guid:"..."</c>) and string values may contain
/// <b>literal newlines</b>. Both are illegal in standard JSON, so neither
/// <c>System.Text.Json</c> nor a strict reader can parse it.
/// </summary>
/// <remarks>
/// Produces a plain object graph: <see cref="Dictionary{TKey,TValue}"/> for objects
/// (string keys, ordinal), <see cref="List{T}"/> for arrays, <see cref="string"/>,
/// <see cref="double"/>, <see cref="bool"/>, or <c>null</c>. Reused by the agenda
/// importer beyond songs.
/// </remarks>
internal sealed class VpJsonReader
{
    private readonly string _text;
    private int _pos;

    private VpJsonReader(string text) => _text = text;

    public static object? Parse(string text)
    {
        var reader = new VpJsonReader(text);
        var value = reader.ReadValue();
        reader.SkipWhitespace();
        if (reader._pos != reader._text.Length)
            throw reader.Error("unexpected trailing content");
        return value;
    }

    private object? ReadValue()
    {
        SkipWhitespace();
        return Peek() switch
        {
            '{' => ReadObject(),
            '[' => ReadArray(),
            '"' => ReadString(),
            _   => ReadLiteral()
        };
    }

    private Dictionary<string, object?> ReadObject()
    {
        var obj = new Dictionary<string, object?>(StringComparer.Ordinal);
        Expect('{');
        SkipWhitespace();
        if (TryConsume('}')) return obj;

        while (true)
        {
            SkipWhitespace();
            var key = ReadKey();
            SkipWhitespace();
            Expect(':');
            obj[key] = ReadValue();
            SkipWhitespace();
            if (TryConsume('}')) return obj;
            Expect(',');
        }
    }

    private List<object?> ReadArray()
    {
        var list = new List<object?>();
        Expect('[');
        SkipWhitespace();
        if (TryConsume(']')) return list;

        while (true)
        {
            list.Add(ReadValue());
            SkipWhitespace();
            if (TryConsume(']')) return list;
            Expect(',');
        }
    }

    private string ReadKey()
    {
        if (Peek() == '"') return ReadString();

        var start = _pos;
        while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] is '_' or '$'))
            _pos++;
        if (_pos == start) throw Error("expected a property name");
        return _text[start.._pos];
    }

    private string ReadString()
    {
        Expect('"');
        var sb = new StringBuilder();
        while (true)
        {
            if (_pos >= _text.Length) throw Error("unterminated string");
            var c = _text[_pos++];
            if (c == '"') return sb.ToString();
            sb.Append(c == '\\' ? ReadEscape() : c);
        }
    }

    private char ReadEscape()
    {
        if (_pos >= _text.Length) throw Error("unterminated escape");
        var c = _text[_pos++];
        return c switch
        {
            'n' => '\n', 'r' => '\r', 't' => '\t', 'b' => '\b', 'f' => '\f',
            'u' => ReadUnicodeEscape(),
            _   => c // covers \" \\ \/ and any other escaped literal
        };
    }

    private char ReadUnicodeEscape()
    {
        if (_pos + 4 > _text.Length) throw Error("truncated \\u escape");
        var hex = _text.Substring(_pos, 4);
        _pos += 4;
        return (char)ushort.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private object? ReadLiteral()
    {
        var start = _pos;
        while (_pos < _text.Length && _text[_pos] is not (',' or '}' or ']'))
            _pos++;
        var token = _text[start.._pos].Trim();
        return token switch
        {
            "true"  => true,
            "false" => false,
            "null"  => null,
            _ => double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
                ? number
                : token
        };
    }

    private void SkipWhitespace()
    {
        while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos])) _pos++;
    }

    private char Peek()
    {
        if (_pos >= _text.Length) throw Error("unexpected end of input");
        return _text[_pos];
    }

    private void Expect(char expected)
    {
        if (_pos >= _text.Length || _text[_pos] != expected)
            throw Error($"expected '{expected}'");
        _pos++;
    }

    private bool TryConsume(char c)
    {
        if (_pos >= _text.Length || _text[_pos] != c) return false;
        _pos++;
        return true;
    }

    private FormatException Error(string message) =>
        new($"VideoPsalm JSON parse error at position {_pos}: {message}.");
}
