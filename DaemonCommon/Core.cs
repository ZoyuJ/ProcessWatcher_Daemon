namespace ProcessWatcher {
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using System.Management;
  using System.Threading;
  using System.Threading.Tasks;

  using Microsoft.Extensions.Logging;

  using Newtonsoft.Json;

  public class DaemonCore {
    public DaemonCore(ProcessCfgCollection Cfgs, ILogger<DaemonCore> Logger, ILogger<ProcessDaemonPart> LoggerP, ILogger<ProcessCfgCollection> LoggerC) {
      _Cfgs = Cfgs;
      Cfgs.TrimByCompare(LoggerC);
      _Parts = Cfgs.StartDaemon(LoggerP).ToArray();
    }
    private readonly ILogger<DaemonCore> _Logger;
    public readonly ProcessCfgCollection _Cfgs;
    public readonly ProcessDaemonPart[] _Parts;
    public async Task ExecuteAsync(CancellationToken stoppingToken) {
      _Logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
      while (!stoppingToken.IsCancellationRequested) {
        Array.ForEach(_Parts, P => {
          P.WaittingStartupManually();
        });
        await Task.Delay(1000, stoppingToken);
      }
    }

  }

  public class ProcessCfgCollection {

    public List<ProcessCfg> Cfgs { get; set; } = new List<ProcessCfg>();

    public IEnumerable<ProcessDaemonPart> StartDaemon(ILogger<ProcessDaemonPart> LoggerP) {
      return Cfgs.Select(E => new ProcessDaemonPart(E, LoggerP)).ToArray();
    }
    public void TrimByCompare(ILogger<ProcessCfgCollection> Logger) {
      Cfgs.Sort();
      string TS = Cfgs.Last().ToString();
      for (int i = Cfgs.Count - 2; i >= 0; i--) {
        if (TS != Cfgs[i].ToString()) {
          TS = Cfgs[i].ToString();
          continue;
        }
        else {
          Logger.LogWarning("Trimed Same Cfg Item By \"Path\"+\" \"+Args", JsonConvert.SerializeObject(Cfgs[i]));
          Cfgs.RemoveAt(i);
        }
      }
    }

  }

  public class ProcessCfg : IComparable<ProcessCfg>, IComparer<ProcessCfg> {
    public string Name { get; set; }
    public string StartUpPath { get; set; }
    public string StartUpCommandArgs { get; set; }
    public string _StartUpDirectory;
    public string StartUpDirectory { get => _StartUpDirectory ?? Path.GetDirectoryName(StartUpPath); set => _StartUpDirectory = value; }
    public bool Enable { get; set; } = true;
    public bool StartupAutomaticalyOnDaemonStart { get; set; }
    public int[] IgnoreExitCode { get; set; } = new int[] { 0 };

    public int CompareTo(ProcessCfg other) => ToString().CompareTo(other.ToString());
    public override string ToString() => $"\"{StartUpPath}\" {StartUpCommandArgs}";
    public int Compare(ProcessCfg x, ProcessCfg y) => x.CompareTo(y);

  }

  public enum DaemonState {
    Disabled = 0,
    Running = 1,
    WaittingManual = 2,
    ReStarting = 3,
    Killed = 4,

    Error = -1,
  }


  public class ProcessDaemonPart : IDisposable {
    private readonly ProcessCfg _Cfg;
    public DaemonState DaemonState { get; set; }
    //public bool LegalExitWaitting { get; private set; }
    private readonly ILogger<ProcessDaemonPart> _Logger;
    public Process AttachedProcess { get; protected set; }
    private readonly string FullArgs;
    public ProcessDaemonPart(ProcessCfg Cfg, ILogger<ProcessDaemonPart> Logger) {
      try {
        _Cfg = Cfg;
        _Logger = Logger;
        FullArgs = $"\"{_Cfg.StartUpPath}\"  {_Cfg.StartUpCommandArgs}";
        if (_Cfg.Enable) {
          Startup();
        }
        else {
          DaemonState = DaemonState.Disabled;
          _Logger.LogWarning("One Watcher Disabled By Cfg", JsonConvert.SerializeObject(_Cfg));
        }

      }
      catch (Exception E) {
        DaemonState = DaemonState.Error;
        _Logger.LogError("Process Watcher StartUp Error", JsonConvert.SerializeObject(_Cfg), AttachedProcess?.Id);
      }
    }


    public void Startup() {
      FindProcess();
      if (AttachedProcess == null) {
        if (_Cfg.StartupAutomaticalyOnDaemonStart) StartUpTarget();
        else DaemonState = DaemonState.WaittingManual;
      }
    }
    protected void FindProcess() {
      AttachedProcess = Process.GetProcessesByName(_Cfg.Name).FirstOrDefault(E => E.GetCommandLineArgs() == FullArgs);

    }
    protected void StartUpTarget() {
      DaemonState = DaemonState.ReStarting;
      AttachedProcess = Process.Start(
        new ProcessStartInfo(_Cfg.StartUpPath, _Cfg.StartUpCommandArgs) {
          WorkingDirectory = _Cfg.StartUpDirectory,
        });
      RegisterExitEvent();
    }
    protected void RegisterExitEvent() {
      AttachedProcess.EnableRaisingEvents = true;
      AttachedProcess.Exited += OnEixted;
    }
    protected virtual void OnEixted(object sender, EventArgs args) {
      _Logger.LogInformation($"Process Exited {_Cfg.Name} ExitCode:{AttachedProcess.ExitCode}", JsonConvert.SerializeObject(_Cfg), AttachedProcess.Id);
      if (!_Cfg.IgnoreExitCode.Contains(AttachedProcess.ExitCode)) {
        StartUpTarget();
        DaemonState = DaemonState.Running;
      }
      else {
        DaemonState = DaemonState.WaittingManual;
        AttachedProcess = null;
        _Logger.LogError($"Process Exited Will Not Reboot {_Cfg.Name} ExitCode:{AttachedProcess.ExitCode}", JsonConvert.SerializeObject(_Cfg), AttachedProcess.Id);
      }
    }
    public void WaittingStartupManually() {
      if (DaemonState == DaemonState.WaittingManual && AttachedProcess == null) {
        FindProcess();
        if (AttachedProcess != null) {
          RegisterExitEvent();
          DaemonState = DaemonState.Running;
        }
      }
      else if (DaemonState == DaemonState.Error) {
        Startup();
      }
    }

    public void Dispose() {
      AttachedProcess.Exited -= OnEixted;
    }

    public static implicit operator ProcessCfg(ProcessDaemonPart Part) => Part._Cfg;

  }

  internal static class Kits {
    public static string GetCommandLineArgs(this Process Process) {
      using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + Process.Id))
      using (ManagementObjectCollection objects = searcher.Get()) {
        return objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
      }
    }
  }
}
