using System;
using System.Collections.Generic;
using System.Linq;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public class MovementTick : ICloneable
    {
        public Vector Position;
        public Vector Orientation;
        public float timestamp;

        public MovementTick() : this(0, Vector.Zero, Vector.Zero) { }

        public MovementTick(float time, Vector position, Vector orientation)
        {
            timestamp = time;
            Position = new Vector(position);
            Orientation = new Vector(orientation);
        }

        public object Clone()
        {
            return new MovementTick(timestamp, Position, Orientation);
        }
    }

    public class Allocation
    {
        public Polygon LocalMustHave;
        public Polygon LocalNiceHave;
        public Vector Offset { get; }
        public double Rotation { get; }
        public double Degrees => Rotation / Math.PI * 180;
        public Polygon GlobalMustHave { get; private set; }
        public Polygon GlobalNiceHave { get; private set; }

        private void UpdateGlobals()
        {
            GlobalMustHave = _Apply(LocalMustHave, Rotation, Offset);
            GlobalNiceHave = _Apply(LocalNiceHave, Rotation, Offset);
        }

        private static Polygon _Apply(Polygon originalPolygon, double rotation, Vector offset)
        {
            Polygon polygon = originalPolygon.DeepClone();
            polygon.Rotate(rotation);
            polygon += offset;
            return polygon;
        }

        public Allocation(Polygon mustHave, Polygon localNiceHave, Vector offset, double rotation)
        {
            LocalMustHave = mustHave;
            LocalNiceHave = localNiceHave;
            Offset = offset;
            Rotation = rotation;
            UpdateGlobals();
        }

        public Allocation Copy()
        {
            return new Allocation(
                LocalMustHave.DeepClone(),
                LocalNiceHave.DeepClone(),
                Offset.Clone(),
                Rotation);
        }
    }

    public class PlayerDataEntry
    {
        private static float PlayerRadius = .3f;
        private static float DetectionRadius = .6f;
        private static Polygon _PlayerPolygon = Polygon.AsCircle(PlayerRadius, new Vector(0, 0), 8);
        private static Polygon _DetectionPolygon = Polygon.AsCircle(DetectionRadius, new Vector(0, 0), 8);
        public Allocation Allocation;
        public Incentives Incentives;
        /* information set by the player (in)directly */
        private IList<MovementTick> _movementHistory;
        public object _ratingsLock = new object();
        public Tuple<float, SpaceRatings> _Ratings = new Tuple<float, SpaceRatings>(0, new SpaceRatings());
        public PlayerPreferences Preferences = PlayerPreferences.Default;
        private float _lastPositionUpdateTime = -1;
        public string UserName;
        float _lastExtrapolationCalculation;
        Vector _movement = new Vector(0, 0);

        public Polygon PositionPolygon
        {
            get
            {
                if (Position == null) return null;
                _PlayerPolygon.Center = Position;
                return new Polygon(_PlayerPolygon);
            }
        }

        public Polygon DetectionPolygon
        {
            get {
                _DetectionPolygon.Center = Position;
                return new Polygon(_DetectionPolygon);
            }
        }
        
        public SpaceRatings Ratings
        {
            get {
                lock(_ratingsLock)
                    return _Ratings.Item2;
            }
        }
        
        public PlayerDataEntry(string userName)
        {
            _movementHistory = new List<MovementTick>(BackendConfig.MovementHistorySize);
            UserName = userName;
        }

        public void UpdateStatus(float time, Vector Position, Vector Orientation)
        {
            _lastPositionUpdateTime = time;

            lock (_movementHistory)
            {
                float Now = Time.NowSeconds;
                int removing = 0;

                for (; _movementHistory.Count > 0 && 
                    BackendConfig.MovementHistoryTimeFrame <= Now - _movementHistory[0].timestamp;)
                {
                    removing += 1;
                    _movementHistory.RemoveAt(0);
                }

                if (_movementHistory.Count >= BackendConfig.MovementHistorySize)
                {
                    removing += 1;
                    _movementHistory.RemoveAt(0);
                }

                _movementHistory.Add(new MovementTick(time, Position, Orientation));
            }
        }

        public void UpdateRatings(float time, SpaceRatings ratings, bool overwrite)
        {
            //Logger.Debug("Received new ratings");
            lock (_ratingsLock)
            {
                if (overwrite)
                {
                    _Ratings = new Tuple<float, SpaceRatings>(time, ratings);
                }
                else
                {
                    SpaceRatings spaceRatings = _Ratings.Item2;
                    spaceRatings.Melt(ratings);
                    _Ratings = new Tuple<float, SpaceRatings>(time, spaceRatings);
                }
            }
        }

        public void UpdatePreferences(PlayerPreferences preferences)
        {
            Preferences = preferences;
        }
        
        public Vector Position
        {
            get {
                lock (_movementHistory)
                {
                    if (_movementHistory.Count == 0)
                        return null;
                    return _movementHistory.Last().Position;
                }
            }
        }

        public Vector Orientation
        {
            get
            {
                lock (_movementHistory)
                {
                    if (_movementHistory.Count == 0)
                        return Vector.Zero;
                    return _movementHistory.Last().Orientation;
                }
            }
        }
        
        public Vector ExtrapolateMovement(float timeInFuture)
        {
            lock (_movementHistory)
            {
                if (_movementHistory.Count <= 1)
                {
                    return Vector.Zero;
                }

                if (_lastExtrapolationCalculation != _movementHistory.Last().timestamp)
                {
                    _lastExtrapolationCalculation = _movementHistory.Last().timestamp;
                    UpdateExtrapolation();
                    //Logger.Debug("Updated extrapolation with " + _movementHistory.Count + " positions.");
                }

                Vector potential = _movementHistory.Last().Position + _movement * timeInFuture;

                if (potential.X.Equals(double.NaN) || potential.Z.Equals(double.NaN))
                    return Vector.Zero;

                return potential;
            }
        }

        private void UpdateExtrapolation()
        {
            // calculate averages
            double tAvg = 0;
            double xAvg = 0;
            double zAvg = 0;
            int sampleCount = 0;

            // set iteration conditions
            int minimumIndexToConsider = Math.Max(_movementHistory.Count - BackendConfig.MovementExtrapolationSize - 1, 0);
            float Now = Time.NowSeconds;

            for (int movementIndex = _movementHistory.Count - 1; minimumIndexToConsider <= movementIndex; movementIndex--)
            {
                MovementTick status = _movementHistory[movementIndex];

                if (Now - status.timestamp >= BackendConfig.MovementExtrapolationTimeFrame)
                {
                    break;
                }

                tAvg += status.timestamp;
                xAvg += status.Position.X;
                zAvg += status.Position.Z;
                sampleCount++;
            }

            tAvg /= sampleCount;
            xAvg /= sampleCount;
            zAvg /= sampleCount;

            // calculate sums
            double txSum = 0;
            double tzSum = 0;
            double t2Sum = 0;
            double x2Sum = 0;
            double z2Sum = 0;
            for (int movementIndex = _movementHistory.Count - 1; minimumIndexToConsider <= movementIndex; movementIndex--)
            {
                MovementTick status = _movementHistory[movementIndex];

                if (Now - status.timestamp >= BackendConfig.MovementExtrapolationTimeFrame)
                {
                    break;
                }

                double tDiff = status.timestamp - tAvg;
                double xDiff = status.Position.X - xAvg;
                double zDiff = status.Position.Z - zAvg;
                txSum += tDiff * xDiff;
                tzSum += tDiff * zDiff;
                t2Sum += tDiff * tDiff;
                x2Sum += xDiff * xDiff;
                z2Sum += zDiff * zDiff;
            }
            double xSlope = txSum / t2Sum;
            // double xConstantTerm = xAvg - xSlope * tAvg; // not needed
            double xDeterminationCoeff = txSum == 0 ? 0 : Math.Pow(txSum / Math.Sqrt(t2Sum * x2Sum), 2);
            double zSlope = tzSum / t2Sum;
            // double zConstantTerm = zAvg - zSlope * tAvg; // not needed
            double zDeterminationCoeff = tzSum == 0 ? 0 : Math.Pow(tzSum / Math.Sqrt(t2Sum * z2Sum), 2);

            _movement = new Vector(xSlope, zSlope);
        }
    }
}
