using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public static class BackendConfig
    {
        /**** dynamic parememter ****/
        /*** player settings ***/
        /* movement history and extrapolation */
        public static int MovementHistorySize = 1000;
        public static float MovementHistoryTimeFrame = 5; // float.MaxValue
        public static int MovementExtrapolationSize = 10;
        public static float MovementExtrapolationTimeFrame = 1; // float.MaxValue
    }
}
