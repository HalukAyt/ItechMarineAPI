using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace ItechMarineAPI.Mqtt;

public sealed class MqttPublisherService : BackgroundService, IMqttPublisher
{
    private readonly ILogger<MqttPublisherService> _logger;
    private readonly MqttOptions _opts;
    private readonly IMqttClient _client;
    private readonly MqttFactory _factory = new();

    public MqttPublisherService(ILogger<MqttPublisherService> logger, IOptions<MqttOptions> opts)
    {
        _logger = logger;
        _opts = opts.Value;
        _client = _factory.CreateMqttClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.DisconnectedAsync += async e =>
        {
            _logger.LogWarning("MQTT disconnected: {Reason}", e.ReasonString);
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            try { await EnsureConnectedAsync(stoppingToken); } catch { /* swallow */ }
        };

        await EnsureConnectedAsync(stoppingToken);

        // canlı tut
        while (!stoppingToken.IsCancellationRequested)
            await Task.Delay(1000, stoppingToken);
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_client.IsConnected) return;

        var clientId = $"{_opts.ClientIdPrefix}-{Environment.MachineName}-{Guid.NewGuid():N}".Substring(0, 22);

        var builder = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(_opts.Host, _opts.Port)
            .WithCleanSession();

        if (!string.IsNullOrWhiteSpace(_opts.Username))
            builder = builder.WithCredentials(_opts.Username, _opts.Password ?? "");

        if (_opts.UseTls)
            builder = builder.WithTls();

        var options = builder.Build();

        _logger.LogInformation("Connecting MQTT {Host}:{Port} ...", _opts.Host, _opts.Port);
        await _client.ConnectAsync(options, ct);
        _logger.LogInformation("✅ MQTT connected");
    }

    public async Task PublishAsync(string topic, string payload, int qos = 0, bool retain = false)
    {
        if (!_client.IsConnected)
        {
            _logger.LogWarning("Publish dropped (not connected) → {Topic}", topic);
            return;
        }

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(Encoding.UTF8.GetBytes(payload))
            .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
            .WithRetainFlag(retain)
            .Build();

        await _client.PublishAsync(msg);
        _logger.LogDebug("📤 MQTT publish → {Topic}: {Payload}", topic, payload);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try { if (_client.IsConnected) await _client.DisconnectAsync(); }
        catch { /* ignore */ }
        await base.StopAsync(cancellationToken);
    }
}
