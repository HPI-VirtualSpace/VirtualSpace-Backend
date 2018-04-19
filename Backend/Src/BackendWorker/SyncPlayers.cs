using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public partial class BackendWorker
    {
        private async void _sendTimeLoop()
        {
            while (true)
            {
                double millis = Time.NowMilliseconds;
                //Logger.Debug($"current time: {millis:0.00}");

                foreach (int userId in PlayerData.Instance.GetKeys())
                {
                    SendUnreliable(
                        new TimeMessage(
                            userId, 
                            millis, 
                            _lastRoundTripTimes.ContainsKey(userId) ? _lastRoundTripTimes[userId].Average : 0)
                        );
                }

                SendToFrontend(new TimeMessage(-1, millis, 0));

                await Task.Delay(1000);
            }
        }

        private Dictionary<int, History> _lastRoundTripTimes = new Dictionary<int, History>();
        void OnTimeMessageBack(IMessageBase baseMessage)
        {
            TimeMessage message = (TimeMessage) baseMessage;

            double nowMillis = Time.NowMilliseconds;
            double deltaMillis = nowMillis - message.Millis;

            if (!_lastRoundTripTimes.ContainsKey(message.UserId))
                _lastRoundTripTimes[message.UserId] = new History(3);

            History playerHistory = _lastRoundTripTimes[message.UserId];
            playerHistory.AddFront(deltaMillis);

            if (deltaMillis > 50)
                Logger.Trace($"Roundtrip to {message.UserId} took {deltaMillis}ms (single: {deltaMillis / 2}, avg single: {playerHistory.Average / 2})");
        }
    }
}
