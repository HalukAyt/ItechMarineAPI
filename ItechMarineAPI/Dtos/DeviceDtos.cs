namespace ItechMarineAPI.Dtos
{
    public record DeviceCreateDto(string Name);
    public record DeviceDto(Guid Id, string Name, bool IsActive, bool IsOnline, DateTime? LastSeenUtc, Guid BoatId);

    public record RotateKeyResponseDto(string NewDeviceKey);

    public record CommandCreateDto(string Type, object Payload); // owner -> device
    public record CommandEnvelopeDto(long Id, string Type, string PayloadJson, DateTime CreatedAt);
    public record CommandAckDto(long Id);
    
}
