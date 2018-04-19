using System.Collections.Generic;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    interface IAreaSplitter
    {
        Dictionary<int, Polygon> GetAreasForPositions(Dictionary<int, Vector> idToPosition);
    }
}
