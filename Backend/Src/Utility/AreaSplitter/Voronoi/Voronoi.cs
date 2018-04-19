using System;
using System.Collections.Generic;
using System.Linq;
using MIConvexHull;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    internal class VoronoiSplitter : IAreaSplitter
    {
        Polygon _wrapperPolygon;
        List<MIVector> _bounds;

        public VoronoiSplitter(Polygon wrapperPolygon)
        {
            _wrapperPolygon = wrapperPolygon;
            Tuple<Vector, Vector> enclosingVectors = wrapperPolygon.EnclosingVectors;
            Vector min = enclosingVectors.Item1;
            Vector max = enclosingVectors.Item2;
            _bounds = new List<MIVector> { new MIVector(min.X, min.Z), new MIVector(max.X, min.Z), new MIVector(max.X, max.Z), new MIVector(min.X, max.Z) };
        }

        public Dictionary<int, Polygon> GetAreasForPositions(Dictionary<int, Vector> idToPosition)
        {
            Dictionary<Vector, MIVector> vectorToMiVectorMapping = new Dictionary<Vector, MIVector>();
         
            idToPosition.ForEachValue(vector => vectorToMiVectorMapping[vector] = new MIVector(vector.X, vector.Z));

            // add fake generators
            vectorToMiVectorMapping.Add(new Vector(0, 10), new MIVector(0, 10));
            vectorToMiVectorMapping.Add(new Vector(10, 0), new MIVector(10, 0));
            vectorToMiVectorMapping.Add(new Vector(0, -10), new MIVector(0, -10));
            vectorToMiVectorMapping.Add(new Vector(-10, 0), new MIVector(-10, 0));

            Dictionary<MIVector, VoronoiCell> positionsToVoronoi = GetAreasForPositions(vectorToMiVectorMapping.Values.ToList());

            Dictionary<int, Polygon> idsToVoronoi = new Dictionary<int, Polygon>();
            foreach (KeyValuePair<int, Vector> pair in idToPosition)
            {
                if (!positionsToVoronoi.ContainsKey(vectorToMiVectorMapping[pair.Value])) continue;
                
                PolygonList intersection = ClipperUtility.Intersection(
                    _wrapperPolygon,
                    new PolygonList(positionsToVoronoi[vectorToMiVectorMapping[pair.Value]]
                        .CellVertices
                        .Select((p) =>
                        {
                            List<Vector> list = p.Select(mi => new Vector(mi.X, mi.Z)).ToList();
                            Polygon poly = new Polygon(list);
                            poly.MakeContour();
                            return poly;
                        })
                        .ToList())
                );
                if (intersection.Count > 0)
                    idsToVoronoi[pair.Key] = intersection.First();
            }

            return idsToVoronoi;
        }

        private Dictionary<MIVector, VoronoiCell> GetAreasForPositions(List<MIVector> vertices)
        {
            Dictionary<MIVector, VoronoiCell> verticesToVoronoi = new Dictionary<MIVector, VoronoiCell>();
            
            foreach (MIVector position in vertices)
            {
                verticesToVoronoi[position] = new VoronoiCell()
                {
                    Generator = position.Clone()
                };
            }
            
            VoronoiMesh<MIVector, DelaunayCell, VoronoiEdge<MIVector, DelaunayCell>> delaunayMesh = VoronoiMesh.Create<MIVector, DelaunayCell>(vertices);
            
            foreach (var cell in delaunayMesh.Vertices)
            {
                GetEdgesForDelaunayCell(cell, verticesToVoronoi);
            }
            
            foreach (VoronoiCell cell in verticesToVoronoi.Values.ToList())
            {
                _createCellPolygon(cell);
            }

            return verticesToVoronoi;
        }

        private List<MIVector> GetVerticesForSideOfRectangle(MIVector from, MIVector to, bool leftSide)
        {
            List<MIVector> vertices = new List<MIVector>
            {
                from,
                to
            };

            foreach (MIVector bound in _bounds)
            {
                if ((leftSide ? 1 : -1) * IsLeft(from, to, bound) > 0)
                {
                    vertices.Add(bound);
                }
            }

            return ConvexHull.Create(vertices).Points.ToList();
        }

        private void GetEdgesForDelaunayCell(DelaunayCell cell, Dictionary<MIVector, VoronoiCell> verticesToVoronoi)
        {
            for (int i = 0; i < 3; i++)
            {
                var verticesOnEdge = cell.Vertices.Where((_, j) => j != i).ToArray();

                VoronoiCell cell1 = verticesToVoronoi[verticesOnEdge[0]];
                VoronoiCell cell2 = verticesToVoronoi[verticesOnEdge[1]];

                MIVector from, to;
                if (cell.Adjacency[i] == null)
                {
                    GetInfiniteEdge(cell, verticesOnEdge, out from, out to);
                }
                else
                {
                    from = cell.Circumcenter;
                    to = cell.Adjacency[i].Circumcenter;
                }

                if (!_vecEq(from, to))
                {
                    if (!cell1.Edges.Any((e) => (_vecEq(e.Item1, from) && _vecEq(e.Item2, to)) || (_vecEq(e.Item1, to) && _vecEq(e.Item2, from))))
                        cell1.Edges.Add(new Tuple<MIVector, MIVector>(from, to));
                    if (!cell2.Edges.Any((e) => (_vecEq(e.Item1, from) && _vecEq(e.Item2, to)) || (_vecEq(e.Item1, to) && _vecEq(e.Item2, from))))
                        cell2.Edges.Add(new Tuple<MIVector, MIVector>(from, to));
                }
            }
        }

        private static void GetInfiniteEdge(DelaunayCell cell, MIVector[] verticesOnEdge, out MIVector from, out MIVector to)
        {
            from = cell.Circumcenter;

            var factor = 100
                * IsLeft(verticesOnEdge[0], verticesOnEdge[1], from)
                * IsLeft(verticesOnEdge[0], verticesOnEdge[1], cell.Center);
            var fromToEdgeMidpoint =
                new MIVector(0.5 * (verticesOnEdge[0].Position[0] + verticesOnEdge[1].Position[0] - from.X),
                           0.5 * (verticesOnEdge[0].Position[1] + verticesOnEdge[1].Position[1]) - from.Z);

            to = new MIVector(from.X + factor * fromToEdgeMidpoint.X, from.Z + factor * fromToEdgeMidpoint.Z);
        }

        static int IsLeft(MIVector a, MIVector b, MIVector c)
        {
            return ((b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X)) > 0 ? 1 : -1;
        }

        private void _createCellPolygon(VoronoiCell cell)
        {
            List<List<MIVector>> mergedEdges = cell.Edges.Select((e) => new List<MIVector>() { e.Item1, e.Item2 }).ToList();
            _mergeEdges(ref mergedEdges);
            
            if (mergedEdges.Count != 1)
                Logger.Warn("Case not covered: voronoi cell has not exactly one edge path, but " + mergedEdges.Count);
            else if (_vecEq(mergedEdges[0].First(), mergedEdges[0].Last()))
                cell.CellVertices.Add(mergedEdges[0]);
            // this log is disabled since the additional points never result in a closed loop
            // else
            //     Logger.Warn("Voronoi cell's edges don't form a closed loop");
        }

        private static bool _mergeTwoEdges(List<MIVector> edge1, List<MIVector> edge2, out List<MIVector> newEdge)
        {
            newEdge = edge1.ToList();
            if (_vecEq(edge2.Last(), edge1.First()))
            {
                newEdge = edge2.Concat(edge1.GetRange(1, edge1.Count - 1)).ToList();
                return true;
            }
            else if (_vecEq(edge1.Last(), edge2.First()))
            {
                newEdge = edge1.Concat(edge2.GetRange(1, edge2.Count - 1)).ToList();
                return true;
            }
            else if (_vecEq(edge1.First(), edge2.First()))
            {
                newEdge = edge2.Reverse<MIVector>().Concat(edge1.GetRange(1, edge1.Count - 1)).ToList();
                return true;
            }
            else if (_vecEq(edge1.Last(), edge2.Last()))
            {
                newEdge = edge1.Concat(edge2.Reverse<MIVector>().ToList().GetRange(1, edge2.Count - 1)).ToList();
                return true;
            }
            return false;
        }

        private static bool _mergeEdges(List<List<MIVector>> inEdges, out List<List<MIVector>> mergedEdges)
        {
            if (!inEdges.TryPop(out List<MIVector> firstEdge))
            {
                // no elements
                mergedEdges = inEdges;
                return true;
            }
            List<List<MIVector>> edges = new List<List<MIVector>>() { firstEdge };
            bool merged = false;
            inEdges.ForEach((inEdge) =>
            {
                bool added = false;
                for (int index = 0; !added && index < edges.Count; index++)
                {
                    added = _mergeTwoEdges(edges[index], inEdge, out List<MIVector> newEdge);
                    edges[index] = newEdge;
                }
                if (added) merged = true;
                else edges.Add(inEdge);
            });
            mergedEdges = edges;
            return merged;
        }

        private static void _mergeEdges(ref List<List<MIVector>> edges)
        {
            if (edges.Count == 0) return;
            bool merged = true;
            while (merged)
            {
                merged = _mergeEdges(edges, out List<List<MIVector>> mergedEdges);
                edges = mergedEdges;
            }
        }

        private static bool _vecEq(MIVector a, MIVector b)
        {
            double epsilon = 0.00001;
            return Math.Abs(a.X - b.X) < epsilon && Math.Abs(a.Z - b.Z) < epsilon;
        }
    }
}
