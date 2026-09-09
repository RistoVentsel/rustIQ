using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RustIQ.Tests.TestDoubles;

/// <summary>
/// A UDP server on loopback that speaks enough A2S to exercise the query client: the challenge
/// handshake, packet loss, split responses, and silence.
/// </summary>
internal sealed class FakeA2sServer : IDisposable
{
    private const byte InfoRequestType = 0x54;
    private const byte PlayerRequestType = 0x55;
    private const byte RulesRequestType = 0x56;
    private const byte InfoResponseType = 0x49;
    private const byte PlayerResponseType = 0x44;
    private const byte RulesResponseType = 0x45;
    private const byte ChallengeResponseType = 0x41;
    private const int SimplePacketHeader = -1;
    private const int SplitPacketHeader = -2;

    private static readonly byte[] Challenge = [0x11, 0x22, 0x33, 0x44];

    private readonly UdpClient _listener = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly CancellationTokenSource _cancellation = new();
    private int _requestCount;

    public FakeA2sServer()
    {
        _ = Task.Run(ReceiveLoopAsync);
    }

    public int Port => ((IPEndPoint)_listener.Client.LocalEndPoint!).Port;

    public int RequestCount => Volatile.Read(ref _requestCount);

    /// <summary>Answer the first request with a challenge that has to be echoed back.</summary>
    public bool RequireChallenge { get; init; } = true;

    /// <summary>Never answer anything, imitating an unreachable server.</summary>
    public bool Silent { get; init; }

    /// <summary>Silently drop this many requests before answering, imitating packet loss.</summary>
    public int DropFirstRequests { get; init; }

    /// <summary>Send the player list as a split response instead of a single packet.</summary>
    public bool SplitPlayerResponse { get; init; }

    public string ServerName { get; init; } = "Fake Rust Server";

    public string MapName { get; init; } = "Procedural Map";

    public byte AdvertisedPlayers { get; init; } = 42;

    public byte AdvertisedMaxPlayers { get; init; } = 200;

    public string? Keywords { get; init; } = "mp200,cp42,qp0";

    public IReadOnlyList<string> Players { get; init; } = [];

    public IReadOnlyDictionary<string, string> Rules { get; init; } = new Dictionary<string, string>();

    public void Dispose()
    {
        _cancellation.Cancel();
        _listener.Dispose();
        _cancellation.Dispose();
    }

    private async Task ReceiveLoopAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await _listener.ReceiveAsync(_cancellation.Token);
            }
            catch
            {
                return;
            }

            var requestNumber = Interlocked.Increment(ref _requestCount);
            if (Silent || requestNumber <= DropFirstRequests)
            {
                continue;
            }

            var responses = BuildResponses(received.Buffer);
            try
            {
                foreach (var response in responses)
                {
                    await _listener.SendAsync(response, received.RemoteEndPoint, _cancellation.Token);
                }
            }
            catch
            {
                return;
            }
        }
    }

    private IReadOnlyList<byte[]> BuildResponses(byte[] request)
    {
        if (request.Length < 5)
        {
            return [];
        }

        var requestType = request[4];

        // The echoed challenge is the last four bytes. Comparing them beats comparing lengths, because
        // an A2S_PLAYER challenge request and its echo are both nine bytes long.
        var carriesChallenge = request.Length >= 9
                               && request.AsSpan(request.Length - 4).SequenceEqual(Challenge);

        if (RequireChallenge && !carriesChallenge)
        {
            return [BuildChallengeResponse()];
        }

        return requestType switch
        {
            InfoRequestType => [BuildInfoResponse()],
            PlayerRequestType => BuildPlayerResponses(),
            RulesRequestType => [BuildRulesResponse()],
            _ => []
        };
    }

    private static byte[] BuildChallengeResponse()
    {
        var response = new byte[9];
        BinaryPrimitives.WriteInt32LittleEndian(response, SimplePacketHeader);
        response[4] = ChallengeResponseType;
        Challenge.CopyTo(response, 5);
        return response;
    }

    private byte[] BuildInfoResponse()
    {
        var payload = new List<byte>();
        payload.AddRange([0xff, 0xff, 0xff, 0xff]);
        payload.Add(InfoResponseType);
        payload.Add(0x11);                       // protocol
        AppendString(payload, ServerName);
        AppendString(payload, MapName);
        AppendString(payload, "rust");
        AppendString(payload, "Rust");
        payload.AddRange([0xfa, 0x00]);          // app id
        payload.Add(AdvertisedPlayers);
        payload.Add(AdvertisedMaxPlayers);
        payload.Add(0);                          // bots
        payload.Add((byte)'d');                  // dedicated
        payload.Add((byte)'l');                  // linux
        payload.Add(0);                          // visibility
        payload.Add(0);                          // vac
        AppendString(payload, "1.0.0.0");

        if (Keywords is not null)
        {
            payload.Add(0x20);                   // extra data flags: keywords follow
            AppendString(payload, Keywords);
        }

        return payload.ToArray();
    }

    private IReadOnlyList<byte[]> BuildPlayerResponses()
    {
        var payload = new List<byte>();
        payload.AddRange([0xff, 0xff, 0xff, 0xff]);
        payload.Add(PlayerResponseType);
        payload.Add((byte)Players.Count);

        for (var index = 0; index < Players.Count; index++)
        {
            payload.Add((byte)index);
            AppendString(payload, Players[index]);
            payload.AddRange(BitConverter.GetBytes(10 * (index + 1)));           // score
            payload.AddRange(BitConverter.GetBytes(60f * (index + 1)));          // duration seconds
        }

        var response = payload.ToArray();
        return SplitPlayerResponse ? Split(response, fragments: 2) : [response];
    }

    private static IReadOnlyList<byte[]> Split(byte[] response, int fragments)
    {
        var packets = new List<byte[]>(fragments);
        var fragmentSize = (response.Length + fragments - 1) / fragments;

        for (var index = 0; index < fragments; index++)
        {
            var start = index * fragmentSize;
            var length = Math.Min(fragmentSize, response.Length - start);
            var packet = new byte[12 + length];
            BinaryPrimitives.WriteInt32LittleEndian(packet, SplitPacketHeader);
            BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(4), 0x0000_1234);
            packet[8] = (byte)fragments;
            packet[9] = (byte)index;
            BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(10), (short)fragmentSize);
            response.AsSpan(start, length).CopyTo(packet.AsSpan(12));
            packets.Add(packet);
        }

        return packets;
    }

    private byte[] BuildRulesResponse()
    {
        var payload = new List<byte>();
        payload.AddRange([0xff, 0xff, 0xff, 0xff]);
        payload.Add(RulesResponseType);
        var count = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(count, (ushort)Rules.Count);
        payload.AddRange(count);

        foreach (var rule in Rules)
        {
            AppendString(payload, rule.Key);
            AppendString(payload, rule.Value);
        }

        return payload.ToArray();
    }

    private static void AppendString(List<byte> payload, string value)
    {
        payload.AddRange(Encoding.UTF8.GetBytes(value));
        payload.Add(0x00);
    }
}
