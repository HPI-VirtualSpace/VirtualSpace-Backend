using System;
using System.Collections.Generic;
using System.Threading;
using VirtualSpace.Shared;

namespace VirtualSpace.Bots
{
    internal class BotWorker : ClientWorker
    {
        private Movement _movement;
        private Action _deactivateBot;
        private static int _nextWorkerId = 0;

        private void _reactToIncentives(IMessageBase baseMessage)
        {
            //Logger.Debug("react to incentives");
            Incentives incentives = (Incentives)baseMessage;

            // do nothing
        }

        private void _handleRegistrationSuccess(IMessageBase messageBase)
        {
            RegistrationSuccess success = (RegistrationSuccess) messageBase;
            Logger.Info($"{_clientName} gets player id {success.UserId}");
            if (!success.Reregistration)
            {
                _startSendPosition();
                _generateSpaceRequirements();
                _sendPlayerPreferences();
            }
        }

        private void _startSendPosition()
        {
            Thread _positionThread = new Thread(this._sendPositionLoop);
            _positionThread.Start();
        }

        private void _generateSpaceRequirements()
        {
            Polygon mustHave;
            if (WorkerId % 2 == 0)
            {
                Logger.Debug($"{_clientName} sends tennis requirements");
                mustHave = new Polygon(new List<Vector>
                {
                    new Vector(0.1, 0),
                    new Vector(4, 0),
                    new Vector(4, 1),
                    new Vector(3, 1),
                    new Vector(3, 2),
                    new Vector(1, 2),
                    new Vector(1, 1),
                    new Vector(0.1, 1)
                });
            }
            else
            {
                Logger.Debug($"{_clientName} sends pacman requirements");
                mustHave = new Polygon(new List<Vector>
                {
                    new Vector(0, 0),
                    new Vector(4, 0),
                    new Vector(4, 4),
                    new Vector(0, 4)
                });
            }
            mustHave += new Vector(-2, -2);
            var niceHave = new Polygon(new List<Vector>
            {
                new Vector(0, 0),
                new Vector(4, 0),
                new Vector(4, 4),
                new Vector(0, 4)
            });
            niceHave += new Vector(-2, -2);

            SendReliable(new PlayerAllocationRequest
            {
                MustHave = mustHave,
                NiceHave = niceHave
            });
        }

        private void _sendPlayerPreferences()
        {
            //SendReliable(
            //    new PreferencesMessage(
            //        new PlayerPreferences()
            //        {
            //            fallbackThresholdTimeBeforeCollision = 2f,
            //            fallbackThresholdProbabilityOfCollision = .6f
            //        }
            //    )
            //);
        }

        Polygon _playerPolygon = Polygon.AsCircle(.8f, Vector.Zero, 8);
        private void _sendPositionLoop()
        {
            while (true)
            {
                SendUnreliable(new PlayerPosition()
                {
                    Position = _movement.GetPlayerPosition()
                });

                _playerPolygon.Center = _movement.GetPlayerPosition();

                _movement.AdvanceTurn();

                Thread.Sleep(50);
            }
        }

        private void _handleAllocationGranted(IMessageBase baseMessage)
        {
            
        }

        public int WorkerId;
        public BotWorker(Movement movement, Action deactivateBot) : base(clientName: $"bot{_nextWorkerId}")
        {
            WorkerId = _nextWorkerId++;

            this._movement = movement;
            _movement.Initialize(this);
            this._deactivateBot = deactivateBot;
            
            AddHandler(typeof(RegistrationSuccess), _handleRegistrationSuccess);
            AddHandler(typeof(Incentives), _reactToIncentives);
            AddHandler(typeof(AllocationGranted), _handleAllocationGranted);

            
        }
    }
}