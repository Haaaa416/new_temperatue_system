using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using MathNet.Numerics.IntegralTransforms;
namespace Batc.Web.Services
{
    public class SignalService
    {
        // ===== 封包參數 =====
        private const int PackageSize = 232;
        private const int Channel = 4;
        private const int Datapoint = 13;
        private const int PointSize = 4;
        private const int StartBytesSize = 20;
        private const int EndBytesSize = 4;
        private const int SampleRate = 250;
        // ===== 訊號處理參數 =====
        private const int NewSignalSize = 130;
        private const int SignalFilterSize = 520;
        private const int MoveSignalSize = 390;

        // ===== 事件 =====
        public event Action<double[], double[]>? OnWaveBlock;
        public event Action<double[], double[]>? OnSpectrum;
        public event Action? OnPacketReceived;

        // ===== 序列埠 =====
        private SerialPort? _port;
        private CancellationTokenSource? _cts;
        private Task? _readerTask;
        private bool _isTransmitting = false;

        // ===== 緩衝區 =====
        private readonly byte[] _readBuffer = new byte[PackageSize];
        private readonly byte[] _bufferMiss = new byte[PackageSize];

        private readonly float[] _tempCh1 = new float[NewSignalSize];
        private readonly float[] _tempCh2 = new float[NewSignalSize];
        private readonly float[] _tempCh3 = new float[NewSignalSize];
        private readonly float[] _tempCh4 = new float[NewSignalSize];

        private readonly float[] _dataCh1 = new float[SignalFilterSize];
        private readonly float[] _dataCh2 = new float[SignalFilterSize];
        private readonly float[] _dataCh3 = new float[SignalFilterSize];
        private readonly float[] _dataCh4 = new float[SignalFilterSize];

        private readonly float[] _drawCh1 = new float[NewSignalSize];
        private readonly float[] _drawCh2 = new float[NewSignalSize];

        private readonly float[] _nftBufferCh1 = new float[NewSignalSize * 3];
        private readonly float[] _nftBufferCh2 = new float[NewSignalSize * 3];
        private readonly float[] _nftCalCh1 = new float[250];
        private readonly float[] _nftCalCh2 = new float[250];

        private int _count = 0;
        private int _datapointCount = 0;
        private int _nftPointCount = 0;

        private readonly SportIirFilter _filter = new(true, true, true);

        public bool IsRunning => _cts is not null && !_cts.IsCancellationRequested;
        public bool IsConnected => _port?.IsOpen == true;

        // ===== 連線（但不啟動傳輸）=====
        public async Task ConnectAsync(string portName, int baud = 460800)
        {
            Console.WriteLine($"========== 連線 COM 埠 ==========");
            Console.WriteLine($"COM 埠: {portName}, Baud: {baud}");

            Stop();

            try
            {
                _port = new SerialPort(portName, baud, Parity.None, 8, StopBits.One)
                {
                    RtsEnable = true,
                    DtrEnable = true,
                    ReadTimeout = 1000
                };

                _port.Open();
                Console.WriteLine("✓ COM 埠已開啟");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 開啟 COM 埠失敗: {ex.Message}");
                throw;
            }

            await Task.CompletedTask;
        }

        // ===== 開始傳輸 =====
        public async Task StartTransmissionAsync()
        {
            if (_port?.IsOpen != true)
                throw new InvalidOperationException("COM 埠未開啟");

            if (_isTransmitting)
                return;

            Console.WriteLine("========== 開始傳輸 ==========");

            _cts = new CancellationTokenSource();
            _isTransmitting = true;

            // 發送開始命令
            SendStartCommand();

            // 啟動讀取執行緒
            _readerTask = Task.Run(() => ReadLoop(_cts.Token), _cts.Token);
            Console.WriteLine("✓ 讀取執行緒已啟動");

            await Task.CompletedTask;
        }

