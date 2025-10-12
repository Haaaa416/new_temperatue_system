using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace Batc.Web.Services
{
    public class SignalService
    {
        private SerialPort? _port;
        private CancellationTokenSource? _cts;
        private Task? _readerTask;

        public event Action<double[], double[]>? OnWaveBlock;
        public event Action<double[], double[]>? OnSpectrum;

        public bool IsRunning => _cts is not null && !_cts.IsCancellationRequested;

        public async Task StartAsync(string portName, int baud = 115200)
        {
            Stop();

            try
            {
                _cts = new CancellationTokenSource();

                // 嘗試開啟實體序列埠
                _port = new SerialPort(portName, baud)
                {
                    NewLine = "\n",
                    ReadTimeout = 300
                };
                _port.Open();

                // 背景讀取
                _readerTask = Task.Run(() => ReadLoop(_cts.Token));
            }
            catch
            {
                // 若開埠失敗，啟用 Demo 產生器
                _cts = new CancellationTokenSource();
                _readerTask = Task.Run(() => DemoLoop(_cts.Token));
            }

            await Task.CompletedTask;
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _port?.Close(); } catch { }
            _port = null;
            _cts = null;
            _readerTask = null;
        }

        // ---- 讀取實體資料：每收滿 BlockSize 筆送出一次 ----
        private const int BlockSize = 64;

        private void ReadLoop(CancellationToken ct)
        {
            var buf1 = new List<double>(BlockSize);
            var buf2 = new List<double>(BlockSize);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var line = _port!.ReadLine(); // 例如 "12.34,-7.89"
                    if (TryParsePair(line, out var a, out var b))
                    {
                        buf1.Add(a); buf2.Add(b);
                        if (buf1.Count >= BlockSize)
                        {
                            OnWaveBlock?.Invoke(buf1.ToArray(), buf2.ToArray());
                            buf1.Clear(); buf2.Clear();
                        }
                    }
                }
                catch (TimeoutException)
                {
                    // 忽略 timeout，繼續讀
                }
                catch
                {
                    // 發生錯誤就切 Demo，避免整個卡住
                    DemoLoop(ct);
                    return;
                }
            }
        }

        // ---- Demo：沒有硬體也能看到線 ----
        private void DemoLoop(CancellationToken ct)
        {
            var t = 0;
            var dt = 1;
            var amp = 50.0;

            while (!ct.IsCancellationRequested)
            {
                var ch1 = new double[BlockSize];
                var ch2 = new double[BlockSize];
                for (int i = 0; i < BlockSize; i++, t += dt)
                {
                    ch1[i] = amp * Math.Sin(2 * Math.PI * (t % 100) / 48.0);
                    ch2[i] = amp * Math.Cos(2 * Math.PI * (t % 100) / 60.0);
                }
                OnWaveBlock?.Invoke(ch1, ch2);
                Thread.Sleep(25); // 控制更新頻率
            }
        }

        private static bool TryParsePair(string s, out double a, out double b)
        {
            a = b = 0;
            var parts = s.Trim().Split(',', ';', '\t', ' ');
            if (parts.Length < 2) return false;

            return double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out a)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out b);
        }
    }
}
