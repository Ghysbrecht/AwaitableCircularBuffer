using System;

namespace BufferTesterConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var tester = new Tester();
            AppDomain.CurrentDomain.ProcessExit += tester.ShutDown;
            tester.Start();
        }
    }
}
