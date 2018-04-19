using System;
using System.Collections.Generic;

namespace VirtualSpace.Shared
{
    public interface ILogPrinter
    {
        void Log(string message, Logger.Level level);
    }

#if BACKEND
    public class ConsolePrinter : ILogPrinter
    {
        private static String _lastMessage;
        private static int _lastMessageCount = 1;

        public void Log(string message, Logger.Level level)
        {
            switch (level)
            {
                case Logger.Level.Trace:
                case Logger.Level.Debug:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
                case Logger.Level.Info:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case Logger.Level.Warn:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case Logger.Level.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }

            if (message.Equals(_lastMessage))
            {
                _lastMessageCount++;
                Console.Write($"\r{message} ({_lastMessageCount})");
            }
            else
            {
                _lastMessageCount = 1;
                Console.WriteLine();
                Console.Write(message);
                _lastMessage = message;
            }
        }
    }
#endif

    public class Logger
	{
		public enum Level {Trace, Debug, Info, Warn, Error, Off};
		private static Level _level = Level.Info;
	    private static List<ILogPrinter> _printer = new List<ILogPrinter>();

        public static void AddPrinter(ILogPrinter printer)
        {
            lock(_printer)
                _printer.Add(printer);
        }

        public static void RemovePrinter(Type printerType)
        {
            lock (_printer)
                _printer.RemoveAll(printer => printer.GetType() == printerType);
        }

		public static void SetLevel(Level level)
		{
			_level = level;
		}

        private static bool _checkLevel(Level level)
        {
            return level >= _level;
        }
        
		public static void Log(Level level, string message)
		{
            if (_checkLevel(level)) {
#if UNITY
                UnityEngine.Debug.Log(message);
#elif BACKEND
                lock(_printer)
                    foreach (var printer in _printer)
                        printer?.Log(message, level);
#endif
            }
        }

		public static void Trace(string message)
		{
			Log(Level.Trace, message);
		}

		public static void Debug(string message)
		{
			Log(Level.Debug, message);
		}

		public static void Info(string message)
		{
			Log(Level.Info, message);
		}

		public static void Warn(string message)
		{
            Log(Level.Warn, message);
		}

		public static void Error(string message)
		{
            Log(Level.Error, message);
		}

		public static string DelimitedConcat(params object [] message)
		{
			string result = "";
			for(int i = 0; i < message.Length; i++)
				result += message[i].ToString() + (i < message.Length - 1 ? " | " : "");
			return result;
		}
	}
}