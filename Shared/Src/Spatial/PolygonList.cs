using System;
using System.Collections.Generic;
using ProtoBuf;

namespace VirtualSpace.Shared
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class PolygonList : List<Polygon>, IEnumerable<Polygon>
    {
        /* constructor */

        public PolygonList(List<Polygon> list) : base(list) { }

        public PolygonList(Polygon polygon) : base(new List<Polygon>() { polygon }) { }

        public PolygonList() : base(new List<Polygon>()) { }

        /* comparer */

        // todo this highly depends on what we mean. are the polygons overlapping?
        public bool Equals(PolygonList other)
        {
            if (other == null && !(Count == other.Count)) return false;

            bool equals = true;
            PolygonList otherClone = other.Clone();
            foreach (Polygon polygon in this)
            {
                Polygon equalPolygon;
                bool equalsAny = polygon.EqualsAny(otherClone, out equalPolygon);
                if (equalsAny)
                    otherClone.Remove(equalPolygon);
                else
                {
                    equals = false;
                    break;
                }
            }
            if (otherClone.Count != 0) return false;

            PolygonList thisClone = Clone();
            foreach (Polygon polygon in other)
            {
                Polygon equalPolygon;
                bool equalsAny = polygon.EqualsAny(thisClone, out equalPolygon);
                if (equalsAny)
                    thisClone.Remove(equalPolygon);
                else
                {
                    equals = false;
                    break;
                }
            }
            if (otherClone.Count != 0) return false;

            return equals;
        }

        public override bool Equals(System.Object other)
        {
            return other != null && other is PolygonList && Equals((PolygonList)other);
        }

        public override int GetHashCode()
        {
            int hash = 17;

            hash = hash * 23 + Count.GetHashCode();
            foreach (Polygon polygon in this)
                hash = hash * 23 + polygon.GetHashCode();

            return hash;
        }

        /* converter */

        // todo didn't work with the converter, afterall it's still a list of that type
        public static implicit operator List<List<IntPoint>>(PolygonList polygonList)
        {
            List<List<IntPoint>> list = new List<List<IntPoint>>();
            polygonList.ForEach(polygon => list.Add(polygon));
            // hack things like this make me wonder whether we shouldn't use an explicit cast or a method toIntPoint
            return list;
        }

        public static implicit operator PolygonList(List<List<IntPoint>> list)
        {
            PolygonList polygonList = new PolygonList();
            list.ForEach(polygon => polygonList.Add(polygon));
            return polygonList;
        }

        public static implicit operator PolygonList(List<List<Vector>> list)
        {
            PolygonList polygonList = new PolygonList();
            list.ForEach(polygon => polygonList.Add(new Polygon(polygon)));
            return polygonList;
        }

#if BACKEND
        public static implicit operator List<List<Vector>>(PolygonList polygonList)
        {
            List<List<Vector>> list = new List<List<Vector>>();
            polygonList.ForEach(polygon => list.Add(polygon));
            return list;
        }
#endif

        public override string ToString()
        {
            String name = GetType().Name + "("; // + ")";
            this.ForEach(polygon => name += polygon.ToString() + ",");
            name = name.Substring(0, name.Length - 1);
            name += ")";
            return name;
        }

        public PolygonList Clone()
        {
            List<Polygon> newList = new List<Polygon>(this.Count);
            this.ForEach((Polygon polygon) => newList.Add(polygon));
            return new PolygonList(newList);
        }

        public PolygonList DeepClone()
        {
            List<Polygon> newList = new List<Polygon>(this.Count);
            this.ForEach((Polygon polygon) => newList.Add(polygon.Clone()));
            return new PolygonList(newList);
        }

        public bool Contains(Vector currentPlayerPosition)
        {
            Polygon pointPolygon = Polygon.AsCircle(.01f, currentPlayerPosition, 8);
            return ClipperUtility.ContainsWithinEpsilon(this, pointPolygon);
        }
    }
}