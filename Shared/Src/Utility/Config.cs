using System.Collections.Generic;

namespace VirtualSpace.Shared
{
    public class Config
	{
	    public const double MaxMoveDist = 1.0f;
	    public const int ServerPort = 8683;
	    public const string ServerIP = "127.0.0.1";
		public const int TurnTimeMs = 80;
		public const int MaxPlayers = 4;
        public const int PlayerTimeoutSeconds = 10;

        public static char delimiter = ','; // rec why have multiple, just use one
        private static char[] _delimiterArray = { '|', '#', '*', '~' };
        public static readonly Dictionary<string, char> Delimiters = new Dictionary<string, char>
        {
            { "Message",                _delimiterArray[0] }
			// TODO: add entries for other serializable data types
		};
        public static string ApplicationNetworkIdentifier = "virtualspace";
        public static int PollIntervalInMilliseconds = 10;
        //public static readonly Polygon PlayArea = Polygon.AsRectangle(new Vector(4, 3.5f), new Vector(-2, -1.75));
        public static readonly Polygon PlayArea = Polygon.AsRectangle(new Vector(3.5f, 4f), new Vector(-1.75, -2));

        public class Space
        {
            public const double PositionX = 0.0f;
            public const double PositionZ = 0.0f;
            public const double SizeX = 4.0f;
            public const double SizeZ = 4.0f;
            public const double MinX = PositionX - SizeX / 2.0f;
            public const double MinZ = PositionZ - SizeZ / 2.0f;
            public const double MaxX = PositionX + SizeX / 2.0f;
            public const double MaxZ = PositionZ + SizeZ / 2.0f;
        }
	}
}