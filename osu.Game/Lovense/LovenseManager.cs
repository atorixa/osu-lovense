using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Configuration;

namespace osu.Game.Lovense
{
    public partial class LovenseManager : Component
    {
        private readonly Bindable<bool> isEnabled = new Bindable<bool>();
        private readonly Bindable<string> apiUrl = new Bindable<string>();
        private readonly Bindable<int> baseIntensity = new Bindable<int>();
        private readonly Bindable<int> deviceIndex = new Bindable<int>();

        private ClientWebSocket ws;
        private CancellationTokenSource cts;
        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        private int messageId = 1;

        private readonly object throttleLock = new object();
        private long lastSendRealTime = 0;
        private double lastSentIntensity = -1;
        private const int MIN_SEND_INTERVAL_MS = 50;

        private ScheduledDelegate stopVibrationDelegate;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.LovenseEnabled, isEnabled);
            config.BindWith(OsuSetting.IntifaceUrl, apiUrl);
            config.BindWith(OsuSetting.LovenseIntensity, baseIntensity);

            config.BindWith(OsuSetting.LovenseDeviceIndex, deviceIndex);

            isEnabled.BindValueChanged(e =>
            {
                if (e.NewValue)
                    _ = connectAsync();
                else
                    disconnect();
            });

            deviceIndex.BindValueChanged(e =>
            {
                if (isEnabled.Value && ws != null && ws.State == WebSocketState.Open)
                {
                    string stopCmd = $"[{{\"ScalarCmd\":{{\"Id\":{GetNextId()},\"DeviceIndex\":{e.OldValue},\"Scalars\":[{{\"Index\":0,\"Scalar\":0.0,\"ActuatorType\":\"Vibrate\"}}]}}}}]";
                    _ = sendRawJson(stopCmd);
                }
            });

            if (isEnabled.Value)
                _ = connectAsync();
        }

        private async Task connectAsync()
        {
            if (ws != null && (ws.State == WebSocketState.Open || ws.State == WebSocketState.Connecting))
                return;

            ws = new ClientWebSocket();
            cts = new CancellationTokenSource();

            string url = string.IsNullOrEmpty(apiUrl.Value) ? "ws://127.0.0.1:12345" : apiUrl.Value;

            try
            {
                await ws.ConnectAsync(new Uri(url), cts.Token);
                Logger.Log("Lovense: Connected!", LoggingTarget.Runtime, LogLevel.Important);

                _ = Task.Run(() => receiveLoop(ws, cts.Token), cts.Token);

                await sendRawJson($"[{{\"RequestServerInfo\":{{\"Id\":{GetNextId()},\"ClientName\":\"osu!lazer\",\"MessageVersion\":3}}}}]");
                await sendRawJson($"[{{\"StartScanning\":{{\"Id\":{GetNextId()}}}}}]");
            }
            catch (Exception ex)
            {
                Logger.Log($"Lovense: connect error: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                disconnect();
            }
        }

        private async Task receiveLoop(ClientWebSocket socket, CancellationToken token)
        {
            var buffer = new byte[4096];
            try
            {
                while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                        {
                            disconnect();
                            break;
                        }
                }
            }
            catch { }
        }

        private void disconnect()
        {
            try
            {
                if (ws != null && ws.State == WebSocketState.Open)
                {
                    _ = setVibration(0.0, true);
                    ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None).Wait(1000);
                }
            }
            catch { }
            finally
            {
                cts?.Cancel();
                ws?.Dispose();
                ws = null;
                cts?.Dispose();
                cts = null;
            }
        }

        private int GetNextId() => Interlocked.Increment(ref messageId);

        private async Task sendRawJson(string json)
        {
            if (!isEnabled.Value || ws == null || ws.State != WebSocketState.Open) return;

            await sendLock.WaitAsync();
            try
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
            }
            catch (Exception ex)
            {
                Logger.Log($"Lovense: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                disconnect();
            }
            finally
            {
                sendLock.Release();
            }
        }

        private async Task setVibration(double intensity, bool force = false)
        {
            if (!isEnabled.Value) return;

            intensity = Math.Clamp(intensity, 0.0, 1.0);
            long now = Environment.TickCount64;

            if (!force)
            {
                lock (throttleLock)
                {
                    if (now - lastSendRealTime < MIN_SEND_INTERVAL_MS) return;
                    if (Math.Abs(intensity - lastSentIntensity) < 0.03) return;

                    lastSendRealTime = now;
                    lastSentIntensity = intensity;
                }
            }
            else
            {
                lock (throttleLock)
                {
                    lastSendRealTime = now;
                    lastSentIntensity = intensity;
                }
            }

            if (ws == null || ws.State != WebSocketState.Open)
            {
                await connectAsync();
                if (ws == null || ws.State != WebSocketState.Open) return;
            }

            string intensityString = intensity.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string cmd = $"[{{\"ScalarCmd\":{{\"Id\":{GetNextId()},\"DeviceIndex\":{deviceIndex.Value},\"Scalars\":[{{\"Index\":0,\"Scalar\":{intensityString},\"ActuatorType\":\"Vibrate\"}}]}}}}]";
            await sendRawJson(cmd);
        }

        public void StartVibration(double customIntensity = -1) => _ = setVibration(customIntensity >= 0 ? customIntensity : (baseIntensity.Value / 100.0), true);
        public void StopVibration() => _ = setVibration(0.0, true);
        public void VibrateBriefly(double customIntensity = -1, double durationMs = 150)

        {
            StartVibration(customIntensity);
            stopVibrationDelegate?.Cancel();
            stopVibrationDelegate = Scheduler.AddDelayed(StopVibration, durationMs);
        }

        public void UpdateSlider(double progress, double minMultiplier = 0.2)
        {
            if (!isEnabled.Value) return;

            double maxUserPower = baseIntensity.Value / 100.0;
            double current = (minMultiplier + ((1.0 - minMultiplier) * progress)) * maxUserPower;

            _ = setVibration(current);
        }

        public void UpdateSpinner(double rpm, double maxRpm = 400.0)
        {
            if (!isEnabled.Value) return;

            double maxUserPower = baseIntensity.Value / 100.0;
            double progress = Math.Clamp(rpm / maxRpm, 0.0, 1.0);
            double current = (0.1 + (0.9 * progress)) * maxUserPower;

            _ = setVibration(current);
        }

        protected override void Dispose(bool isDisposing)
        {
            disconnect();
            base.Dispose(isDisposing);
        }
    }
}
