using System.Buffers.Binary;

namespace RustIQ.Infrastructure.ServerQuery;

internal sealed record A2sServerInfo(
    string? Name,
    string? Map,
    int Players,
    int MaxPlayers,
    string? Keywords);

internal sealed record A2sPlayer(
    string? Name,
    double DurationSeconds);

internal static class A2sResponseParser
{
    public const byte InfoResponseType = 0x49;
    public const byte PlayerResponseType = 0x44;

    private const byte ExtraDataPortFlag = 0x80;
    private const byte ExtraDataSteamIdFlag = 0x10;
    private const byte ExtraDataSpectatorFlag = 0x40;
    private const byte ExtraDataKeywordsFlag = 0x20;

    /// <summary>
    /// Parses an A2S_INFO response. Rust reports the counts that matter in its keywords rather than the
    /// single-byte player fields, so the keywords are carried out for <see cref="RustServerKeywordParser"/>.
    /// </summary>
    public static A2sServerInfo ParseServerInfo(ReadOnlySpan<byte> payload)
    {
        EnsureResponseType(payload, InfoResponseType, "A2S_INFO");

        var offset = 5;
        A2sPacket.Skip(payload, ref offset, 1); // protocol
        var name = A2sPacket.ReadNullTerminatedString(payload, ref offset);
        var map = A2sPacket.ReadNullTerminatedString(payload, ref offset);
        A2sPacket.ReadNullTerminatedString(payload, ref offset); // folder
        A2sPacket.ReadNullTerminatedString(payload, ref offset); // game
        A2sPacket.Skip(payload, ref offset, 2); // app id
        var players = A2sPacket.ReadByte(payload, ref offset);
        var maxPlayers = A2sPacket.ReadByte(payload, ref offset);
        A2sPacket.Skip(payload, ref offset, 1); // bots
        A2sPacket.Skip(payload, ref offset, 4); // server type, environment, visibility, vac
        A2sPacket.ReadNullTerminatedString(payload, ref offset); // version

        // The extra data block is optional, and each present field has to be stepped over in order to
        // reach the keywords.
        string? keywords = null;
        if (offset < payload.Length)
        {
            var extraDataFlags = A2sPacket.ReadByte(payload, ref offset);

            if ((extraDataFlags & ExtraDataPortFlag) != 0)
            {
                A2sPacket.Skip(payload, ref offset, 2);
            }

            if ((extraDataFlags & ExtraDataSteamIdFlag) != 0)
            {
                A2sPacket.Skip(payload, ref offset, 8);
            }

            if ((extraDataFlags & ExtraDataSpectatorFlag) != 0)
            {
                A2sPacket.Skip(payload, ref offset, 2);
                A2sPacket.ReadNullTerminatedString(payload, ref offset);
            }

            if ((extraDataFlags & ExtraDataKeywordsFlag) != 0)
            {
                keywords = A2sPacket.ReadNullTerminatedString(payload, ref offset);
            }
        }

        return new A2sServerInfo(name, map, players, maxPlayers, keywords);
    }

    /// <summary>
    /// Parses an A2S_PLAYER response. The advertised count is treated as a hint: servers routinely send
    /// fewer entries than they claim, so parsing stops at whichever runs out first.
    /// </summary>
    public static IReadOnlyList<A2sPlayer> ParsePlayers(ReadOnlySpan<byte> payload)
    {
        EnsureResponseType(payload, PlayerResponseType, "A2S_PLAYER");

        var offset = 5;
        var advertisedCount = A2sPacket.ReadByte(payload, ref offset);
        var players = new List<A2sPlayer>(advertisedCount);

        for (var index = 0; index < advertisedCount && offset < payload.Length; index++)
        {
            A2sPacket.Skip(payload, ref offset, 1); // player index, not meaningful for Rust
            var name = A2sPacket.ReadNullTerminatedString(payload, ref offset);
            A2sPacket.Skip(payload, ref offset, 4); // score
            A2sPacket.EnsureRemaining(payload, offset, 4);
            var duration = BinaryPrimitives.ReadSingleLittleEndian(payload[offset..]);
            offset += 4;

            players.Add(new A2sPlayer(
                name,
                double.IsFinite(duration) && duration > 0 ? duration : 0));
        }

        return players;
    }

    private static void EnsureResponseType(
        ReadOnlySpan<byte> payload,
        byte responseType,
        string requestName)
    {
        if (payload.Length < 6
            || BinaryPrimitives.ReadInt32LittleEndian(payload) != A2sPacket.SimplePacketHeader
            || payload[4] != responseType)
        {
            throw new InvalidDataException($"The server returned an invalid {requestName} response.");
        }
    }
}
