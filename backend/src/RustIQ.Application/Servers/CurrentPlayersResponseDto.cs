namespace RustIQ.Application.Servers;

public sealed record CurrentPlayersResponseDto(
    DateTimeOffset? RefreshedAtUtc,
    IReadOnlyList<CurrentPlayerDto> Players);
