namespace ProcessWatcher {
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.Diagnostics.CodeAnalysis;
  using System.Linq;
  using System.Management;
  using System.Threading;
  using System.Threading.Tasks;

  using Microsoft.Extensions.Hosting;
  using Microsoft.Extensions.Logging;

  using Newtonsoft.Json;

  public class Worker : BackgroundService {
    private readonly ILogger<Worker> _logger;
    private readonly ProcessCfgCollection _Cfgs;
    private readonly ProcessDaemonPart[] _Parts;
    public Worker(ProcessCfgCollection Cfgs, ILogger<ProcessCfgCollection> LoggerC, ILogger<Worker> logger, ILogger<ProcessDaemonPart> LoggerP) {
      _logger = logger;
      _Cfgs = Cfgs;
      Cfgs.TrimByCompare(LoggerC);
      _Parts = Cfgs.StartDaemon(LoggerP).ToArray();
    }

    public override async Task StartAsync(CancellationToken cancellationToken) {
      _logger.LogInformation("Start Daemon");
      await Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken) {
      _logger.LogInformation("Stop Daemon");
      await Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
      while (!stoppingToken.IsCancellationRequested) {
        _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
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
    public bool Enable { get; set; } = true;
    public int[] IgnoreExitCode { get; set; } = new int[] { 0 };

    public int CompareTo(ProcessCfg other) => ToString().CompareTo(other.ToString());
    public override string ToString() => $"\"{StartUpPath}\" {StartUpCommandArgs}";
    public int Compare([AllowNull] ProcessCfg x, [AllowNull] ProcessCfg y) => x.CompareTo(y);

  }
  public class ProcessDaemonPart : IDisposable {
    private readonly ProcessCfg _Cfg;
    private readonly ILogger<ProcessDaemonPart> _Logger;
    public Process AttachedProcess { get; protected set; }
    public ProcessDaemonPart(ProcessCfg Cfg, ILogger<ProcessDaemonPart> Logger) {
      try {
        _Cfg = Cfg;
        _Logger = Logger;
        if (_Cfg.Enable) {
          string FullArgs = $"\"{_Cfg.StartUpPath}\"  {_Cfg.StartUpCommandArgs}";
          AttachedProcess = Process.GetProcessesByName(_Cfg.Name).FirstOrDefault(E => E.GetCommandLineArgs() == FullArgs);
          if (AttachedProcess == null) {
            _Logger.LogWarning("Process Not Found", JsonConvert.SerializeObject(_Cfg));
            AttachedProcess = Process.Start(new ProcessStartInfo(_Cfg.StartUpPath, _Cfg.StartUpCommandArgs));
            _Logger.LogWarning("Process Started", JsonConvert.SerializeObject(_Cfg));
          }
          else { _Logger.LogWarning("Process Found", JsonConvert.SerializeObject(_Cfg)); }
          AttachedProcess.EnableRaisingEvents = true;
          AttachedProcess.Exited += OnEixted;
        }
        else {
          _Logger.LogWarning("One Watcher Disabled By Cfg", JsonConvert.SerializeObject(_Cfg));
        }
      }
      catch (Exception E) {
        _Logger.LogError("Process Watcher StartUp Error", JsonConvert.SerializeObject(_Cfg), AttachedProcess?.Id);
      }
    }

    protected virtual void OnEixted(object sender, EventArgs args) {
      _Logger.LogInformation($"Process Exited {_Cfg.Name} ExitCode:{AttachedProcess.ExitCode}", JsonConvert.SerializeObject(_Cfg), AttachedProcess.Id);
      if (!_Cfg.IgnoreExitCode.Contains(AttachedProcess.ExitCode)) {
        AttachedProcess = Process.Start(new ProcessStartInfo(_Cfg.StartUpPath, _Cfg.StartUpCommandArgs));
        AttachedProcess.EnableRaisingEvents = true;
        AttachedProcess.Exited += OnEixted;
      }
      else {
        _Logger.LogError($"Process Exited Will Not Reboot {_Cfg.Name} ExitCode:{AttachedProcess.ExitCode}", JsonConvert.SerializeObject(_Cfg), AttachedProcess.Id);
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
