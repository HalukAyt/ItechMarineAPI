namespace ItechMarineAPI.Mqtt;

public sealed class MqttOptions
{
    public string Host { get; set; } = "127.0.0.1"; // Mosquitto / broker IP
    public int Port { get; set; } = 1883;
    public bool UseTls { get; set; } = false;

    public string? Username { get; set; }
    public string? Password { get; set; }

    // istemci kimliği prefix’i
    public string ClientIdPrefix { get; set; } = "ItechMarineAPI";
}
