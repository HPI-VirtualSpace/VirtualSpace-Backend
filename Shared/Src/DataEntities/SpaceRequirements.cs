using System.Collections;
using System.Collections.Generic;
using ProtoBuf;

namespace VirtualSpace.Shared
{
    /// <summary>
    /// Rating for polygon at a single moment in time.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class PolygonRating
    {
        public Polygon polygon;
        public float rating;
        private PolygonRating() { }
        public PolygonRating(Polygon polygon, float rating)
        {
            this.polygon = polygon;
            this.rating = rating;
        }
    }

    /// <summary>
    /// Used by Apps to communicate their space requirements.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class SpaceRatings : IEnumerable<SpaceRating>
    {
        List<SpaceRating> ratings = new List<SpaceRating>();

        public SpaceRatings() { }
        public SpaceRatings(IEnumerable<SpaceRating> ratings) { }

        public void Add(SpaceRating rating)
        {
            ratings.Add(rating);
        }

        public void Melt(SpaceRatings other)
        {
            ratings.AddRange(other.ToList());
        }

        public List<SpaceRating> ToList()
        {
            return ratings;
        }
        
        public IEnumerable<PolygonRating> GetRatingsAtTime(long time)
        {
            foreach (SpaceRating rating in ratings)
            {
                Polygon area = rating.AreaAtTime(time);
                if (area != null)
                {
                    yield return
                        new PolygonRating(area,
                                rating.RatingAtTime(time)
                        );
                }
            }
        }

        public IEnumerator<SpaceRating> GetEnumerator()
        {
            return ratings.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ratings.GetEnumerator();
        }
    }

    /// <summary>
    /// Space in time rated by its degree of importance.
    /// If they overlap time and space, the left-most polygon in the overlap is prefered.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    [ProtoInclude(501, typeof(ConstantSpaceRating))]
    public abstract class SpaceRating
    {
        public long from;
        public long to;

        protected SpaceRating() { }
        public SpaceRating(long from, long to)
        {
            this.from = from;
            this.to = to;
        }
        
        public abstract Polygon AreaAtTime(long time);
        public abstract float RatingAtTime(long time);
    }

    /// <summary>
    /// Constant area and rating from one time to another time.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class ConstantSpaceRating : SpaceRating
    {
        Polygon _polygon;
        float _rating;

        private ConstantSpaceRating() { }
        public ConstantSpaceRating(long from, long to, Polygon polygon, float rating)
            : base(from, to)
        {
            _polygon = polygon;
            _rating = rating;
        }

        public override Polygon AreaAtTime(long time)
        {
            if (from <= time && time <= to) return _polygon;
            else return null;
        }

        public override float RatingAtTime(long time)
        {
            if (from <= time && time <= to) return _rating;
            else return 0;
        }
    }
}
