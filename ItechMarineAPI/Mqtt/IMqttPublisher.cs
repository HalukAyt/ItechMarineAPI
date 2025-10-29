namespace ItechMarineAPI.Mqtt
{
    public interface IMqttPublisher
    {
        Task PublishAsync(string topic, string payload);
        Task PublishAsync(string topic, string payload, int qos, bool retain, CancellationToken ct = default);
    }
}
