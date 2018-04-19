using System;
using System.Linq;
using System.ServiceModel;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    internal class Program 
    {
        public static void Main(string[] args)
        {
            Console.BackgroundColor = ConsoleColor.Gray;
            Console.Clear();

            Program program = new Program();
            program._run(args);
        }

        private void _run(string[] args)
        {
            Logger.SetLevel(Logger.Level.Debug);

            BackendWorker.CreateInstance();

            Logger.Info("Starting backend worker");
            BackendWorker.Start();
            
            // run until threads die
            BackendWorker.Join();
            Logger.Info("Worker thread stopped");
        }
    }
}