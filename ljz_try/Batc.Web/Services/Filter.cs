using System;
using System.Collections.Generic;

namespace Batc.Web.Services
{
    // �@�Τ����G��ƺ޽u�u�ݭn�I�s�o��Ӥ�k
    public interface IChannelFilter
    {
        // �b��}�C�W�N�a�o�i�]�䴩�s���y�^
        void ProcessInPlace(double[] buffer);
        // �M���o�i���������A�]�������ժ�/���s�}�l�ɫ�ĳ�I�s�^
        void Reset();
    }

    /// <summary>
    /// ���ĭ� SportIIRFilter�GLow-pass(4 �q) �� High-pass(4 �q) �� 60Hz Notch(4 �q)
    /// �� Direct Form II Transposed�A�i���T������妸���A�C
    /// </summary>
    public sealed class SportIirFilter : IChannelFilter
    {
        // ---- biquad�]a0=1�^----
        private struct Biquad
        {
            public double b0, b1, b2;
            public double a1, a2;
            public double z1, z2; // ���A

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
                // ��s���A
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

        /// <param name="enableLowpass">�w�]�}��</param>
        /// <param name="enableHighpass">�w�]�}��</param>
        /// <param name="enableNotch">�w�]�}�ҡ]60Hz ���^</param>
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

        // �N�a�B�z�]double[]�^
        public void ProcessInPlace(double[] buffer)
        {
            if (buffer is null || buffer.Length == 0 || _chain.Count == 0) return;

            for (int s = 0; s < _chain.Count; s++)
            {
                var sec = _chain[s]; // struct �ƥ�
                for (int n = 0; n < buffer.Length; n++)
                    buffer[n] = sec.Tick(buffer[n]);
                _chain[s] = sec;     // �g�^�]�O�d�s���A�^
            }
        }

        // ��K�A�٦� float[] �ɪ����o�]�D���������A���v�T�J���I�s�^
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

        // ===== �Y�ơ]�u�ΧA�쥻���^ =====

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
