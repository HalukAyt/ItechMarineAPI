namespace ItechMarineAPI.Mqtt;

public class MqttOptions
{
    public string Host { get; set; } = "192.168.1.40";
    public int Port { get; set; } = 1883;
    public bool UseTls { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
    // İstersen ClientId prefix, KeepAlive vb. ekleyebilirsin
}


