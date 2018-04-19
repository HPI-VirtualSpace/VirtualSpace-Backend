using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public class FrontendPrinter : ILogPrinter
    {
        private readonly BackendWorker _worker;

        public FrontendPrinter(BackendWorker worker)
        {
            _worker = worker;
        }

        public void Log(string message, Logger.Level level)
        {
            FrontendLogMessage logMessage = new FrontendLogMessage()
            {
                Message = message,
                LogLevel = level
            };

            _worker.SendToFrontend(logMessage);
        }
    }

    public partial class BackendWorker
    {
        public int FrontendUserId = 100;
        public bool FrontendConnected => _sessionIds.ContainsKey(FrontendUserId);

        public void OnFrontendConnect(IMessageBase messageBase)
        {
            Logger.Debug("Frontend connected");
            FrontendRegistration message = (FrontendRegistration) messageBase;
            _sessionIds[FrontendUserId] = message.SessionId;

            // send user preferences
            foreach (int userId in PlayerData.Instance.GetKeys())
            {
                if (!PlayerData.Instance.TryGetEntry(userId, out PlayerDataEntry entry)) continue;

                PreferencesMessage playerPosition = new PreferencesMessage()
                {
                    preferences = entry.Preferences,
                    UserId = userId
                };

                SendToFrontend(new FrontendPayload(playerPosition));
            }

            Logger.AddPrinter(new FrontendPrinter(this));

            StrategyManager.Instance.ActiveStrategy.UpdateFrontend();
        }

        public void OnFrontendDisconnect(IMessageBase messageBase)
        {
            Logger.Debug("Frontend disconnected");

            Logger.RemovePrinter(typeof(FrontendPrinter));

            _sessionIds.TryRemove(FrontendUserId, out int _);
        }

        public void SendToFrontend(MessageBase message, bool wrapped=true)
        {
            if (wrapped)
            {
                var wrapper = new FrontendPayload(message);
                message = wrapper;
            }

            message.UserId = FrontendUserId;
            if (!SendReliable(message))
            {
                //Logger.Warn("Did not send message to Frontend");
            }
        }

        private float FrontendUpdatesPerSecond = 20f;
        private async void _sendFrontendLoop()
        {
            while (true)
            {
                var now = Time.NowMilliseconds;

                foreach (int userId in PlayerData.Instance.GetKeys())
                {
                    if (!PlayerData.Instance.TryGetEntry(userId, out PlayerDataEntry entry)) continue;

                    PlayerPosition playerPosition = new PlayerPosition()
                    {
                        MillisSinceStartup = now,
                        Orientation = entry.Orientation,
                        Position = entry.Position,
                        UserId = userId
                    };

                    SendToFrontend(new FrontendPayload(playerPosition));
                }

                await Task.Delay((int)(1000 / FrontendUpdatesPerSecond));
            }
        }
    }
}
