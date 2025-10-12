using System;
using System.Collections.Generic;
using System.IO.Ports;
using MathNet.Numerics.IntegralTransforms;

namespace Batc.Web.Services
{
    public sealed class SignalService : IAsyncDisposable
    {
        // ======= 依你的裝置封包規格調這些常數 =======
        private static readonly byte[] HEAD = new byte[] { 0xAD, 0xDE };
        private static readonly byte[] TAIL = new byte[] { 0xEF, 0xBE };
        private const int CHANNELS = 4;    // 裝置通道數（原專案 4），UI 只畫 CH1/CH2
        private const int SAMPLERATE = 250;  // 取樣率（原專案 250Hz）
        private const bool USE_INT24 = true; // 是否為 24-bit 單樣本
        private const double SCALE = 1.0;  // 縮放係數（依實機調整）

        // 若資料有額外 header/footer，這裡調整
        private const int EXTRA_HEADER_BYTES = 0;
        private const int EXTRA_TAIL_BYTES = 0;

        private readonly object _gate = new();
        private readonly List<byte> _cache = new();

        private CancellationTokenSource? _cts;
        private SerialPort? _port;

        // 對 UI 的事件
        public event Action<double[], double[]>? OnWaveBlock;  // (ch1, ch2)
        public event Action<double[], double[]>? OnSpectrum;   // (fft1, fft2)

        // 每通道獨立濾波器（IIR 有狀態，不能共用）
        private readonly IChannelFilter _fCh1;
        private readonly IChannelFilter _fCh2;

        // 用 DI 的工廠建立兩個相互獨立實例
        public SignalService(Func<IChannelFilter> filterFactory)
        {
            _fCh1 = filterFactory();
            _fCh2 = filterFactory();
        }

        public Task StartAsync(string portName, int baud = 460800)
        {
            Stop();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            _port = new SerialPort(portName, baud)
            {
                DtrEnable = true,
                RtsEnable = true,
                ReadTimeout = 500,
                WriteTimeout = 500
            };
            _port.Open();

            // 開始前重置濾波器狀態
            _fCh1.Reset();
            _fCh2.Reset();

            _ = Task.Run(() => ReadLoop(_cts.Token), _cts.Token);
            return Task.CompletedTask;
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _port?.Close(); } catch { }
            _port = null;
        }

        private async Task ReadLoop(CancellationToken ct)
        {
            var buf = new byte[4096];
            var secCh1 = new List<double>(SAMPLERATE);
            var secCh2 = new List<double>(SAMPLERATE);

            while (!ct.IsCancellationRequested && _port?.IsOpen == true)
            {
                int n;
                try { n = _port.Read(buf, 0, buf.Length); }
                catch (TimeoutException) { continue; }

                if (n <= 0) continue;

                foreach (var frame in SyncAndSlice(buf.AsSpan(0, n).ToArray()))
                {
                    var data = DecodeFrame(frame); // double[CHANNELS][points]

                    var ch1 = (double[])data[0].Clone();
                    var ch2 = (double[])data[1].Clone();

                    _fCh1.ProcessInPlace(ch1);
                    _fCh2.ProcessInPlace(ch2);

                    OnWaveBlock?.Invoke(ch1, ch2);

                    CollectForOneSecond(secCh1, ch1);
                    CollectForOneSecond(secCh2, ch2);
                    if (secCh1.Count >= SAMPLERATE && secCh2.Count >= SAMPLERATE)
                    {
                        OnSpectrum?.Invoke(
                            ComputeMagnitudeSpectrum(secCh1.ToArray()),
                            ComputeMagnitudeSpectrum(secCh2.ToArray()));
                        secCh1.Clear(); secCh2.Clear();
                    }
                }

                await Task.Yield();
            }
        }

        // --- 封包對齊 ---
        private IEnumerable<byte[]> SyncAndSlice(byte[] chunk)
        {
            lock (_gate)
            {
                _cache.AddRange(chunk);

                while (true)
                {
                    int start = FindSeq(_cache, HEAD, 0);
                    if (start < 0) { if (_cache.Count > 8192) _cache.Clear(); yield break; }

                    int end = FindSeq(_cache, TAIL, start + HEAD.Length);
                    if (end < 0) yield break;

                    int frameLen = end + TAIL.Length - start;
                    var frame = _cache.GetRange(start, frameLen).ToArray();
                    _cache.RemoveRange(0, end + TAIL.Length);
                    yield return frame;
                }
            }
        }


        private static int FindSeq(List<byte> data, byte[] seq, int from)
        {
            for (int i = from; i <= data.Count - seq.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < seq.Length; j++)
                    if (data[i + j] != seq[j]) { ok = false; break; }
                if (ok) return i;
            }
            return -1;
        }

        // --- 解碼 ---
        private double[][] DecodeFrame(byte[] frame)
        {
            int payloadStart = HEAD.Length + EXTRA_HEADER_BYTES;
            int payloadEnd = frame.Length - TAIL.Length - EXTRA_TAIL_BYTES;
            int payloadLen = payloadEnd - payloadStart;
            if (payloadLen <= 0) return EmptyChannels(0);

            int bpsPerCh = USE_INT24 ? 3 : 2;
            int bytesPerPoint = CHANNELS * bpsPerCh;

            int points = payloadLen / bytesPerPoint;
            if (points <= 0) return EmptyChannels(0);

            var ch = new double[CHANNELS][];
            for (int c = 0; c < CHANNELS; c++) ch[c] = new double[points];

            int ofs = payloadStart;
            for (int i = 0; i < points; i++)
            {
                for (int c = 0; c < CHANNELS; c++)
                {
                    double val;
                    if (USE_INT24)
                    {
                        int raw = Int24LE(frame[ofs], frame[ofs + 1], frame[ofs + 2]);
                        ofs += 3;
                        val = raw * SCALE;
                    }
                    else
                    {
                        short raw = BitConverter.ToInt16(frame, ofs);
                        ofs += 2;
                        val = raw * SCALE;
                    }
                    ch[c][i] = val;
                }
            }
            return ch;
        }

        private static double[][] EmptyChannels(int points)
        {
            var ch = new double[CHANNELS][];
            for (int c = 0; c < CHANNELS; c++) ch[c] = new double[points];
            return ch;
        }

        private static int Int24LE(byte b0, byte b1, byte b2)
        {
            int v = b0 | (b1 << 8) | (b2 << 16);
            if ((v & 0x0080_0000) != 0) v |= unchecked((int)0xFF00_0000); // 符號延伸
            return v;
        }

        private static double[] ComputeMagnitudeSpectrum(double[] time)
        {
            int n = time.Length;
            var buf = new System.Numerics.Complex[n];

            for (int i = 0; i < n; i++)
            {
                double w = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (n - 1)); // Hamming
                buf[i] = new System.Numerics.Complex(time[i] * w, 0);
            }
            Fourier.Forward(buf, FourierOptions.Matlab);

            int half = n / 2;
            var mag = new double[half + 1];
            for (int k = 0; k <= half; k++) mag[k] = buf[k].Magnitude;
            return mag;
        }

        private static void CollectForOneSecond(List<double> acc, double[] block)
        {
            acc.AddRange(block);
            if (acc.Count > SAMPLERATE) acc.RemoveRange(0, acc.Count - SAMPLERATE);
        }

        public async ValueTask DisposeAsync()
        {
            Stop();
            _cts?.Dispose();
            await Task.CompletedTask;
        }
    }
}
