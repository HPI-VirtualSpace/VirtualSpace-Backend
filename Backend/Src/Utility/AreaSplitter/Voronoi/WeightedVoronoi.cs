using System;
using System.Collections.Generic;
using System.Linq;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    class VoronoiWeighting
    {
        private const double _g = 0.35;

        public static Dictionary<int, Vector> WeightPositions(List<Vector> positions, List<double> weights)
        {
            var dictPositions = new Dictionary<int, Vector>();
            var dictWeights = new Dictionary<int, double>();

            for (int i = 0; i < positions.Count; i++)
            {
                dictPositions[i] = positions[i];
                dictWeights[i] = weights[i];
            }

            var dictWeightedPosition = WeightPositions(dictPositions, dictWeights);

            return dictWeightedPosition;
        }


        public static Dictionary<int, Vector> WeightPositions(Dictionary<int, Vector> positions, Dictionary<int, double> weights)
        {
            Dictionary<Tuple<int, int>, double> forces = new Dictionary<Tuple<int, int>, double>();
            Dictionary<Tuple<int, int>, Vector> directions = new Dictionary<Tuple<int, int>, Vector>();

            foreach (int key1 in positions.Keys)
            {
                foreach (int key2 in positions.Keys)
                {
                    if (key1 < key2)
                    {
                        Tuple<int, int> tuple = new Tuple<int, int>(key1, key2);
                        Vector dir = positions[key2] - positions[key1];
                        directions[tuple] = dir.Normalized;
                        forces[tuple] = _g * weights[key1] * weights[key2];// / dir.Length; 
                    }
                }
            }

            Dictionary<int, Vector> newPositions = new Dictionary<int, Vector>();

            foreach (Tuple<int, int> tuple in forces.Keys)
            {
                Vector baseForce = directions[tuple] * forces[tuple];

                int key1 = tuple.Item1;
                if (!newPositions.TryGetValue(key1, out Vector pos1))
                    pos1 = positions[key1];
                pos1 -= baseForce / weights[key1];
                newPositions[key1] = pos1;

                int key2 = tuple.Item2;
                if (!newPositions.TryGetValue(key2, out Vector pos2))
                    pos2 = positions[key2];
                pos2 += baseForce / weights[key2];
                newPositions[key2] = pos2;
            }

            return newPositions;
        }
    }
}