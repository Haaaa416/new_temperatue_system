using System.Collections.ObjectModel;

namespace Batc.Web.Services
{
    public class ChartState
    {
        public ObservableCollection<double> Ch1 { get; } = new();
        public ObservableCollection<double> Ch2 { get; } = new();
        public ObservableCollection<double> Fft1 { get; } = new();
        public ObservableCollection<double> Fft2 { get; } = new();

        // 預設顯示視窗寬度（你頁面有綁到這個）
        public int MaxPoints { get; set; } = 1000;

        public void AppendWave(double[] ch1, double[] ch2)
        {
            foreach (var v in ch1) Ch1.Add(v);
            foreach (var v in ch2) Ch2.Add(v);
            Trim(Ch1); Trim(Ch2);
        }

        public void SetSpectrum(double[] f1, double[] f2)
        {
            Fft1.Clear(); foreach (var v in f1) Fft1.Add(v);
            Fft2.Clear(); foreach (var v in f2) Fft2.Add(v);
        }

        private void Trim(ObservableCollection<double> s)
        {
            while (s.Count > MaxPoints) s.RemoveAt(0);
        }
    }
}
