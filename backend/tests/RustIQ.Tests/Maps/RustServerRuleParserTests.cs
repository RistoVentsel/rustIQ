using System.Buffers.Binary;
using System.Text;
using RustIQ.Infrastructure.Maps;

namespace RustIQ.Tests.Maps;

public sealed class RustServerRuleParserTests
{
    [Fact]
    public void Parse_ReadsRustMapRules()
    {
        var payload = BuildRulesPayload(
            ("world.seed", "1337"),
            ("world.size", "4500"),
            ("level_url", "https://maps.rustmaps.com/286/c5cca32d0a93407c9e2d1cf6ab1b01f9/4500_test.map"),
            ("map_image_url", "https://files.facepunch.com/rust/map-images/test.jpg"));

        var rules = RustServerRuleParser.Parse(payload);

        Assert.Equal("1337", rules["world.seed"]);
        Assert.Equal("4500", rules["world.size"]);
        Assert.Equal(
            "https://files.facepunch.com/rust/map-images/test.jpg",
            rules["map_image_url"]);
    }

    [Theory]
    [InlineData(
        "https://maps.rustmaps.com/286/c5cca32d0a93407c9e2d1cf6ab1b01f9/4500_test.map",
        "https://rustmaps.com/map/c5cca32d0a93407c9e2d1cf6ab1b01f9")]
    [InlineData("https://example.com/map/test.map", null)]
    [InlineData("not-a-url", null)]
    public void BuildRustMapsUrl_UsesMapIdFromRustMapsFileUrl(
        string mapFileUrl,
        string? expected)
    {
        Assert.Equal(expected, RustServerRuleParser.BuildRustMapsUrl(mapFileUrl));
    }

    private static byte[] BuildRulesPayload(params (string Name, string Value)[] rules)
    {
        using var stream = new MemoryStream();
        stream.Write([0xff, 0xff, 0xff, 0xff, 0x45]);

        Span<byte> ruleCount = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(ruleCount, checked((ushort)rules.Length));
        stream.Write(ruleCount);

        foreach (var (name, value) in rules)
        {
            WriteNullTerminated(stream, name);
            WriteNullTerminated(stream, value);
        }

        return stream.ToArray();
    }

    private static void WriteNullTerminated(Stream stream, string value)
    {
        stream.Write(Encoding.UTF8.GetBytes(value));
        stream.WriteByte(0);
    }
}
