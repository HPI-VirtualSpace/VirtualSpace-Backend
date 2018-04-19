using System.Collections.Generic;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public abstract class ConflictStrategy
    {
        static int _nextStrategyId;
        public readonly int strategyId = _nextStrategyId++;

        public abstract void UpdateState();
        public abstract void UpdateIncentives();
        public virtual void Deinitialize() { }
        public abstract void UpdateUsersRequest();
        public abstract void UpdateFrontend();
    }
}
