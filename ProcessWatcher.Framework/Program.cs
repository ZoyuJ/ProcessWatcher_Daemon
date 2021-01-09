namespace ProcessWatcher.Framework {
  using System;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using System.ServiceProcess;
  using System.Text;

  using Microsoft.Extensions.DependencyInjection;

  using Newtonsoft.Json;
  using Newtonsoft.Json.Linq;
  static class Program {
    /// <summary>
    /// 应用程序的主入口点。
    /// </summary>
    static void Main() {
      ServiceBase[] ServicesToRun;
      var ServiceCollection = new ServiceCollection();
      ConfigureServices(ServiceCollection);
      var ServiceProvider = ServiceCollection.BuildServiceProvider();
      ServicesToRun = new ServiceBase[]
      {
        ServiceProvider.GetRequiredService<Service1>(),
      };
      ServiceBase.Run(ServicesToRun);
    }
    static void ConfigureServices(IServiceCollection Services) {
      var ExePath = Process.GetCurrentProcess().MainModule.FileName;
      var ExeDirectory = Path.GetDirectoryName(ExePath);
      Directory.SetCurrentDirectory(ExeDirectory);
      var SettingFile = Environment.GetCommandLineArgs().FirstOrDefault(E => E.StartsWith("--cfg")) ?? "appsettings.json";
      if (!Path.IsPathRooted(SettingFile)) {
        SettingFile = Path.Combine(Environment.CurrentDirectory, SettingFile);
      }
      var Configs = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(SettingFile, Encoding.UTF8));
      Services.AddOptions();
      Services.AddLogging();
      Services.AddSingleton(Configs[nameof(ProcessCfgCollection)].ToObject<ProcessCfgCollection>());
      Services.AddSingleton<Service1>();

    }
  }
}
