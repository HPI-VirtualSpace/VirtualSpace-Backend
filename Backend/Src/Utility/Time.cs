using System;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    internal class Time
    {
        private static long _ticksAtStartup = DateTime.Now.Ticks;
        
        /* time relative to program start */
        public static long NowTicks
        {
            get {
                if (_absoluteTicksAtStop > long.MinValue) return _absoluteTicksAtStop;
                return DateTime.Now.Ticks - _ticksAtStartup;
            }
        }
        public static float NowSeconds
        {
            get { return _TicksToSeconds(NowTicks); }
        }
        public static double NowMilliseconds
        {
            get { return _TicksToMilliseconds(NowTicks); }
        }

        /* delta time*/
        public static float SecondsForLastTurn { get { return _TicksToSeconds(_ticksForLastUpdate); } }
        public static double MillisecondsForLastTurn { get { return _TicksToMilliseconds(_ticksForLastUpdate); } }

        /* time at frame points */
        private static long _ticksAtFrameStart;
        private static long _ticksAtFrameEnd;
        private static long _ticksForLastUpdate = 0;
        public static long CurrentTurn {
            get { return ConvertMillisecondsToTurns(NowMilliseconds); }
        }
        public static void UpdateFrameStart()
        {
            _ticksAtFrameStart = NowTicks;
        }
        public static void UpdateFrameEnd()
        {
            _ticksAtFrameEnd = NowTicks;
            _ticksForLastUpdate = _ticksAtFrameEnd - _ticksAtFrameStart;
            //CurrentTurn++;
        }

        private static long _absoluteTicksAtStop = long.MinValue;
        private static long _offset;
        public static void Resume()
        {
            _offset = DateTime.Now.Ticks - _absoluteTicksAtStop;
            _absoluteTicksAtStop = long.MinValue;
        }

        public static void Stop()
        {
            _absoluteTicksAtStop = DateTime.Now.Ticks;
        }

        /* helper */
        private static float _TicksToSeconds(long ticks)
        {
            return (float)ticks / TimeSpan.TicksPerSecond;
        }
        private static double _TicksToMilliseconds(long ticks)
        {
            return (double)ticks / TimeSpan.TicksPerMillisecond;
        }

        public static long ConvertMillisecondsToTurns(double millis)
        {
            if (double.MaxValue == millis) return long.MaxValue;
            return (long) (millis / Config.TurnTimeMs + .5);
        }

        public static long ConvertSecondsToTurns(float seconds)
        {
            if (float.MaxValue == seconds) return long.MaxValue;
            return (long)((double)seconds * 1000 / Config.TurnTimeMs + .5);
        }

        public static float ConvertTurnsToSeconds(long turn)
        {
            return (float)((double)turn * Config.TurnTimeMs / 1000);
        }
    }
}
