namespace ItechMarineAPI.Entities
{
    public class RefreshToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string TokenHash { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }

        // Üretim
        public static (RefreshToken token, string plain) Create(Guid userId, TimeSpan life)
        {
            var plain = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + "-" + Guid.NewGuid();
            return (new RefreshToken
            {
                UserId = userId,
                TokenHash = Hash(plain),
                ExpiresAt = DateTime.UtcNow.Add(life)
            }, plain);
        }

        public static string Hash(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }
    }
}
