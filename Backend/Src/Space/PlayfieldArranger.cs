using System;
using System.Collections.Generic;
using System.Linq;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public class PlayfieldArranger
    {
        private readonly IPlayerData _playerData;

        private readonly List<Vector> _possibleOffsets = new List<Vector>
        {
            new Vector(0, 0), new Vector(0, 0), new Vector(0, 0), new Vector(0, 0)
        };
        private readonly List<double> _possibleRotations = new List<double>
        {
            Math.PI / 2, 0, 3D / 2D * Math.PI, Math.PI
        };
        private readonly int _numPlacements = 4;

        public PlayfieldArranger(IPlayerData playerData)
        {
            _playerData = playerData;
        }

        public Allocation PlaceSingle(PlayerAllocationRequest request)
        {
            double minMustHaveOverlap = double.MaxValue;
            double minNiceHaveOverlap = double.MaxValue;
            int minIndex = -1;

            for (int i = 0; i < _numPlacements; i++)
            {
                var offset = _possibleOffsets[i];
                var rotation = _possibleRotations[i];

                var mustHaveArea = request.MustHave.DeepClone();
                mustHaveArea.Rotate(rotation);
                mustHaveArea += offset;
                var niceHaveArea = request.NiceHave.DeepClone();
                niceHaveArea.Rotate(rotation);
                niceHaveArea += offset;

                var mustHaveOverlap = .0;
                var niceHaveOverlap = .0;
                foreach (var allocation in _playerData.GetAllocations())
                {
                    if (allocation == null) continue;

                    var playerMustHaveOverlap = ClipperUtility.GetArea(ClipperUtility.Intersection(allocation.GlobalMustHave, mustHaveArea));
                    var playerNiceHaveOverlap = ClipperUtility.GetArea(ClipperUtility.Intersection(allocation.GlobalNiceHave, niceHaveArea));

                    //Logger.Debug($"Overlap {request.PlayerId} with {playerId}: {playerMustHaveOverlap}");

                    mustHaveOverlap += playerMustHaveOverlap;
                    niceHaveOverlap += playerNiceHaveOverlap;
                }

                if (mustHaveOverlap < minMustHaveOverlap || Math.Abs(mustHaveOverlap - minMustHaveOverlap) < .001 && niceHaveOverlap < minNiceHaveOverlap)
                {
                    minMustHaveOverlap = mustHaveOverlap;
                    minNiceHaveOverlap = niceHaveOverlap;
                    minIndex = i;
                }
            }

            if (minIndex < 0) return null;

            var newAllocation = new Allocation(request.MustHave, request.NiceHave, _possibleOffsets[minIndex], _possibleRotations[minIndex]);

            return newAllocation;
        }
    }
}
