using System.Threading.Channels;

namespace ItechMarineAPI.Entities
{
    public class Boat
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = default!;
        public Guid OwnerId { get; set; }
        public AppUser Owner { get; set; } = default!;
        public ICollection<Device> Devices { get; set; } = new List<Device>();
        public ICollection<Channel> Channels { get; set; } = new List<Channel>();
    }
}
