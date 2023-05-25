using MathNet.Numerics.IntegralTransforms;
namespace NeurCLib;
/// <summary>
/// Stores a window of the signal received from the arduino.
/// </summary>
public class SignalWindow {
  Queue<double> q_microvolts = new();
  double[] weights;
  double intercept;
  int max_size = 178;
  int weight_size = 45;
  /// <summary>
  /// Make a prediction every # of inputs
  /// </summary>
  int sample_rate = 50;
  int current_sample = 0;
  bool predict_ready = false;
  public bool PredictReady {
    get => predict_ready;
  }
  public SignalWindow() {
    weights = new double[weight_size];
    intercept = 0.0;
    // TODO load weights and intercept
  }
  public void add(double uv) {
    if (q_microvolts.Count >= max_size) {
      q_microvolts.Dequeue();
    }
    q_microvolts.Enqueue(uv);
    current_sample++;
    if (current_sample >= sample_rate) {
      predict_ready = true;
    }
  }
  public bool predict() {
    if (q_microvolts.Count < max_size) return false;
    current_sample = 0;
    double[] signal = q_microvolts.ToArray();
    double[] signalComplex = new double[max_size];
    Fourier.Forward(signal, signalComplex, FourierOptions.NoScaling);
    double sum = 0.0;
    for (int i = 0; i <= weight_size; i++) {
      double psd = Math.Sqrt(signal[i] * signal[i] + signalComplex[i] * signalComplex[i]);
      sum += weights[i] * psd;
    }
    if (sum + intercept > 0) {
      return true;
    }
    return false;
  }
}