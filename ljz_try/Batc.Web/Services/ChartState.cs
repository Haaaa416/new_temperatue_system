using System.Collections.ObjectModel;

namespace Batc.Web.Services;

public sealed class ChartState
{
	public ObservableCollection<double> Ch1 { get; } = new();
	public ObservableCollection<double> Ch2 { get; } = new();

	public ObservableCollection<double> Fft1 { get; } = new();
	public ObservableCollection<double> Fft2 { get; } = new();

	public int MaxPoints { get; set; } = 1000; // 時間視窗寬度

	public void AppendWave(double[] ch1, double[] ch2)
	{
		Append(Ch1, ch1);
		Append(Ch2, ch2);
	}

	public void SetSpectrum(double[] fft1, double[] fft2)
	{
		Replace(Fft1, fft1);
		Replace(Fft2, fft2);
	}

	private void Append(ObservableCollection<double> series, double[] incoming)
	{
		foreach (var v in incoming) series.Add(v);
		while (series.Count > MaxPoints) series.RemoveAt(0);
	}

	private void Replace(ObservableCollection<double> series, double[] data)
	{
		series.Clear();
		foreach (var v in data) series.Add(v);
	}
}
