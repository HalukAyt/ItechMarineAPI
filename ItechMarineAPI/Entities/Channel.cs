namespace ItechMarineAPI.Entities
{
    public enum ChannelType { Light, Pump, Fan, Aux }

    public class Channel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid BoatId { get; set; }
        public Boat Boat { get; set; } = default!;
        public string Name { get; set; } = default!;
        public ChannelType Type { get; set; }
        public int Pin { get; set; }          // Cihaz tarafı için
        public bool State { get; set; }       // Açık/Kapalı
    }
}
