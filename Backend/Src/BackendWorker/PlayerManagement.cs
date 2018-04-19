using System.Linq;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public partial class BackendWorker : NetworkingBaseServer
    {
        private bool _isSessionConnected(long sessionId)
        {
            return _sessionIds.Any(existingSessionId => existingSessionId.Value == sessionId);
        }

        private void _handleRegistrations(IMessageBase messageBase)
        {
            Registration registration = (Registration)messageBase;

            int sessionId = registration.sessionId;
            string userName = registration.UserName;

            foreach (var playerId in PlayerData.Instance.GetKeys())
            {
                if (PlayerData.Instance.TryGetEntry(playerId, out PlayerDataEntry entry))
                {
                    if (entry.UserName == userName)
                    {
                        if (entry.Allocation != null)
                        {
                            _sessionIds[playerId] = sessionId;

                            SendReliable(new RegistrationSuccess
                            {
                                UserId = playerId,
                                Reregistration = true
                            });

                            SendReliable(new AllocationGranted
                            {
                                UserId = playerId,
                                Offset = entry.Allocation.Offset,
                                RotationAroundFirstPoint = entry.Allocation.Rotation
                            });

                            return;
                        }
                        else
                        {
                            PlayerData.Instance.RemovePlayer(playerId);
                            break;
                        }
                    }
                }
            }

            int playerID = PlayerIDManager.GetID();
            if (playerID <= -1)
                Logger.Info("Can't add player. List of players is full.");
            else
                _addPlayer(playerID, sessionId, userName);
        }

        private void _addPlayer(int playerID, int sessionId, string userName)
        {
            bool success = PlayerData.Instance.AddPlayer(playerID, userName);

            if (success)
            {
                lock (_sessionIds)
                    _sessionIds[playerID] = sessionId;
                SendReliable(new RegistrationSuccess
                {
                    UserId = playerID,
                    Reregistration = false
                });
                Logger.Info("New player registered with session id " + _sessionIds[playerID] + ". Giving Player id " + playerID);
            }
            else
            {
                PlayerIDManager.FreeID(playerID);
                Logger.Warn("Could not add PlayerData.Instance for player " + playerID);
            }
        }

        internal void RemovePlayer(IMessageBase messageBase)
        {
            Deregistration deregistration = (Deregistration)messageBase;
            int playerID = deregistration.UserId;
            Logger.Debug($"{playerID} wants to deregister");

            bool success = PlayerData.Instance.RemovePlayer(playerID);

            if (success)
            {
                Logger.Info("Removing player " + playerID + " with sessionid " + _sessionIds[playerID]);
                SendReliable(new DeregistrationSuccess
                {
                    UserId = playerID
                });
                lock (_sessionIds)
                    _sessionIds[playerID] = -1;
                PlayerIDManager.FreeID(playerID);
            }
            else
                Logger.Warn("Could not remove player " + playerID);
            
            StrategyManager.Instance.UpdatePlayerList();
        }
    }
}
