namespace ItechMarineAPI.Dtos
{
    public record BoatCreateDto(string Name);
    public record BoatDto(Guid Id, string Name);
    public record BoatStatusDto(bool Online, DateTime? LastSeenUtc);
}
