namespace ItechMarineAPI.Mqtt;
public interface IMqttPublisher
{
    Task PublishCommandAsync(Guid boatId, object payload, CancellationToken ct = default);
}
