using System;
using System.Collections.Generic;

namespace VirtualSpace.Shared
{
    using NetworkMessageHandler = Action<IMessageBase>;

    public class NetworkEventHandler
    {
        private Dictionary<Type, NetworkMessageHandler> _eventHandler =
            new Dictionary<Type, NetworkMessageHandler>();
        private NetworkMessageHandler _defaultHandler;

        public NetworkMessageHandler GetHandler(Type type)
        {
            if (_eventHandler.ContainsKey(type))
            {
                return _eventHandler[type];
            }
            else
            {
                return _defaultHandler;
            }
        }

        public void Invoke(Type type, IMessageBase message)
        {
            GetHandler(type).Invoke(message);
        }

        public void SetDefaultHandler(NetworkMessageHandler handler)
        {
            _defaultHandler = handler;
        }

        public NetworkMessageHandler GetDefaultHandler(NetworkMessageHandler handler)
        {
            return _defaultHandler;
        }

        public void Attach(Type type, NetworkMessageHandler handler)
        {
            try
            {
                NetworkMessageHandler handlers;
                _eventHandler.TryGetValue(type, out handlers);

                if (handlers == null)
                {
                    _eventHandler[type] = handler;
                } else
                {
                    _eventHandler[type] = _eventHandler[type] + handler;
                }
            }
            catch
            {
                //Logger.Error("Can't attach to delegate: " + exception.Message);
            }
        }

        public void Detach(Type type, NetworkMessageHandler handler)
        {

            try
            {
                _eventHandler[type] -= handler;
            }
            catch
            {
                //Logger.Error("Can't detach to delegate");
            }
        }
    }
}