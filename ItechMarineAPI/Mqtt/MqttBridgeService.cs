using ItechMarineAPI.Mqtt;
using Microsoft.Extensions.Options;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet;
using System.Text;
using ItechMarineAPI.Data;
using Microsoft.EntityFrameworkCore;

public class MqttBridgeService : BackgroundService, IMqttPublisher
{
    private readonly ILogger<MqttBridgeService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MqttOptions _opt;

    private IMqttClient? _client;

    public MqttBridgeService(
        ILogger<MqttBridgeService> logger,
        IOptions<MqttOptions> opt,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _opt = opt.Value;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        _client.DisconnectedAsync += async e =>
        {
            _logger.LogWarning("MQTT disconnected: {Reason}", e.Reason);
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            try { await ConnectAsync(stoppingToken); } catch { /* swallow */ }
        };

        _client.ApplicationMessageReceivedAsync += async e =>
        {
            try
            {
                var topic = e.ApplicationMessage.Topic ?? "";
                if (topic.EndsWith("/status", StringComparison.OrdinalIgnoreCase))
                {
                    // Topic: itechmarine/device/<deviceId>/status
                    var parts = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        var deviceIdStr = parts[^2];
                        if (Guid.TryParse(deviceIdStr, out var deviceId))
                        {
                            var payload = e.ApplicationMessage.PayloadSegment;
                            var status = payload.Array is null
                                ? ""
                                : Encoding.UTF8.GetString(payload);

                            var online = string.Equals(status, "online", StringComparison.OrdinalIgnoreCase);

                            using var scope = _scopeFactory.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                            var dev = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
                            if (dev is not null)
                            {
                                dev.IsOnline = online;
                                dev.LastSeenUtc = DateTime.UtcNow;
                                await db.SaveChangesAsync();

                                _logger.LogInformation("STATUS → device={DeviceId} {Status}", deviceId, status);
                            }
                        }
                    }
                }

                // (Gerekirse başka topic’leri de burada ele al)
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT message handling error");
            }
        };

        await ConnectAsync(stoppingToken);
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        if (_client is null) return;

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_opt.Host, _opt.Port);

        if (_opt.UseTls)
            builder = builder.WithTls();

        if (!string.IsNullOrWhiteSpace(_opt.Username))
            builder = builder.WithCredentials(_opt.Username, _opt.Password);

        _logger.LogInformation("Connecting MQTT {Host}:{Port} ...", _opt.Host, _opt.Port);
        await _client.ConnectAsync(builder.Build(), ct);
        _logger.LogInformation("MQTT connected");

        // 🔵 Status topic’ine abone ol
        var sub = new MqttTopicFilterBuilder()
            .WithTopic("itechmarine/device/+/status")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce) // QoS 1
            .Build();

        await _client.SubscribeAsync(sub, ct);
        _logger.LogInformation("SUB → itechmarine/device/+/status");
    }

    public async Task PublishAsync(string topic, string payload, int qos = 0, bool retain = false, CancellationToken ct = default)
    {
        if (_client is null || !_client.IsConnected)
        {
            _logger.LogWarning("MQTT publish skipped (not connected) topic={Topic}", topic);
            return;
        }

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithRetainFlag(retain)
            .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
            .Build();

        await _client.PublishAsync(msg, ct);
    }
}
