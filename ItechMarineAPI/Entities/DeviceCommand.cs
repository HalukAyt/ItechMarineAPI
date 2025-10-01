namespace ItechMarineAPI.Entities;

public class DeviceCommand
{
    public long Id { get; set; }
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = default!;
    public string Type { get; set; } = default!;         // ör: "channel.set"
    public string PayloadJson { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DequeuedAt { get; set; }
    public DateTime? AckedAt { get; set; }
}
