using System;
using System.Diagnostics;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public partial class BackendWorker : NetworkingBaseServer
    {
        private void _setEventHandler()
        {
            _networkEventHandler.Attach(typeof(Registration), _handleRegistrations);
            _networkEventHandler.Attach(typeof(Deregistration), RemovePlayer);
            _networkEventHandler.Attach(typeof(PlayerPosition), _handlePlayerPosition);
            _networkEventHandler.Attach(typeof(PlayerAllocationRequest), _handleAllocationRequest);
            _networkEventHandler.Attach(typeof(PreferencesMessage), _handlePlayerPreferences);
            _networkEventHandler.Attach(typeof(TimeMessage), OnTimeMessageBack);
            
            _networkEventHandler.Attach(typeof(FrontendRegistration), OnFrontendConnect);
            _networkEventHandler.Attach(typeof(FrontendDisconnect), OnFrontendDisconnect);

            _networkEventHandler.SetDefaultHandler(delegate (IMessageBase baseMessage)
            {
                Logger.Warn("Missing handler for message type " + baseMessage.GetType());
            });
        }

        public void AddHandler(Type type, Action<IMessageBase> action)
        {
            _networkEventHandler.Attach(type, action);
        }

        public void RemoveHandler(Type type, Action<IMessageBase> action)
        {
            _networkEventHandler.Detach(type, action);
        }
    }
}
