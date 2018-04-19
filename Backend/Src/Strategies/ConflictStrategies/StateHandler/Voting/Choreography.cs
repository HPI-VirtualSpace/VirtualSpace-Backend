using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    class Choreography : VotingMechanism
    {
        private List<StateTransition> _choreo = new List<StateTransition>()
        {
            StateTransition.RotateRight,
            StateTransition.RotateRight,
            StateTransition.RotateRight,
            StateTransition.RotateLeft,
        };
        private int _currentState = 0;
        private bool _changeTime = false;

        public Choreography()
        {

        }

        public override AssignedVote GetBestVote()
        {
            // return the next vote in the choreography
            var desiredTransition = _choreo[_currentState];

            foreach (var systemVote in AssignedVotes)
            {
                if (systemVote.Transition.Equals(desiredTransition))
                {
                    if (_changeTime)
                    {
                        foreach (var userVote in systemVote.Votes)
                        {
                            userVote.ExecutionLengthMs = new List<double> { 1000 };
                            userVote.PlanningTimestampMs = new List<double> { 2000 };
                        }

                        systemVote.PlanningTimestampMs = 2000;
                        systemVote.ExecutionLengthMs = 1000;
                    }

                    return systemVote;
                }
            }

            return null;
        }

        public override void ConsiderSelection(AssignedVote selectedVote)
        {
            _currentState = (_currentState + 1) % _choreo.Count;
        }

        public override double GetUserValue(TransitionVote userVote, AssignedVote assignedVote)
        {
            return userVote.Value;
        }
    }
}
