namespace ProcessWatcher {
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using System.Threading.Tasks;

  using Microsoft.Extensions.Configuration;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Hosting;
  using Microsoft.Extensions.Logging;

  public class Program {
    public static async Task Main(string[] args) {
      var IsService = !(args.Contains("--console") || Debugger.IsAttached);
      string ExeDirectory = null;
      if (IsService) {
        if (IsService) {
          var ExePath = Process.GetCurrentProcess().MainModule.FileName;
          ExeDirectory = Path.GetDirectoryName(ExePath);
          Directory.SetCurrentDirectory(ExeDirectory);
        }
      }
      if (IsService)
        await CreateServiceHost(args, ExeDirectory).Build().RunAsync();
      else
        await CreateHost(args).RunConsoleAsync();
    }


    static IHostBuilder CreateServiceHost(string[] args, string ContentRoot) {
      return Host.CreateDefaultBuilder(args)
        .UseContentRoot(ContentRoot)
        .ConfigureServices((ctx, services) => {
          services.AddOptions();
          services.AddSingleton(ctx.Configuration.GetSection(nameof(ProcessCfgCollection)).Get<ProcessCfgCollection>());
          services.AddSingleton<DaemonCore>();
          services.AddSingleton<IHostedService, Worker>();
        })
        .ConfigureLogging((ctx, logging) => {
          logging.AddConfiguration(ctx.Configuration.GetSection("Logging"));
          if (args.Contains("--console")) logging.AddConsole();
        });

    }

    static IHostBuilder CreateHost(string[] args) {
      return Host.CreateDefaultBuilder(args)
        .UseContentRoot(Environment.CurrentDirectory)
        .ConfigureServices((ctx, services) => {
          services.AddOptions();
          services.AddSingleton(ctx.Configuration.GetSection(nameof(ProcessCfgCollection)).Get<ProcessCfgCollection>());
          services.AddSingleton<DaemonCore>();
          services.AddSingleton<IHostedService, Worker>();
        })
        .ConfigureLogging((ctx, logging) => {
          logging.AddConfiguration(ctx.Configuration.GetSection("Logging"));

        });

    }
  }
}