        // ===== 停止傳輸 =====
        public void StopTransmission()
        {
            Console.WriteLine("========== 停止傳輸 ==========");

            _isTransmitting = false;

            try
            {
                if (_port?.IsOpen == true)
                {
                    SendStopCommand();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"發送停止命令錯誤: {ex.Message}");
            }

            try { _cts?.Cancel(); } catch { }
            _cts = null;
            _readerTask = null;

            // 重置濾波器
            _filter.Reset();
            _count = 0;
            _datapointCount = 0;
            _nftPointCount = 0;
        }

        // ===== 完全停止（關閉 COM 埠）=====
        public void Stop()
        {
            Console.WriteLine("========== 完全停止 ==========");

            StopTransmission();

            try { _port?.Close(); } catch { }
            _port?.Dispose();
            _port = null;

            Console.WriteLine("✓ COM 埠已關閉");
        }

        // ===== 發送開始命令 =====
        private void SendStartCommand()
        {
            if (_port?.IsOpen != true) return;

            try
            {
                Console.WriteLine("正在清空輸入緩衝區...");
                _port.DiscardInBuffer();

                var datestart = new byte[10];
                datestart[0] = 0x55;
                datestart[1] = 0x03;
                datestart[2] = 0x06;
                datestart[3] = (byte)(DateTime.Now.Year - 2000);
                datestart[4] = (byte)DateTime.Now.Month;
                datestart[5] = (byte)DateTime.Now.Day;
                datestart[6] = (byte)DateTime.Now.Hour;
                datestart[7] = (byte)DateTime.Now.Minute;
                datestart[8] = (byte)DateTime.Now.Second;
                datestart[9] = 0xAA;

                _port.Write(datestart, 0, 10);
                Console.WriteLine("✓ 已發送開始命令");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 發送開始命令失敗: {ex.Message}");
            }
        }

        // ===== 發送停止命令 =====
        private void SendStopCommand()
        {
            if (_port?.IsOpen != true) return;

            try
            {
                var stop = new byte[] { 0x55, 0x02, 0x00, 0xAA };
                _port.Write(stop, 0, 4);
                _port.DiscardInBuffer();
                Console.WriteLine("✓ 已發送停止命令");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 發送停止命令失敗: {ex.Message}");
            }
        }

