using System.Collections.Concurrent;
using System.Linq;

namespace NeurCLib;
internal class TaskBag {
  private ConcurrentDictionary<String, TaskEngine> bag = new();

  public int Count {
    get => bag.Count;
  }

  public bool TryAdd(TaskEngine tsk) {
    TaskEngine? te;
    if (TryFindTask(tsk.name, out te)) {
      te?.Kill();
      return bag.TryUpdate(tsk.name, tsk, te);
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
        Exception e2 = tsk.worker.Exception;
        Log.critical(String.Format("{0}: {1}", e2.GetType().Name, e2.Message));
        Log.critical(e2.StackTrace);
        // remove since it won't exit normally
        bag.TryRemove(n);
      } else if (tsk.state == TaskEngine.TaskState.Error) {
        Log.critical($"Task '{tsk.name}' in error state.");
      }
    }
  }

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