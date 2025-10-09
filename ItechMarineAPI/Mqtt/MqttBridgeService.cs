using System.Text;
using System.Text.Json;
using ItechMarineAPI.Data;
using ItechMarineAPI.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace ItechMarineAPI.Mqtt
{

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
                try { await ConnectAndSubscribeAsync(stoppingToken); } catch { /* ignore */ }
            };

            _client.ApplicationMessageReceivedAsync += async e =>
            {
                try
                {
                    var topic = e.ApplicationMessage.Topic ?? "";
                    var seg = e.ApplicationMessage.PayloadSegment;
                    string body = seg.Array is null ? string.Empty : Encoding.UTF8.GetString(seg.Array, seg.Offset, seg.Count);

                    if (topic.EndsWith("/status", StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleStatusAsync(topic, body);
                        return;
                    }

                    if (topic.Contains("/channel/", StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleChannelStateAsync(topic, body);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MQTT message handling error");
                }
            };

            await ConnectAndSubscribeAsync(stoppingToken);
        }

        private async Task ConnectAndSubscribeAsync(CancellationToken ct)
        {
            if (_client is null) return;

            var builder = new MqttClientOptionsBuilder().WithTcpServer(_opt.Host, _opt.Port);
            if (_opt.UseTls) builder = builder.WithTls();
            if (!string.IsNullOrWhiteSpace(_opt.Username)) builder = builder.WithCredentials(_opt.Username, _opt.Password);

            _logger.LogInformation("Connecting MQTT {Host}:{Port} ...", _opt.Host, _opt.Port);
            await _client.ConnectAsync(builder.Build(), ct);
            _logger.LogInformation("MQTT connected");

            var subStatus = new MqttTopicFilterBuilder()
                .WithTopic("itechmarine/device/+/status")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            var subState = new MqttTopicFilterBuilder()
                .WithTopic("itechmarine/device/+/channel/+")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _client.SubscribeAsync(subStatus, ct);
            await _client.SubscribeAsync(subState, ct);

            _logger.LogInformation("SUB ␦ itechmarine/device/+/status");
            _logger.LogInformation("SUB ␦ itechmarine/device/+/channel/+");
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
            _logger.LogInformation("MQTT PUSH ␦ {Topic} : {Payload}", topic, payload);
        }

        private async Task HandleStatusAsync(string topic, string body)
        {
            var parts = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return;
            var deviceIdStr = parts[^2];
            if (!Guid.TryParse(deviceIdStr, out var deviceId)) return;

            var online = string.Equals(body, "online", StringComparison.OrdinalIgnoreCase);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var dev = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
            if (dev is null) return;

            dev.IsOnline = online;
            dev.LastSeenUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();

            _logger.LogInformation("STATUS ␦ device={DeviceId} {Status}", deviceId, online ? "online" : "offline");
        }

        private async Task HandleChannelStateAsync(string topic, string body)
        {
            var parts = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6) return;

            var deviceIdStr = parts[^3];
            var pinStr = parts[^1];

            if (!Guid.TryParse(deviceIdStr, out var deviceId)) return;
            if (!int.TryParse(pinStr, out var pin)) return;

            bool? state = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(body))
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("state", out var s))
                    {
                        if (s.ValueKind == JsonValueKind.True) state = true;
                        else if (s.ValueKind == JsonValueKind.False) state = false;
                    }
                }
            }
            catch { return; }

            if (!state.HasValue) return;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var ch = await db.Channels
                .Include(c => c.Boat)
                .Join(db.Devices.Where(d => d.Id == deviceId),
                      c => c.BoatId,
                      d => d.BoatId,
                      (c, d) => c)
                .FirstOrDefaultAsync(c => c.Pin == pin);

            if (ch is null) return;

            ch.State = state.Value;
            await db.SaveChangesAsync();

            _logger.LogInformation("STATE ␦ device={DeviceId} pin={Pin} state={State}", deviceId, pin, state);

            var hub = scope.ServiceProvider.GetRequiredService<IHubContext<BoatHub>>();
            await hub.Clients.Group(ch.BoatId.ToString()).SendAsync("channel.state", new
            {
                channelId = ch.Id,
                pin = ch.Pin,
                state = ch.State
            });
        }
    }
}
