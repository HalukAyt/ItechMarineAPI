namespace ItechMarineAPI.Mqtt
{
    public interface IMqttPublisher
    {
        Task PublishAsync(string topic, string payload, int qos = 0, bool retain = false, CancellationToken ct = default);
    }
}
