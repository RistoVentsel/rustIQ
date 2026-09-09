using System.Buffers.Binary;
using System.Net.Sockets;

namespace RustIQ.Infrastructure.ServerQuery;

/// <summary>
/// Packet-level A2S transport shared by the A2S clients.
/// </summary>
/// <remarks>
/// Every receive here runs against a socket this process owns, with cancellation as the timeout, so a
/// server that never answers costs one cancelled await. The A2S NuGet package this replaced exposed
/// only a blocking <c>Query(address, port, timeout)</c> that ignored its timeout argument entirely -
/// measured still blocked after 45 seconds - which leaked a thread per unreachable server.
/// </remarks>
internal static class A2sPacket
{
    public const int SimplePacketHeader = -1;
    public const int SplitPacketHeader = -2;
    public const byte ChallengeResponseType = 0x41;
    public const int ChallengeLength = 4;

    private const int MaximumSplitPacketCount = 32;
    private const int SplitPacketPrefixLength = 12;
    private const int ChallengeOffset = 5;

    /// <summary>
    /// Builds the initial "give me a challenge" request for a request type.
    /// </summary>
    public static byte[] BuildChallengeRequest(byte requestType)
    {
        return [0xff, 0xff, 0xff, 0xff, requestType, 0xff, 0xff, 0xff, 0xff];
    }

    /// <summary>
    /// Builds a request of <paramref name="requestType"/> carrying an optional fixed body (such as the
    /// A2S_INFO query string) followed by an optional echoed challenge.
    /// </summary>
    public static byte[] BuildRequest(
        byte requestType,
        ReadOnlySpan<byte> body,
        ReadOnlySpan<byte> challenge)
    {
        var request = new byte[ChallengeOffset + body.Length + challenge.Length];
        BinaryPrimitives.WriteInt32LittleEndian(request, SimplePacketHeader);
        request[4] = requestType;
        body.CopyTo(request.AsSpan(ChallengeOffset));
        challenge.CopyTo(request.AsSpan(ChallengeOffset + body.Length));
        return request;
    }

    public static bool IsChallengeResponse(ReadOnlySpan<byte> payload)
    {
        return payload.Length >= ChallengeOffset + ChallengeLength
               && BinaryPrimitives.ReadInt32LittleEndian(payload) == SimplePacketHeader
               && payload[4] == ChallengeResponseType;
    }

    /// <summary>
    /// Copies the four challenge bytes out of a challenge response so they can be echoed back.
    /// </summary>
    public static byte[] CopyChallenge(ReadOnlySpan<byte> challengeResponse)
    {
        if (!IsChallengeResponse(challengeResponse))
        {
            throw new InvalidDataException("The server returned an invalid A2S challenge.");
        }

        return challengeResponse.Slice(ChallengeOffset, ChallengeLength).ToArray();
    }

    /// <summary>
    /// Receives one logical A2S response, reassembling a split response when the server sends one. The
    /// returned payload always starts with the four-byte simple header followed by the response type.
    /// </summary>
    public static async Task<byte[]> ReceiveResponseAsync(
        UdpClient client,
        CancellationToken cancellationToken)
    {
        var firstPacket = (await client.ReceiveAsync(cancellationToken)).Buffer;
        if (firstPacket.Length < 4)
        {
            throw new InvalidDataException("The server returned an incomplete A2S response.");
        }

        var header = BinaryPrimitives.ReadInt32LittleEndian(firstPacket);
        if (header == SimplePacketHeader)
        {
            return firstPacket;
        }

        if (header != SplitPacketHeader || firstPacket.Length < SplitPacketPrefixLength)
        {
            throw new InvalidDataException("The server returned an invalid A2S packet header.");
        }

        var responseId = BinaryPrimitives.ReadInt32LittleEndian(firstPacket.AsSpan(4));
        if ((responseId & int.MinValue) != 0)
        {
            throw new InvalidDataException("Compressed split A2S responses are not supported.");
        }

        var packetCount = firstPacket[8];
        if (packetCount is 0 or > MaximumSplitPacketCount)
        {
            throw new InvalidDataException("The server returned an invalid A2S split-packet count.");
        }

        var fragments = new byte[packetCount][];
        AddSplitPacket(firstPacket, responseId, fragments);

        while (fragments.Any(fragment => fragment is null))
        {
            var packet = (await client.ReceiveAsync(cancellationToken)).Buffer;
            AddSplitPacket(packet, responseId, fragments);
        }

        var length = fragments.Sum(fragment => fragment.Length);
        var payload = new byte[length];
        var offset = 0;
        foreach (var fragment in fragments)
        {
            fragment.CopyTo(payload, offset);
            offset += fragment.Length;
        }

        return payload;
    }

    /// <summary>
    /// Reads a null-terminated string, advancing <paramref name="offset"/> past the terminator.
    /// </summary>
    public static string ReadNullTerminatedString(ReadOnlySpan<byte> payload, ref int offset)
    {
        if (offset < 0 || offset > payload.Length)
        {
            throw new InvalidDataException("The server returned a truncated A2S response.");
        }

        var terminator = payload[offset..].IndexOf((byte)0x00);
        if (terminator < 0)
        {
            throw new InvalidDataException("The server returned an unterminated A2S string.");
        }

        var value = System.Text.Encoding.UTF8.GetString(payload.Slice(offset, terminator));
        offset += terminator + 1;
        return value;
    }

    /// <summary>
    /// Reads a single byte, advancing <paramref name="offset"/>.
    /// </summary>
    public static byte ReadByte(ReadOnlySpan<byte> payload, ref int offset)
    {
        EnsureRemaining(payload, offset, 1);
        return payload[offset++];
    }

    /// <summary>
    /// Skips <paramref name="count"/> bytes, failing rather than running off the end of the payload.
    /// </summary>
    public static void Skip(ReadOnlySpan<byte> payload, ref int offset, int count)
    {
        EnsureRemaining(payload, offset, count);
        offset += count;
    }

    public static void EnsureRemaining(ReadOnlySpan<byte> payload, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > payload.Length)
        {
            throw new InvalidDataException("The server returned a truncated A2S response.");
        }
    }

    private static void AddSplitPacket(
        byte[] packet,
        int expectedResponseId,
        byte[][] fragments)
    {
        if (packet.Length < SplitPacketPrefixLength
            || BinaryPrimitives.ReadInt32LittleEndian(packet) != SplitPacketHeader
            || BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(4)) != expectedResponseId
            || packet[8] != fragments.Length
            || packet[9] >= fragments.Length)
        {
            throw new InvalidDataException("The server returned an invalid A2S split packet.");
        }

        fragments[packet[9]] = packet[SplitPacketPrefixLength..];
    }
}
