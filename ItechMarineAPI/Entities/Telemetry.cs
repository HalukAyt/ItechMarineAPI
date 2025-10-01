namespace ItechMarineAPI.Entities
{
    public class Telemetry
    {
        public long Id { get; set; }
        public Guid BoatId { get; set; }
        public Guid? DeviceId { get; set; }
        public string Key { get; set; } = default!;   // e.g. "battery.voltage"
        public string Value { get; set; } = default!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
