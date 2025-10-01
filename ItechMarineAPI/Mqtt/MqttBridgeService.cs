// Mqtt/MqttBridgeService.cs
using System.Text;
using System.Text.Json;
using ItechMarineAPI.Data;
using ItechMarineAPI.Entities;
using ItechMarineAPI.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;
using Microsoft.Extensions.Hosting;   // BackgroundService için
using MQTTnet.Protocol;
using MQTTnet.Client;               // QoS enum’u kullanacaksanız


namespace ItechMarineAPI.Mqtt;

public class MqttOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 1883;
    public string ClientId { get; set; } = "itechmarine-api";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string BaseTopic { get; set; } = "itechmarine";
}

public class MqttBridgeService : BackgroundService, IMqttPublisher
{
    private readonly ILogger<MqttBridgeService> _log;
    private readonly IServiceProvider _sp;
    private readonly MqttOptions _opt;
    private IMqttClient? _client;

    public MqttBridgeService(ILogger<MqttBridgeService> log, IServiceProvider sp, IOptions<MqttOptions> opt)
    { _log = log; _sp = sp; _opt = opt.Value; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        _client.ApplicationMessageReceivedAsync += HandleMessageAsync;

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_opt.Host, _opt.Port)
            .WithClientId(_opt.ClientId + "-" + Guid.NewGuid())
            .WithCredentials(_opt.Username, _opt.Password)
            .Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    await _client.ConnectAsync(options, stoppingToken);
                    _log.LogInformation("MQTT connected");
                    // Telemetry aboneliği: itechmarine/boat/{boatId}/telemetry
                    var topicFilter = $"{_opt.BaseTopic}/boat/+/telemetry";
                    await _client.SubscribeAsync(topicFilter, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, stoppingToken);
                }
                await Task.Delay(2000, stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "MQTT reconnect loop error");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        // konu: itechmarine/boat/{boatId}/telemetry
        var topic = arg.ApplicationMessage.Topic;
        if (!topic.Contains("/telemetry")) return;

        var payload = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hub = scope.ServiceProvider.GetRequiredService<IHubContext<BoatHub>>();

            // topic'ten boatId çek
            // itechmarine/boat/{boatId}/telemetry
            var parts = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var boatId = Guid.Parse(parts[2]);

            // payload ör: { "deviceId":"...", "key":"battery.voltage", "value":"12.6", "timestampUtc":"..." }
            var doc = JsonDocument.Parse(payload).RootElement;
            Guid? deviceId = doc.TryGetProperty("deviceId", out var d) ? d.GetGuid() : null;
            var key = doc.GetProperty("key").GetString()!;
            var value = doc.GetProperty("value").GetString()!;
            var ts = doc.TryGetProperty("timestampUtc", out var tt) ? tt.GetDateTime() : DateTime.UtcNow;

            var tel = new Telemetry { BoatId = boatId, DeviceId = deviceId, Key = key, Value = value, CreatedAt = ts };
            db.Telemetries.Add(tel);
            await db.SaveChangesAsync();

            await hub.Clients.Group($"boat:{boatId}").SendAsync("telemetry", new { tel.Key, tel.Value, tel.CreatedAt });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "MQTT telemetry handle failed. Topic={Topic} Payload={Payload}", topic, payload);
        }
    }

    // Owner tarafından komut yayınlamak için bu yardımcıyı static değil DI üzerinden çağıracağız.
    public async Task PublishCommandAsync(Guid boatId, object payload, CancellationToken ct = default)
    {
        if (_client is null || !_client.IsConnected) return;
        var topic = $"{_opt.BaseTopic}/boat/{boatId}/commands";
        var json = JsonSerializer.Serialize(payload);
        var msg = new MqttApplicationMessageBuilder()
    .WithTopic(topic)
    .WithPayload(json)
    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce) 
    .Build();

        await _client.PublishAsync(msg, ct);
    }
}
