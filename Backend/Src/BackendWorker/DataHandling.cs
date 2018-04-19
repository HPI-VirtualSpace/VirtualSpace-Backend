using System;
using System.Collections.Generic;
using System.Linq;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public partial class BackendWorker
    {
        private void _handleAllocationRequest(IMessageBase messageBase)
        {
            int playerId = ((MessageBase)messageBase).UserId;
            if (playerId < 0)
            {
                Logger.Warn("Received invalid player id " + playerId);
                return;
            }

            PlayerAllocationRequest allocation = (PlayerAllocationRequest)messageBase;

            Logger.Debug("Allocation request from player " + allocation.UserId);
            
            {
                //MustArea/NiceArea

                // automatic placement based on CMAES
                // hack for now: auto-accept
                SetAllocationAndRecalculateIntersections(allocation);
            }
        }
        
        PlayfieldArranger arranger = new PlayfieldArranger(PlayerData.Instance);
        private readonly object _intersectionLock = new object();
        private void SetAllocationAndRecalculateIntersections(PlayerAllocationRequest request)
        {
            if (PlayerData.Instance.TryGetEntry(request.UserId, out PlayerDataEntry entry))
            {
                lock (_intersectionLock)
                {
                    Allocation playerAllocation = arranger.PlaceSingle(request);

                    if (playerAllocation == null)
                    {
                        Logger.Warn($"Couldn't find allocation for player {request.UserId} ({entry.UserName})");
                        SendReliable(new AllocationDenied()
                        {
                            UserId = request.UserId,
                        });
                        return;
                    }

                    entry.Allocation = playerAllocation;

                    Logger.Info($"{request.UserId} ({entry.UserName}) got {entry.Allocation.Degrees} degrees orientation");
                    
                    SendReliable(new AllocationGranted
                    {
                        UserId = request.UserId,
                        Offset = playerAllocation.Offset,
                        RotationAroundFirstPoint = playerAllocation.Rotation
                    });

                    //SendInitialEmptyArea(request.UserId);

                    StrategyManager.Instance.UpdatePlayerList();
                }
            }
        }

        private void SendInitialEmptyArea(int playerId)
        {
            var strategyId = -1;
            TransitionFrame frame = new TransitionFrame(
                                    new TimedPosition(new Vector(-3, -3), Time.CurrentTurn, long.MaxValue, playerId, IncentiveType.Recommended, playerId, strategyId),
                                    new TimedArea(new Polygon(), Time.CurrentTurn, long.MaxValue, playerId, IncentiveType.Recommended, playerId, strategyId));
            var playerFrames = new List<TransitionFrame> { frame };
            Transition transition = new Transition(
                    0, playerId, -1,
                    playerFrames, Time.CurrentTurn, long.MaxValue, playerId, IncentiveType.Recommended, strategyId, 0);
            EventMap.Instance.AddOrModifyEvent(transition);
            SendReliable(new Incentives(new List<TimedEvent> { transition }));
        }

        private void _handlePlayerPosition(IMessageBase messageBase)
        {
            int playerId = ((MessageBase)messageBase).UserId;
            if (playerId < 0)
            {
                Logger.Warn("Received invalid player id " + playerId);
                return;
            }
            PlayerPosition position = (PlayerPosition)messageBase;
            PlayerDataEntry entry = PlayerData.Instance.GetEntry(position.UserId);
            if (entry != null)
            {
                entry.UpdateStatus(Time.NowSeconds, position.Position, position.Orientation);
            }
        }
        
        private void _handlePlayerPreferences(IMessageBase messageBase)
        {
            PreferencesMessage preferences = (PreferencesMessage)messageBase;
            Logger.Debug("Received new player preferences from P" + preferences.UserId);
            if (preferences == null)
            {
                Logger.Warn("Received invalid message type " + messageBase.GetType() +
                    ". Expected " + typeof(PreferencesMessage));
                return;
            }
            if (preferences.UserId < 0)
            {
                Logger.Warn("Received invalid player id " + preferences.UserId);
                return;
            }
            if (!PlayerData.Instance.TryGetEntry(preferences.UserId, out PlayerDataEntry entry))
            {
                Logger.Warn("Received unregistered player id " + preferences.UserId);
                return;
            }

            entry.UpdatePreferences(preferences.preferences);
            SendToFrontend(new FrontendPayload(preferences));
            MetricLogger.Log(preferences.UserId + " reregistered as " + preferences.preferences.SceneName);
        }
    }
}
