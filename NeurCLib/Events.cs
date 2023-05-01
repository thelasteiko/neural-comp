using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Buffers.Binary;

namespace NeurCLib;
/// <summary>
/// Arguments for streamed data.
/// </summary>
public class StreamEventArgs : EventArgs {
  /// <summary>
  /// Represents the timestamp part of the data
  /// </summary>
  public ulong timestamp;
  /// <summary>
  /// Represents the data part
  /// </summary>
  public ushort microvolts;
  public StreamEventArgs(byte[] payload) {
    // first 4 are timestamp
    timestamp = BinaryPrimitives.ReadUInt64LittleEndian(payload);
    // last 2 is neural data
    microvolts = BinaryPrimitives.ReadUInt16LittleEndian(payload.Skip(4).ToArray());
  }
}
/// <summary>
/// Delegate that defines an event listener for the stream data.
/// </summary>
/// <param name="sender"></param>
/// <param name="e"></param>
/// <returns></returns>
public delegate Task OnStreamData(Object sender, StreamEventArgs e);

/// Looked into many ways of doing this. This was the least work intensive.
/// Credit to Oleg Karasik, derived from
/// https://olegkarasik.wordpress.com/2019/04/16/code-tip-how-to-work-with-asynchronous-event-handlers-in-c/
internal class NaiveSynchronizationContext : SynchronizationContext {
  private readonly Action completed;
  private readonly Action<Exception> failed;
  
  public NaiveSynchronizationContext(Action completed, Action<Exception> failed) {
    this.completed = completed;
    this.failed = failed;
  }
  public override SynchronizationContext CreateCopy() {
    return new NaiveSynchronizationContext(this.completed, this.failed);
  }
  public override void Post(SendOrPostCallback d, object? state) {
    if (state is ExceptionDispatchInfo edi) {
      this.failed(edi.SourceException);
    } else {
      base.Post(d, state);
    }
  }
  public override void Send(SendOrPostCallback d, object? state) {
    if (state is ExceptionDispatchInfo edi) {
      this.failed(edi.SourceException);
    } else {
      base.Send(d, state);
    }
  }
  public override void OperationStarted() {}
  public override void OperationCompleted() {
    this.completed();
  }
}
/// <summary>
/// Helper extensions
/// </summary>
internal static class Ext {
  /// <summary>
  /// Quick lookup for enums
  /// </summary>
  public static bool In<T>(this T val, params T[] values) where T : struct {
      return values.Contains(val);
  }
  /// <summary>
  /// Asynchronously invoke calls to possibly several handlers.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="sender"></param>
  /// <param name="args"></param>
  /// <typeparam name="T"></typeparam>
  /// <returns></returns>
  public static Task AsyncInvoke<T>(this EventHandler<T> @this,
      object sender, EventArgs args) {
    if (@this is null) return Task.CompletedTask;
    
    TaskCompletionSource<bool> tcs = new();
    
    Delegate[] delegates = @this.GetInvocationList();
    int count = delegates.Length;
    Exception? exception = null;
    
    foreach (var d in delegates) {
      var async = d.Method.GetCustomAttributes(typeof(AsyncStateMachineAttribute), false).Any();
      
      var completed = new Action(() => {
        if (Interlocked.Decrement(ref count) == 0) {
          if (exception is null) tcs.SetResult(true);
          else tcs.SetException(exception);
        }
      });
      
      var failed = new Action<Exception>(e => {
        Interlocked.CompareExchange(ref exception, e, null);
      });
      
      if (async) {
        SynchronizationContext.SetSynchronizationContext(
          new NaiveSynchronizationContext(completed, failed));
      }

      try {
        d.DynamicInvoke(sender, args);
      } catch (TargetInvocationException e1) when (e1.InnerException != null) {
        failed(e1.InnerException);
      } catch (Exception e2) {
        failed(e2);
      }
      
      if (!async) completed();
    }
    return tcs.Task;
  }
}
