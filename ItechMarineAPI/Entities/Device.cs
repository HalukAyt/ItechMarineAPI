namespace ItechMarineAPI.Entities
{
    public class Device
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid BoatId { get; set; }
        public Boat Boat { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string DeviceKeyHash { get; set; } = default!; // HMAC için hash
        public bool IsActive { get; set; } = true;
        public string DeviceKeyProtected { get; set; } = default!;   // AES/DataProtection ile saklanan plain key
    }
}
