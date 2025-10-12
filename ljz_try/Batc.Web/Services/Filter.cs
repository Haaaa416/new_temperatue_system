using System;
using System.Collections.Generic;

namespace Batc.Web.Services
{
    // 共用介面：資料管線只需要呼叫這兩個方法
    public interface IChannelFilter
    {
        // 在原陣列上就地濾波（支援連續串流）
        void ProcessInPlace(double[] buffer);
        // 清除濾波器內部狀態（切換受試者/重新開始時建議呼叫）
        void Reset();
    }

    /// <summary>
    /// 等效原 SportIIRFilter：Low-pass(4 段) → High-pass(4 段) → 60Hz Notch(4 段)
    /// 採 Direct Form II Transposed，可正確維持跨批次狀態。
    /// </summary>
    public sealed class SportIirFilter : IChannelFilter
    {
        // ---- biquad（a0=1）----
        private struct Biquad
        {
            public double b0, b1, b2;
            public double a1, a2;
            public double z1, z2; // 狀態

            public Biquad(double[] b, double[] a)
            {
                b0 = b[0]; b1 = b[1]; b2 = b[2];
                // a = [1, a1, a2]
                a1 = a[1]; a2 = a[2];
                z1 = 0; z2 = 0;
            }

            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public double Tick(double x)
            {
                // y = b0*x + z1
                var y = b0 * x + z1;
                // 更新狀態
                z1 = b1 * x - a1 * y + z2;
                z2 = b2 * x - a2 * y;
                return y;
            }

            public void Reset() { z1 = 0; z2 = 0; }
        }

        private readonly List<Biquad> _chain = new();

        public bool EnableLowpass { get; }
        public bool EnableHighpass { get; }
        public bool EnableNotch { get; }

        /// <param name="enableLowpass">預設開啟</param>
        /// <param name="enableHighpass">預設開啟</param>
        /// <param name="enableNotch">預設開啟（60Hz 抑制）</param>
        public SportIirFilter(bool enableLowpass = true, bool enableHighpass = true, bool enableNotch = true)
        {
            EnableLowpass = enableLowpass;
            EnableHighpass = enableHighpass;
            EnableNotch = enableNotch;

            if (EnableLowpass) AddGroup(SportlowGroups);
            if (EnableHighpass) AddGroup(SporthighGroups);
            if (EnableNotch) AddGroup(SportnotchGroups);
        }

        private void AddGroup(double[][][] sosGroup)
        {
            foreach (var sos in sosGroup) // sos[0]=b, sos[1]=a
                _chain.Add(new Biquad(sos[0], sos[1]));
        }

        // 就地處理（double[]）
        public void ProcessInPlace(double[] buffer)
        {
            if (buffer is null || buffer.Length == 0 || _chain.Count == 0) return;

            for (int s = 0; s < _chain.Count; s++)
            {
                var sec = _chain[s]; // struct 複本
                for (int n = 0; n < buffer.Length; n++)
                    buffer[n] = sec.Tick(buffer[n]);
                _chain[s] = sec;     // 寫回（保留新狀態）
            }
        }

        // 方便你還有 float[] 時直接濾（非介面成員，不影響既有呼叫）
        public void ProcessInPlace(float[] buffer)
        {
            if (buffer is null || buffer.Length == 0) return;
            var tmp = new double[buffer.Length];
            for (int i = 0; i < buffer.Length; i++) tmp[i] = buffer[i];
            ProcessInPlace(tmp);
            for (int i = 0; i < buffer.Length; i++) buffer[i] = (float)tmp[i];
        }

        public void Reset()
        {
            for (int i = 0; i < _chain.Count; i++)
            {
                var sec = _chain[i];
                sec.Reset();
                _chain[i] = sec;
            }
        }

        // ===== 係數（沿用你原本的） =====

        // lowpass iir ,order:8,100
        private static readonly double[][][] SportlowGroups = new double[][][]
        {
            new double[][]
            {
                new double[] { 0.8115, 1.6229, 0.8115 },
                new double[] { 1.0000, 1.4516, 0.7943 }
            },
            new double[][]
            {
                new double[] { 0.6818, 1.3637, 0.6818 },
                new double[] { 1.0000, 1.2197, 0.5077 }
            },
            new double[][]
            {
                new double[] { 0.6076, 1.2151, 0.6076 },
                new double[] { 1.0000, 1.0869, 0.3434 }
            },
            new double[][]
            {
                new double[] { 0.5737, 1.1475, 0.5737 },
                new double[] { 1.0000, 1.0264, 0.2686 }
            }
        };

        // highpass iir ,order:8,1.0
        private static readonly double[][][] SporthighGroups = new double[][][]
        {
            new double[][]
            {
                new double[] { 0.9950, -1.9899, 0.9950 },
                new double[] { 1.0000, -1.9896, 0.9902 }
            },
            new double[][]
            {
                new double[] { 0.9861, -1.9722, 0.9861 },
                new double[] { 1.0000, -1.9718, 0.9725 }
            },
            new double[][]
            {
                new double[] { 0.9794, -1.9588, 0.9794 },
                new double[] { 1.0000, -1.9584, 0.9591 }
            },
            new double[][]
            {
                new double[] { 0.9758, -1.9516, 0.9758 },
                new double[] { 1.0000, -1.9513, 0.9519 }
            }
        };

        // bandstop iir ,order:8,59-61
        private static readonly double[][][] SportnotchGroups = new double[][][]
        {
            new double[][]
            {
                new double[] { 0.9902, -0.1244, 0.9902 },
                new double[] { 1.0000, -0.1703, 0.9810 }
            },
            new double[][]
            {
                new double[] { 0.9902, -0.1244, 0.9902 },
                new double[] { 1.0000, -0.0785, 0.9809 }
            },
            new double[][]
            {
                new double[] { 0.9773, -0.1228, 0.9773 },
                new double[] { 1.0000, -0.1415, 0.9546 }
            },
            new double[][]
            {
                new double[] { 0.9773, -0.1228, 0.9773 },
                new double[] { 1.0000, -0.1040, 0.9546 }
            }
        };
    }
}
