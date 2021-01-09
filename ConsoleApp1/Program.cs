namespace ConsoleApp1 {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;

  class Program {
    static void Main(string[] args) {
      Console.WriteLine(Environment.CurrentDirectory);
      Console.ReadKey();
      Environment.ExitCode = 2;
    }
  }
}
