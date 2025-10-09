using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItechMarineAPI.Entities
{
    public class Device
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey(nameof(Boat))]
        public Guid BoatId { get; set; }
        public Boat? Boat { get; set; }

        [MaxLength(100)]
        public string Name { get; set; } = "ESP32";

        // HMAC için korunmuş anahtar (IDataProtection ile)
        [Required]
        public string DeviceKeyProtected { get; set; } = default!;

        public bool IsActive { get; set; } = true;

        // 🔵 YENİ: Canlılık bilgisi
        public bool IsOnline { get; set; } = false;

        // 🔵 YENİ: Son görüldüğü zaman (UTC)
        public DateTime? LastSeenUtc { get; set; }
    }
}
