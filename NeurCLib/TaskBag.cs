using System.Collections.Concurrent;
using System.Linq;

namespace NeurCLib;
/// <summary>
/// Convenience object for controlling and tracking threaded tasks.
/// </summary>
internal class TaskBag {
  /// <summary>
  /// Thread-safe dictionary of tasks.
  /// </summary>
  /// <returns></returns>
  private ConcurrentDictionary<String, TaskEngine> bag = new();
  /// <summary>
  /// Number of tasks in the bag.
  /// </summary>
  /// <value></value>
  public int Count {
    get => bag.Count;
  }
  /// <summary>
  /// Checks if the task already exists, kills and removes it if it does, and
  /// adds the given task.
  /// </summary>
  /// <param name="tsk">The task to add. The task must have the name property defined.</param>
  /// <returns>True if the task was added successfully</returns>
  public bool TryAdd(TaskEngine tsk) {
    TaskEngine? te;
    if (TryFindTask(tsk.name, out te)) {
      // te must be not-null here
      #pragma warning disable CS8604
      te?.Kill();
      return bag.TryUpdate(tsk.name, tsk, te);
      #pragma warning restore CS8604
    }
    return bag.TryAdd(tsk.name, tsk);
  }
  public bool TryRemove(TaskEngine tsk) {
    return bag.TryRemove(tsk.name, out _);
  }
  /// <summary>
  /// Tries to find the named task. If the task exists, sets tsk equal to it.
  /// </summary>
  /// <param name="name">Name of the task</param>
  /// <param name="tsk"></param>
  /// <returns>Returns true if the task exists and tsk is not null. False otherwise.</returns>
  public bool TryFindTask(String name, out TaskEngine? tsk) {
    if (bag.ContainsKey(name)) {
      //tsk = bag[name];
      return bag.TryGetValue(name, out tsk);
    }
    tsk = null;
    return false;
  }
  /// <summary>
  /// Checks the state of all tasks and prints an error if applicable.
  /// If a task worker has died without properly exiting, this will remove
  /// it from the bag.
  /// </summary>
  public void Check() {
    foreach(var n in bag) {
      TaskEngine tsk = n.Value;
      if (tsk.worker is not null && tsk.worker.Status == TaskStatus.Faulted) {
        // exception must exist for faulted task status
        #pragma warning disable CS8600, CS8602
        Exception e2 = tsk.worker.Exception;
        Log.critical(String.Format("{0}: {1}", e2.GetType().Name, e2.Message));
        Log.critical(e2.StackTrace ?? "StackTrace not available");
        #pragma warning restore CS8600, CS8602
        // remove since it won't exit normally
        bag.TryRemove(n);
      } else if (tsk.state == TaskEngine.TaskState.Error) {
        Log.critical($"Task '{tsk.name}' in error state.");
      }
    }
  }
  /// <summary>
  /// Iterates through the bag and calls the Run function for all tasks.
  /// </summary>
  /// <returns>The list of worker tasks created by the Run functions</returns>
  public Task[] StartAll() {
    Task[] tasks = new Task[Count];
    int i = 0;
    foreach (var n in bag) {
      tasks[i] = n.Value.Run();
      i++;
    }
    return tasks;
  }
  /// <summary>
  /// Iterates through the existing tasks and sends the kill order.
  /// Tasks will complete before removing themselves.
  /// </summary>
  public void KillAll() {
    foreach(var tsk in bag) {
      tsk.Value.Kill();
    }
  }
}