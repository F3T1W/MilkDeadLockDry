using System.Text;

namespace DeadLocky.Features.LaunchGame.Api;

internal sealed class SteamVdf(string name, string? value = null)
{
    public string Name { get; } = name;
    public string? Value { get; } = value;
    public List<SteamVdf> Children { get; } = [];

    public SteamVdf this[string key] => Children.FirstOrDefault(child =>
        string.Equals(child.Name, key, StringComparison.OrdinalIgnoreCase)) ?? new SteamVdf(key);

    public static SteamVdf? LoadAsText(string path)
    {
        try
        {
            if (new FileInfo(path).Length > 4 * 1024 * 1024)
            {
                return null;
            }

            var reader = new Reader(File.ReadAllText(path));
            SteamVdf root = reader.ReadNode();
            return reader.Next() is null ? root : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private sealed class Reader(string text)
    {
        private int _depth;
        private int _position;

        public SteamVdf ReadNode()
        {
            if (_depth > 32)
            {
                throw new IOException("Steam metadata is nested too deeply.");
            }

            string name = Next() ?? throw new IOException("Missing Steam metadata key.");
            if (name is "{" or "}")
            {
                throw new IOException("Invalid Steam metadata key.");
            }

            string value = Next() ?? throw new IOException("Missing Steam metadata value.");
            if (value != "{")
            {
                return value == "}"
                    ? throw new IOException("Invalid Steam metadata value.")
                    : new SteamVdf(name, value);
            }

            var node = new SteamVdf(name);
            _depth++;
            while (true)
            {
                int saved = _position;
                string next = Next() ?? throw new IOException("Unclosed Steam metadata object.");
                if (next == "}")
                {
                    _depth--;
                    return node;
                }

                _position = saved;
                node.Children.Add(ReadNode());
            }
        }

        public string? Next()
        {
            while (_position < text.Length)
            {
                char current = text[_position];
                if (char.IsWhiteSpace(current) || current == '\uFEFF')
                {
                    _position++;
                    continue;
                }

                if (current == '/' && _position + 1 < text.Length && text[_position + 1] == '/')
                {
                    while (_position < text.Length && text[_position] != '\n')
                    {
                        _position++;
                    }

                    continue;
                }

                break;
            }

            if (_position == text.Length)
            {
                return null;
            }

            char first = text[_position++];
            if (first is '{' or '}')
            {
                return first.ToString();
            }

            var token = new StringBuilder();
            if (first != '"')
            {
                _ = token.Append(first);
                while (_position < text.Length && !char.IsWhiteSpace(text[_position])
                                               && text[_position] is not ('{' or '}'))
                {
                    _ = token.Append(text[_position++]);
                }

                return token.ToString();
            }

            while (_position < text.Length)
            {
                char current = text[_position++];
                switch (current)
                {
                    case '"': return token.ToString();
                    case '\\' when _position < text.Length:
                        char escaped = text[_position++];
                        _ = token.Append(escaped switch { 'n' => '\n', 't' => '\t', 'r' => '\r', _ => escaped });
                        break;
                    default: _ = token.Append(current); break;
                }
            }

            throw new IOException("Unclosed Steam metadata string.");
        }
    }
}
