namespace ItechMarineAPI.Mqtt;

public class MqttOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 1883;
    public string ClientId { get; set; } = "itechmarine-api";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string BaseTopic { get; set; } = "itechmarine";
    public bool UseTls { get; set; } = true;  // 🔥 TLS flag'ini ekledik
}
