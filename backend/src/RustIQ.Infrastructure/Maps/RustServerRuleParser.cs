using System.Buffers.Binary;
using System.Text;

namespace RustIQ.Infrastructure.Maps;

public static class RustServerRuleParser
{
    private const byte RulesResponseType = 0x45;

    public static IReadOnlyDictionary<string, string> Parse(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 7
            || BinaryPrimitives.ReadInt32LittleEndian(payload) != -1
            || payload[4] != RulesResponseType)
        {
            throw new InvalidDataException("The server returned an invalid A2S_RULES response.");
        }

        var ruleCount = BinaryPrimitives.ReadUInt16LittleEndian(payload[5..]);
        var offset = 7;
        var rules = new Dictionary<string, string>(ruleCount, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < ruleCount; index++)
        {
            var name = ReadNullTerminatedString(payload, ref offset);
            var value = ReadNullTerminatedString(payload, ref offset);

            if (!string.IsNullOrWhiteSpace(name))
            {
                rules[name] = value;
            }
        }

        return rules;
    }

    public static string? BuildRustMapsUrl(string? mapFileUrl)
    {
        if (!Uri.TryCreate(mapFileUrl, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Host, "maps.rustmaps.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
        {
            return null;
        }

        var mapId = segments[^2];
        if (mapId.Length != 32 || !mapId.All(Uri.IsHexDigit))
        {
            return null;
        }

        return $"https://rustmaps.com/map/{mapId}";
    }

    private static string ReadNullTerminatedString(ReadOnlySpan<byte> payload, ref int offset)
    {
        if (offset >= payload.Length)
        {
            throw new InvalidDataException("The A2S_RULES response ended before all rules were read.");
        }

        var terminatorIndex = payload[offset..].IndexOf((byte)0);
        if (terminatorIndex < 0)
        {
            throw new InvalidDataException("The A2S_RULES response contains an unterminated string.");
        }

        var value = Encoding.UTF8.GetString(payload.Slice(offset, terminatorIndex));
        offset += terminatorIndex + 1;
        return value;
    }
}
