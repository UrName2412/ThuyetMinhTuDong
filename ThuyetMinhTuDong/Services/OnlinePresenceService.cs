using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ThuyetMinhTuDong.Services
{
    public class OnlinePresenceService
    {
        private const string SupabaseProjectUrl = "https://vkicutmxykziwygemslh.supabase.co";
        private const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InZraWN1dG14eWt6aXd5Z2Vtc2xoIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzU0MTc1NDAsImV4cCI6MjA5MDk5MzU0MH0.SVNFu7wpI-TTLRXDvAOX_KPRXIvX7TEQapi0DjNX2z0";
        private const string Topic = "realtime:public:realtime_users";

        private readonly string _presenceKey = $"maui-{Guid.NewGuid():N}";
        private readonly SemaphoreSlim _syncLock = new(1, 1);
        private readonly StatusService _statusService;
        private bool _isChannelJoined = false;
        private bool _isReconnecting = false;
        private string? _joinRef;
        private ClientWebSocket? _socket;
        private CancellationTokenSource? _cts;
        private Task? _receiveTask;
        private Task? _heartbeatTask;
        private int _refCounter = 1;

        public OnlinePresenceService(StatusService statusService)
        {
            _statusService = statusService;
        }

        public async Task StartAsync()
        {
            await _syncLock.WaitAsync();
            try
            {
                if (_socket != null && (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.Connecting))
                    return;

                if (_socket != null)
                {
                    try { _socket.Dispose(); } catch { }
                    _socket = null;
                }

                _statusService.UpdateStatus("Đang kết nối...", "Orange");
                _isChannelJoined = false;
                _joinRef = null;

                if (_cts != null)
                {
                    try { _cts.Cancel(); } catch { }
                    try { _cts.Dispose(); } catch { }
                    _cts = null;
                }

                _cts = new CancellationTokenSource();
                _socket = new ClientWebSocket();
                _socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

                var wsUrl = BuildRealtimeWebSocketUrl();
                await _socket.ConnectAsync(new Uri(wsUrl), _cts.Token);

                _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
                _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_cts.Token));

                await JoinChannelAsync(_cts.Token);

                DebugLogService.Log("[ONLINE] Presence started, waiting for channel join reply.");
            }
            catch (Exception ex)
            {
                _statusService.UpdateStatus($"Lỗi Khởi Động: {ex.Message}", "Red");
                DebugLogService.Log($"[ONLINE] Start error: {ex.Message}");

                if (_socket != null)
                {
                    try { _socket.Dispose(); } catch { }
                    _socket = null;
                }

                // Chạy ScheduleReconnectAsync ngầm để không giữ Lock của StartAsync
                _ = Task.Run(ScheduleReconnectAsync);
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task StopAsync()
        {
            await _syncLock.WaitAsync();
            try
            {
                _isChannelJoined = false;

                if (_socket != null)
                {
                    if (_socket.State == WebSocketState.Open)
                    {
                        try
                        {
                            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                            await UntrackOnlineAsync(timeoutCts.Token);
                            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "sleep", timeoutCts.Token);
                        }
                        catch { }
                    }

                    try { _socket.Dispose(); } catch { }
                    _socket = null;
                }

                if (_cts != null)
                {
                    try { _cts.Cancel(); } catch { }
                    try { _cts.Dispose(); } catch { }
                    _cts = null;
                }

                DebugLogService.Log("[ONLINE] Presence stopped.");
                _statusService.UpdateStatus("Đã ngắt kết nối", "Gray");
            }
            finally
            {
                _syncLock.Release();
            }
        }

        private static string BuildRealtimeWebSocketUrl()
        {
            var host = SupabaseProjectUrl.Replace("https://", string.Empty).TrimEnd('/');
            return $"wss://{host}/realtime/v1/websocket?apikey={Uri.EscapeDataString(SupabaseAnonKey)}&vsn=1.0.0";
        }

        private async Task JoinChannelAsync(CancellationToken ct)
        {
            var payload = new Dictionary<string, object>
            {
                ["config"] = new Dictionary<string, object>
                {
                    ["broadcast"] = new Dictionary<string, object>
                    {
                        ["self"] = false,
                        ["ack"] = false
                    },
                    ["presence"] = new Dictionary<string, object>
                    {
                        ["key"] = _presenceKey
                    },
                    ["postgres_changes"] = Array.Empty<object>(),
                    ["private"] = false
                }
            };

            DebugLogService.Log($"[ONLINE] >>> phx_join {Topic}");
            var joinRef = NextRef();
            _joinRef = joinRef;
            await SendPhoenixFrameAsync(Topic, "phx_join", payload, joinRef, ct, null);
        }

        private async Task TrackOnlineAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_joinRef))
            {
                DebugLogService.Log("[ONLINE] Track skipped: join_ref is null");
                return;
            }

            DebugLogService.Log($"[ONLINE] >>> presence track {Topic}");

            await SendPhoenixFrameAsync(
                Topic,
                "presence",
                new
                {
                    type = "presence",
                    @event = "track",
                    payload = new
                    {
                        online_at = DateTime.UtcNow.ToString("O"),
                        client = "maui"
                    }
                },
                NextRef(),
                ct,
                _joinRef);
        }

        private async Task UntrackOnlineAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_joinRef))
                return;

            DebugLogService.Log($"[ONLINE] >>> presence untrack {Topic}");

            await SendPhoenixFrameAsync(
                Topic,
                "presence",
                new
                {
                    type = "presence",
                    @event = "untrack"
                },
                NextRef(),
                ct,
                _joinRef);
        }

        private async Task HeartbeatLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(20), ct);
                    if (_socket?.State != WebSocketState.Open)
                        continue;

                    await SendPhoenixFrameAsync("phoenix", "heartbeat", new { }, NextRef(), ct, null);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    DebugLogService.Log($"[ONLINE] Heartbeat error: {ex.Message}");
                    _ = ScheduleReconnectAsync();
                    break;
                }
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[8192];
            while (!ct.IsCancellationRequested && _socket?.State == WebSocketState.Open)
            {
                try
                {
                    var text = await ReceiveTextMessageAsync(buffer, ct);
                    if (text == null)
                        break;

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        DebugLogService.Log($"[ONLINE] WS: {text}");

                        try
                        {
                            using var jsonDoc = JsonDocument.Parse(text);
                            if (TryGetFrameValues(jsonDoc.RootElement, out var frameEvent, out var frameTopic, out var payload) &&
                                frameEvent == "phx_reply")
                            {
                                if (frameTopic == Topic)
                                {
                                    if (payload.HasValue &&
                                        payload.Value.TryGetProperty("status", out var statusProp))
                                    {
                                        var status = statusProp.GetString();
                                        if (status == "ok" && !_isChannelJoined)
                                        {
                                            _isChannelJoined = true;
                                            _statusService.UpdateStatus("KÊNH OK, ĐANG TRACK...", "CornflowerBlue");

                                            await TrackOnlineAsync(ct);

                                            _statusService.UpdateStatus("KẾT NỐI THÀNH CÔNG!", "Green");
                                        }
                                        else if (status == "error")
                                        {
                                            var reason = string.Empty;
                                            if (payload.Value.TryGetProperty("response", out var response) &&
                                                response.TryGetProperty("reason", out var reasonProp))
                                            {
                                                reason = reasonProp.GetString() ?? string.Empty;
                                            }

                                            if (reason.Contains("UnableToConnectToProject", StringComparison.OrdinalIgnoreCase))
                                            {
                                                _statusService.UpdateStatus("Realtime server đang bận, đang thử kết nối lại...", "Orange");
                                                _ = ScheduleReconnectAsync();
                                            }
                                            else
                                            {
                                                _statusService.UpdateStatus($"LỖI JOIN KÊNH: {reason}", "Red");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugLogService.Log($"[ONLINE] Parse WS error: {ex.Message}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    DebugLogService.Log($"[ONLINE] Receive error: {ex.Message}");
                    _statusService.UpdateStatus($"Lỗi Nhận: {ex.Message}", "Red");
                    _ = ScheduleReconnectAsync();
                    break;
                }
            }
        }

        private async Task ScheduleReconnectAsync()
        {
            if (_isReconnecting)
                return;

            _isReconnecting = true;
            try
            {
                await StopAsync();
                await Task.Delay(TimeSpan.FromSeconds(8));
            }
            catch (Exception ex)
            {
                DebugLogService.Log($"[ONLINE] Reconnect error: {ex.Message}");
            }
            finally
            {
                _isReconnecting = false;
            }

            await StartAsync();
        }

        private async Task SendPhoenixFrameAsync(string topic, string eventName, object payload, string reference, CancellationToken ct, string? joinRef)
        {
            if (_socket?.State != WebSocketState.Open)
            {
                DebugLogService.Log($"[ONLINE] Send skipped, socket state: {_socket?.State}");
                return;
            }

            var frame = new Dictionary<string, object?>
            {
                ["join_ref"] = joinRef,
                ["ref"] = reference,
                ["topic"] = topic,
                ["event"] = eventName,
                ["payload"] = payload
            };

            var json = JsonSerializer.Serialize(frame);
            DebugLogService.Log($"[ONLINE] >>> {json}");

            var bytes = Encoding.UTF8.GetBytes(json);
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken: ct);
        }

        private static bool TryGetFrameValues(JsonElement root, out string? frameEvent, out string? frameTopic, out JsonElement? payload)
        {
            frameEvent = null;
            frameTopic = null;
            payload = null;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("event", out var eventProp))
                    frameEvent = eventProp.GetString();
                if (root.TryGetProperty("topic", out var topicProp))
                    frameTopic = topicProp.GetString();
                if (root.TryGetProperty("payload", out var payloadProp))
                    payload = payloadProp;

                return !string.IsNullOrWhiteSpace(frameEvent);
            }

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() >= 5)
            {
                frameTopic = root[2].GetString();
                frameEvent = root[3].GetString();
                payload = root[4];
                return !string.IsNullOrWhiteSpace(frameEvent);
            }

            return false;
        }

        private async Task<string?> ReceiveTextMessageAsync(byte[] buffer, CancellationToken ct)
        {
            if (_socket == null)
                return null;

            using var ms = new MemoryStream();
            WebSocketReceiveResult? result;
            do
            {
                result = await _socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    DebugLogService.Log($"[ONLINE] WS closed by server. Status={_socket.CloseStatus}, Desc={_socket.CloseStatusDescription}");
                    return null;
                }

                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private string NextRef() => Interlocked.Increment(ref _refCounter).ToString();
    }
}
