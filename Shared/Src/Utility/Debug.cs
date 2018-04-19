#if !UNITY
namespace VirtualSpace.Shared
{
    internal class Debug
    {
        internal static void Log(string v)
        {
            Logger.Debug(v);
        }

        internal static void LogError(string v)
        {
            Logger.Error(v);
        }
    }
}
#endif