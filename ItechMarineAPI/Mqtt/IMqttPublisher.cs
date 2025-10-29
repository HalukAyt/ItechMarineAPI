namespace ItechMarineAPI.Mqtt
{
    public interface IMqttPublisher
    {
        Task PublishAsync(string topic, string payload);
    }
}
