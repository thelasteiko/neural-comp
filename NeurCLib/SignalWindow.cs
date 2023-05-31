using MathNet.Numerics.IntegralTransforms;
namespace NeurCLib;
/// <summary>
/// Stores a window of the signal received from the arduino and calculates predictions
/// based on stored weights.
/// </summary>
public class SignalWindow {
  /// <summary>
  /// The signal data.
  /// </summary>
  /// <returns></returns>
  private Queue<double> q_signal = new();
  /// <summary>
  /// List of integers representing the last few predictions made.
  /// </summary>
  /// <returns></returns>
  private Queue<int> q_predictions = new();
  private double[] weights = new double[] {
    1.3870019465068615e-05, 4.313564569348816e-05, 9.892837395098251e-05, 
    5.934895117985429e-05, 4.1657575626930166e-05, 6.738527154134303e-05, 
    0.00014213989886547365, 0.00010700766577817803, 5.2750525304857835e-05, 
    3.5778103775882136e-05, 3.8205440056306245e-05, -2.1337893225894182e-05, 
    9.093677955381632e-06, 2.9947379962392004e-05, 2.7623657899970657e-05, 
    3.581396682035981e-06, -2.850404783946663e-05, -8.152726755011381e-05, 
    -8.919903913607539e-05, 4.882913426744099e-06, 4.1394597000482263e-05, 
    1.2024921079809641e-05, 2.6628108220205992e-06, -4.9274269286924874e-05, 
    -4.919111550114623e-05, 0.00012481632180263747, 0.0003276390007298714, 
    0.00017841104241752556, 4.996375488394727e-05, 4.5858194241904496e-05, 
    -7.742496620720455e-05, 8.712332680136825e-05, 0.00017162102886002087, 
    0.00045311513395966733, 0.000206594706715627, 0.00041226512057469767, 
    0.000543144609911776, 5.935889373870565e-05, 2.5431412390439862e-05, 
    0.00048366728040802147, -0.00048423509122271196, 2.1039204505622336e-05, 
    -0.0004296078735828951, -3.361403775658161e-05, -0.00047378175439483235, 
    -0.0010855225502114604
  };
  private double[] test_signal = new double[] {
    17.999267578125, 19.01922607421875, 38.9984130859375, 
    40.01837158203125, 28.01885986328125, 2.9998779296875, 
    -13.01947021484375, -16.97930908203125, -8.9996337890625, 
    1.01995849609375, 4.97979736328125, 1.01995849609375, 
    -4.97979736328125, -32.9986572265625, -32.9986572265625, 
    -31.01873779296875, -29.998779296875, -17.999267578125, 
    -16.97930908203125, -8.9996337890625, 13.01947021484375, 
    2.9998779296875, -10.97955322265625, -46.97808837890625, 
    -58.01763916015625, -34.01861572265625, 13.01947021484375, 
    37.01849365234375, 55.01776123046875, 58.01763916015625, 
    49.01800537109375, 59.99755859375, 79.01678466796875, 
    77.996826171875, 58.01763916015625, 35.99853515625, 
    2.9998779296875, -5.999755859375, -4.01983642578125, 
    -2.9998779296875, 4.01983642578125, -20.9991455078125, 
    -22.01910400390625, -16.01934814453125, -10.01959228515625, 
    4.97979736328125, 10.01959228515625, 0.0, 
    -14.9993896484375, -31.97869873046875, -25.97894287109375, 
    -25.01898193359375, -35.99853515625, -61.01751708984375, 
    -92.9962158203125, -103.01580810546876, -85.97650146484375, 
    -62.9974365234375, -40.01837158203125, -7.01971435546875, 
    8.9996337890625, 16.97930908203125, 23.9990234375, 
    29.998779296875, 44.9981689453125, 34.97857666015625, 
    16.01934814453125, 19.01922607421875, -17.999267578125, 
    -46.01812744140625, -82.01666259765625, -94.01617431640624, 
    -88.01641845703125, -71.9970703125, -49.97796630859375, 
    -31.01873779296875, 7.97967529296875, 46.97808837890625, 
    82.01666259765625, 76.01690673828125, 67.97723388671875, 
    43.97821044921875, 20.9991455078125, 5.999755859375, 
    -1.97991943359375, 8.9996337890625, -4.97979736328125, 
    -13.97943115234375, -35.99853515625, -52.01788330078125, 
    -56.9976806640625, -37.97845458984375, -7.01971435546875, 
    31.97869873046875, 52.01788330078125, 43.97821044921875, 
    14.9993896484375, 8.9996337890625, 14.9993896484375, 
    25.97894287109375, 17.999267578125, 7.97967529296875, 
    17.999267578125, 20.9991455078125, 40.97833251953125, 
    53.997802734375, 64.01739501953125, 77.996826171875, 
    88.97637939453125, 91.97625732421876, 103.01580810546876, 
    79.97674560546875, 61.01751708984375, 17.999267578125, 
    -19.01922607421875, -31.97869873046875, -40.01837158203125, 
    -41.998291015625, -34.01861572265625, -22.01910400390625, 
    16.97930908203125, 26.9989013671875, 49.01800537109375, 
    35.99853515625, 17.999267578125, 1.01995849609375, 
    4.01983642578125, -16.01934814453125, -17.999267578125, 
    -26.9989013671875, -19.97918701171875, -11.99951171875, 
    -4.97979736328125, 4.97979736328125, 8.9996337890625, 
    8.9996337890625, 7.01971435546875, 13.97943115234375, 
    2.9998779296875, 10.01959228515625, 13.01947021484375, 
    7.01971435546875, 4.01983642578125, -20.9991455078125, 
    -46.97808837890625, -74.9969482421875, -88.97637939453125, 
    -100.97589111328124, -92.9962158203125, -83.99658203125, 
    -64.01739501953125, -25.97894287109375, -7.97967529296875, 
    16.97930908203125, 7.01971435546875, -1.97991943359375, 
    -10.01959228515625, -7.97967529296875, -11.99951171875, 
    -43.97821044921875, -67.01727294921875, -103.97576904296876, 
    -127.01483154296876, -131.99462890625, -106.97564697265624, 
    -61.01751708984375, -31.01873779296875, -7.97967529296875, 
    -8.9996337890625, -16.01934814453125, -23.9990234375, 
    -19.01922607421875, -28.97882080078125, -22.01910400390625, 
    -10.01959228515625, 2.9998779296875, -8.9996337890625, 
    -29.998779296875
  };
  private double intercept = -4.204528957411403;
  /// <summary>
  /// Max size of the window.
  /// </summary>
  private int max_signal_size = 178;
  /// <summary>
  /// Max number of predictions to save.
  /// </summary>
  private int max_predict_size = 3;
  private int weight_size = 45;
  /// <summary>
  /// Make a prediction every # of inputs
  /// </summary>
  private int sample_rate = 20;
  /// <summary>
  /// The current sample in relation to the sample_rate
  /// </summary>
  private int current_sample = 0;
  private bool predict_ready = false;
  /// <summary>
  /// There are enough data points saved to make a prediction and there have been
  /// at least sample_rate number of samples since the last prediction.
  /// </summary>
  /// <value></value>
  public bool PredictReady {
    get => predict_ready && q_signal.Count >= max_signal_size;
  }
  private int _count = 0;
  /// <summary>
  /// Total number of data points received since creation.
  /// Not the size of the window.
  /// </summary>
  /// <value></value>
  public int Count {
    get => _count;
  }
  /// <summary>
  /// Add a data point to the signal queue. If the queue is at capacity, equal to max_signal_size,
  /// this removes a data point before adding the new one.
  /// 
  /// If the number of samples since the last prediction is greater or equal to the sample_rate,
  /// sets predict_ready to true.
  /// </summary>
  /// <param name="uv"></param>
  public void add(double uv) {
    if (q_signal.Count >= max_signal_size) {
      q_signal.Dequeue();
    }
    q_signal.Enqueue(uv);
    current_sample++;
    if (current_sample >= sample_rate) {
      predict_ready = true;
    }
    _count++;
  }
  /// <summary>
  /// Predict if we have a seizure.
  /// </summary>
  /// <returns>True if there is a seizure detected, false if not.</returns>
  public bool predict() {
    // Log.debug($"Predicting...sample count={current_sample}");
    //Console.WriteLine("FOURIER");
    if (q_signal.Count < max_signal_size) return false;
    //Log.debug("window is big enough");
    current_sample = 0;
    predict_ready = false;
    double[] signal = q_signal.ToArray();
    // Array.Copy(q_signal.ToArray(), signal, 178);
    // signal[178] = 0.0;
    // signal[179] = 0.0;
    double[] signalComplex = new double[max_signal_size];
    //Log.debug("input:", signal);
    Fourier.Forward(signal, signalComplex, FourierOptions.NoScaling);
    double sum = 0.0;
    //double[] logit = new double[weight_size];
    for (int i = 0; i < weight_size; i++) {
      double psd = Math.Sqrt(signal[i] * signal[i] + signalComplex[i] * signalComplex[i]);
      //logit[i] = psd;
      sum += weights[i] * psd;
    }
    //Log.debug("Power density:", logit);
    if (q_predictions.Count >= max_predict_size) q_predictions.Dequeue();
    if (sum + intercept > 0) {
      q_predictions.Enqueue(1);
      return true;
    }
    q_predictions.Enqueue(-1);
    return false;
  }
  /// <summary>
  /// Calculate the confidence level of the current prediction given prior predictions.
  /// </summary>
  /// <returns></returns>
  public double confidence() {
    double sum = 0;
    double wi = 1.0 / (double) max_predict_size;
    double w = wi;
    foreach (var i in q_predictions) {
      sum += ((double) i) * w;
      w += wi;
    }
    return sum;
  }
}