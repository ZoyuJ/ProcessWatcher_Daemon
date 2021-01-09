namespace ProcessWatcher.Framework {
  using System.ComponentModel;
  using System.Data;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using System.Management;
  using System.ServiceProcess;
  using System.Text;
  using System.Threading;
  using System.Threading.Tasks;

  using Microsoft.Extensions.Logging;
  using Microsoft.Extensions.Options;

  using Newtonsoft.Json;

  public partial class Service1 : ServiceBase {
    public Service1(DaemonCore Core, ILogger<Service1> logger) {
      _logger = logger;
      _CTokS = new CancellationTokenSource();
      _Core = Core;
      InitializeComponent();
    }
    private DaemonCore _Core;
    private readonly CancellationTokenSource _CTokS;
    private readonly ILogger<Service1> _logger;
    protected override void OnStart(string[] args) {
      _Core.ExecuteAsync(_CTokS.Token)
        .ContinueWith(Tk => {
          if (Tk.IsFaulted) {
            _logger.LogError(Tk.Exception, "Daemon Task Finished Cause Exception");
          }
          else
            _logger.LogWarning("Daemon Task Finished");

        });
    }
    protected override void OnStop() {
      _CTokS.Cancel();
    }
  }
}
