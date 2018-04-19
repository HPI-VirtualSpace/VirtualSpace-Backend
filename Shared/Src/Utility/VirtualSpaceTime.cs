using System;

#if UNITY
using UnityEngine;
#endif

namespace VirtualSpace.Shared
{
    public static class VirtualSpaceTime
    {
        private static double _CurrentTimeInMillis = -1;
#if BACKEND
        private static long TicksAtLastTimeUpdate;
#elif UNITY
        private static double MillisAtLastTimeUpdate;
#endif

        public static bool IsInitialized { get { return _CurrentTimeInMillis >= 0; } } 

        public static long CurrentTurn
        {
            get
            {
                return ConvertMillisecondsToTurns(CurrentTimeInMillis);
            }
        }

        public static double TurnLengthInMillis { get { return Config.TurnTimeMs;  } }

        private static float _unityTime;
        public static void SetUnityTime(float unscaledUnityTime)
        {
            _unityTime = unscaledUnityTime;
        }
        public static double CurrentTimeInMillis
        {
            get
            {
#if BACKEND
                long ticksSinceUpdate = DateTime.Now.Ticks - TicksAtLastTimeUpdate;
                // ReSharper disable once PossibleLossOfFraction
                double millisSinceUpdate = ticksSinceUpdate / TimeSpan.TicksPerMillisecond;
#elif UNITY
                double millisSinceUpdate = _unityTime * 1000 - MillisAtLastTimeUpdate;
#endif
                return _CurrentTimeInMillis + millisSinceUpdate;
            }
        }

        public static float CurrentTimeInSeconds { get { return (float)(CurrentTimeInMillis / 1000f); } }

        private static readonly bool CorrectTripTime = true;
        public const double TripTimeProportion = .5;
        public static void Update(double millis, double tripTime)
        {

            if (CorrectTripTime)
            {
#if BACKEND
                TicksAtLastTimeUpdate = DateTime.Now.Ticks - (long)(TripTimeProportion * tripTime * TimeSpan.TicksPerMillisecond);
#elif UNITY
                MillisAtLastTimeUpdate = _unityTime * 1000 + TripTimeProportion * tripTime;
#endif
            }
            else
            {
#if BACKEND
                TicksAtLastTimeUpdate = DateTime.Now.Ticks;
#elif UNITY
                MillisAtLastTimeUpdate = _unityTime * 1000;
#endif
            }
                _CurrentTimeInMillis = millis;
        }

        public static long ConvertMillisecondsToTurns(double millis)
        {
            if (Math.Abs(double.MaxValue - millis) < 0.001) return long.MaxValue;
            return (long)(millis / Config.TurnTimeMs + .5);
        }

        public static float ConvertTurnsToSeconds(long turn)
        {
            return (float)turn * Config.TurnTimeMs / 1000;
        }

        public static long ConvertSecondsToTurns(float seconds)
        {
            if (Math.Abs(float.MaxValue - seconds) < 0.001) return long.MaxValue;
            return (long)((double)seconds * 1000 / Config.TurnTimeMs + .5);
        }
    }
}
