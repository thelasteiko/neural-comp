using System.Runtime.InteropServices;
using NeurCLib;

namespace NeurCApp;
/// <summary>
/// I was going to make this more complicated.
/// </summary>
public class MenuOption {
  public string label;
  public MenuOption(string lbl) {
    label = lbl;
  }
}
/// <summary>
/// Helper for showing menu options and getting input from the user.
/// </summary>
public class MenuFactory {
  public string Title;
  private List<MenuOption> options = new();
  public int PrintTimeout = 30000;
  private System.Threading.Mutex kill_lock = new();
  private bool __killbit = false;
  public bool killbit {
    get => __killbit;
    set {
      lock(kill_lock) {__killbit = value;}
    }
  }
  public static MenuFactory BuildSimple(string t, string opt) {
    MenuFactory mf = new(t);
    mf.Add(opt);
    return mf;
  }
  public MenuFactory(string t) {
    Title = t;
  }
  public void Add(MenuOption menuOption) {
    options.Add(menuOption);
  }
  public void Add(string lbl) {
    options.Add(new MenuOption(lbl));
  }
  public void Print() {
    Console.WriteLine(Title);
    Console.WriteLine("Enter a menu option and press ENTER:");
    int i = 0;
    foreach (var opt in options) {
      Console.WriteLine($"\t{i+1} {opt.label}");
      i++;
    }
    Console.WriteLine("\tq. Quit");
  }
  public int ReadChoice() {
    String? s;
    int choice = 0;
    s = Console.ReadLine();
    KillWait();
    if (s != null) {
      int.TryParse(s, out choice);
    }
    return choice;
  }
  public async Task WaitPrint() {
    killbit = false;
    await Task.Factory.StartNew(() => {
      // reprint at intervals
      do {
        Print();
        Thread.Sleep(PrintTimeout);
      } while (!killbit);
    });
  }
  public void KillWait() {
    killbit = true;
  }
}
