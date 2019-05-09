using System;
using System.Linq;
using System.Threading;
using static System.Console;

namespace TestAnalyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            var tail = string.Join(" ", args.Skip(1));
            while (true) {
                var data = ReadLine().Split().Select(s => double.Parse(s) / 255).ToArray();
                Thread.Sleep(100);
                WriteLine($"{Math.Round(data.Average() * 100)}% {tail}");
            }
        }
    }
}
