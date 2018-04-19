using System;
using System.Collections.Generic;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public class MovementStatus : ICloneable
    {
        public Vector Position;
        public float Timestamp;

        public MovementStatus() : this(0, Vector.Zero) { }

        public MovementStatus(float time, Vector position)
        {
            Timestamp = time;
            Position = new Vector(position);
        }

        public object Clone()
        {
            return new MovementStatus(Timestamp, Position);
        }
    }
}