        // ===== 讀取迴圈 =====
        private void ReadLoop(CancellationToken ct)
        {
            Console.WriteLine("---------- 讀取迴圈開始 ----------");
            var lastReportTime = DateTime.Now;
            var packetCount = 0;

            while (!ct.IsCancellationRequested && _isTransmitting)
            {
                try
                {
                    if (_port?.IsOpen != true)
                    {
                        Console.WriteLine("✗ 序列埠未開啟，退出讀取迴圈");
                        break;
                    }

                    // 每秒報告一次
                    if ((DateTime.Now - lastReportTime).TotalSeconds >= 1)
                    {
                        Console.WriteLine($"[狀態] 緩衝區: {_port.BytesToRead} bytes, 封包: {packetCount}");
                        lastReportTime = DateTime.Now;
                    }

                    if (_port.BytesToRead >= PackageSize)
                    {
                        _port.Read(_readBuffer, 0, PackageSize);
                        packetCount++;
                        OnPacketReceived?.Invoke();

                        var voltage = DataConvert232(_readBuffer);

                        for (int i = 0; i < Datapoint; i++)
                        {
                            _tempCh1[_count] = voltage[0, i];
                            _tempCh2[_count] = voltage[1, i];
                            _tempCh3[_count] = voltage[2, i];
                            _tempCh4[_count] = voltage[3, i];
                            _count++;
                            _datapointCount++;
                        }

                        if (_count >= NewSignalSize)
                        {
                            ProcessSignal();
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (TimeoutException) { }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ 讀取錯誤: {ex.Message}");
                    Thread.Sleep(10);
                }
            }

            Console.WriteLine($"---------- 讀取迴圈結束 (共 {packetCount} 封包) ----------");
        }

        // ===== 處理訊號 =====
        private void ProcessSignal()
        {
            _count -= NewSignalSize;

            if (_datapointCount > SignalFilterSize)
            {
                // CH1 濾波
                Buffer.BlockCopy(_dataCh1, 4 * NewSignalSize, _dataCh1, 0, 4 * MoveSignalSize);
                Buffer.BlockCopy(_tempCh1, 0, _dataCh1, 4 * MoveSignalSize, 4 * NewSignalSize);

                var ch1Double = new double[SignalFilterSize];
                for (int i = 0; i < SignalFilterSize; i++) ch1Double[i] = _dataCh1[i];
                _filter.ProcessInPlace(ch1Double);
                for (int i = 0; i < NewSignalSize; i++) _drawCh1[i] = (float)ch1Double[MoveSignalSize + i];

                // CH2 濾波
                Buffer.BlockCopy(_dataCh2, 4 * NewSignalSize, _dataCh2, 0, 4 * MoveSignalSize);
                Buffer.BlockCopy(_tempCh2, 0, _dataCh2, 4 * MoveSignalSize, 4 * NewSignalSize);

                var ch2Double = new double[SignalFilterSize];
                for (int i = 0; i < SignalFilterSize; i++) ch2Double[i] = _dataCh2[i];
                _filter.ProcessInPlace(ch2Double);
                for (int i = 0; i < NewSignalSize; i++) _drawCh2[i] = (float)ch2Double[MoveSignalSize + i];

                // CH3, CH4
                Buffer.BlockCopy(_dataCh3, 4 * NewSignalSize, _dataCh3, 0, 4 * MoveSignalSize);
                Buffer.BlockCopy(_tempCh3, 0, _dataCh3, 4 * MoveSignalSize, 4 * NewSignalSize);

                Buffer.BlockCopy(_dataCh4, 4 * NewSignalSize, _dataCh4, 0, 4 * MoveSignalSize);
                Buffer.BlockCopy(_tempCh4, 0, _dataCh4, 4 * MoveSignalSize, 4 * NewSignalSize);

                // 累積 NFT Buffer
                Buffer.BlockCopy(_drawCh1, 0, _nftBufferCh1, 0, 4 * 130);
                Buffer.BlockCopy(_drawCh2, 0, _nftBufferCh2, 0, 4 * 130);

                _nftPointCount += NewSignalSize;

                // 發送波形資料（轉 μV）
                var waveCh1 = new double[NewSignalSize];
                var waveCh2 = new double[NewSignalSize];
                for (int i = 0; i < NewSignalSize; i++)
                {
                    waveCh1[i] = _drawCh1[i] * 1000000;
                    waveCh2[i] = _drawCh2[i] * 1000000;
                }

                OnWaveBlock?.Invoke(waveCh1, waveCh2);

                // 每 250 點計算 FFT
                if (_nftPointCount >= 250)
                {
                    Buffer.BlockCopy(_nftBufferCh1, 0, _nftCalCh1, 0, 4 * 250);
                    Buffer.BlockCopy(_nftBufferCh2, 0, _nftCalCh2, 0, 4 * 250);

                    var fft1 = ComputeSTFT(_nftCalCh1);
                    var fft2 = ComputeSTFT(_nftCalCh2);

                    OnSpectrum?.Invoke(fft1, fft2);

                    Buffer.BlockCopy(_nftBufferCh1, 4 * 250, _nftBufferCh1, 0, 4 * (_nftPointCount - 250));
                    Buffer.BlockCopy(_nftBufferCh2, 4 * 250, _nftBufferCh2, 0, 4 * (_nftPointCount - 250));

                    _nftPointCount -= 250;
                }
            }
            else
            {
                int temp = _datapointCount - NewSignalSize;
                Buffer.BlockCopy(_tempCh1, 0, _dataCh1, 4 * temp, 4 * NewSignalSize);
                Buffer.BlockCopy(_tempCh2, 0, _dataCh2, 4 * temp, 4 * NewSignalSize);
                Buffer.BlockCopy(_tempCh3, 0, _dataCh3, 4 * temp, 4 * NewSignalSize);
                Buffer.BlockCopy(_tempCh4, 0, _dataCh4, 4 * temp, 4 * NewSignalSize);
            }
        }

        // ===== 資料轉換 =====
        private float[,] DataConvert232(byte[] bufferdata)
        {
            var outsignal = new float[Channel, Datapoint];
            var packData = new byte[PackageSize];

            for (int i = 0; i < bufferdata.Length; i++)
            {
                if (i + PackageSize - EndBytesSize + 1 >= bufferdata.Length) break;

                if (bufferdata[i] == 173 &&
                    bufferdata[i + 1] == 222 &&
                    bufferdata[i + PackageSize - EndBytesSize] == 239 &&
                    bufferdata[i + PackageSize - EndBytesSize + 1] == 190)
                {
                    Buffer.BlockCopy(bufferdata, i, packData, 0, PackageSize);

                    if (i < PackageSize)
                    {
                        Buffer.BlockCopy(bufferdata, 0, _bufferMiss, _bufferMiss.Length - i, i);

                        if (_bufferMiss[0] == 173 && _bufferMiss[1] == 222 &&
                            _bufferMiss[PackageSize - EndBytesSize] == 239 &&
                            _bufferMiss[PackageSize - EndBytesSize + 1] == 190)
                        {
                            outsignal = DataConvert13Points(_bufferMiss);
                            Array.Clear(_bufferMiss, 0, _bufferMiss.Length);
                        }
                    }

                    i += PackageSize - 1;
                    outsignal = DataConvert13Points(packData);
                }

                if (i >= bufferdata.Length - PackageSize)
                {
                    Buffer.BlockCopy(bufferdata, i + 1, _bufferMiss, 0, bufferdata.Length - i - 1);
                    break;
                }
            }

            return outsignal;
        }

        // ===== 13 點轉換 =====
        private float[,] DataConvert13Points(byte[] packdata)
        {
            var outsignal = new float[Channel, Datapoint];
            var temp = new byte[3];

            for (int ch = 0; ch < Channel; ch++)
            {
                int offset = StartBytesSize + ch * Datapoint * PointSize;

                for (int pt = 0; pt < Datapoint; pt++)
                {
                    int baseIdx = offset + pt * PointSize;

                    temp[0] = packdata[baseIdx + 2];
                    temp[1] = packdata[baseIdx + 1];
                    temp[2] = packdata[baseIdx];

                    var hex = BitConverter.ToString(temp).Replace("-", "");
                    var value = Convert.ToInt32(hex, 16);

                    if (value >= 10000000)
                        value -= 16777216;

                    outsignal[ch, pt] = value * 9f / 48f / 8388608f;
                }
            }

            return outsignal;
        }

        // ===== FFT =====
        public static double[] ComputeSTFT(float[] data, int windowSize = 250, int hopSize = 125, int nfft = 256)
        {
            if (data.Length < windowSize)
                throw new ArgumentException("數據長度必須大於窗口大小");

            int numFrames = 1 + (data.Length - windowSize) / hopSize;
            var averageSpectrum = new double[nfft / 2 + 1];

            for (int frame = 0; frame < numFrames; frame++)
            {
                var samples = new Complex[nfft];

                for (int i = 0; i < windowSize; i++)
                {
                    int dataIndex = frame * hopSize + i;
                    if (dataIndex < data.Length)
                    {
                        double hammingWindow = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (windowSize - 1));
                        samples[i] = new Complex(data[dataIndex] * hammingWindow, 0);
                    }
                }

                Fourier.Forward(samples, FourierOptions.NoScaling);

                for (int i = 0; i <= nfft / 2; i++)
                {
                    if (i == 0)
                        averageSpectrum[i] += Math.Abs(samples[i].Real);
                    else
                        averageSpectrum[i] += 2.0 * samples[i].Magnitude;
                }
            }

            for (int i = 0; i <= nfft / 2; i++)
                averageSpectrum[i] /= numFrames;

            return averageSpectrum;
        }
    }
}