namespace ProcessWatcher {
  using System;
  using System.Threading;
  using System.Threading.Tasks;

  using Microsoft.Extensions.Hosting;
  using Microsoft.Extensions.Logging;


  public class Worker : BackgroundService {
    private readonly ILogger<Worker> _logger;
    private readonly DaemonCore _Core;
    public Worker(DaemonCore Core) {
      _Core = Core;
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
      _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
      await _Core.ExecuteAsync(stoppingToken);
    }
  }



}
