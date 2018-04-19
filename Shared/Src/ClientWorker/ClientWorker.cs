using System;
using System.Threading;

#if UNITY
using UnityEngine;
#endif

namespace VirtualSpace.Shared
{
    public partial class ClientWorker : NetworkingBaseClient
    {
        public int PlayerID = -1;
        private bool _registeredAtServer = false;
        protected NetworkEventHandler _networkEventHandler = new NetworkEventHandler();
        protected string _clientName;
        public TimeMessage LastTimeMessage;

        public ClientWorker(
            string serverIp = Config.ServerIP,
            int serverPort = Config.ServerPort,
            string clientName = "default")
            :
            base(serverIp, serverPort)
        {
            _setEventHandler();
            _clientName = clientName;
        }

        public void Start()
        {
            StartListening();
        }

        protected override void _onConnectionEstablished()
        {
            Debug.Log("Connection to server was established");
            _registerAtServer();
        }

        protected void _handleTimeMessage(IMessageBase baseMessage)
        {
            //Debug.Log("Received time message");

            if (!(baseMessage is TimeMessage)) {
                Logger.Warn("Received the incorrect message type " + baseMessage.GetType() + ". " +
                    "Excepted " + typeof(TimeMessage));
                return;
            }

            TimeMessage message = (TimeMessage)baseMessage;

            SendUnreliable(message);

            //Logger.Info("Received time message: " + message.turn + " " + message.millis + " " + message.turnMillis);
#if BACKEND
            double timeBeforeSync = VirtualSpaceTime.CurrentTimeInMillis;
            VirtualSpaceTime.Update(message.Millis, message.TripTime);
            double timeAfterSync = VirtualSpaceTime.CurrentTimeInMillis;

            double timeOffset = Math.Abs(timeAfterSync - timeBeforeSync);
            if (timeOffset > 20)
                Logger.Warn($"{message.UserId}: Time offset was {Math.Abs(timeOffset)}");
#elif UNITY
            LastTimeMessage = message;
#endif
            //Debug.Log("Handled time message");
        }

        public bool IsRegistered()
        {
            return _registeredAtServer;
        }

        public bool IsReregistered()
        {
            return _registeredAtServer && _reregistered;
        }

        public void Stop()
        {
            _deregisterFromServer();
            Thread.Sleep(200);
            StopListening();
        }

        public void AddHandler(Type type, Action<IMessageBase> handler)
        {
            _networkEventHandler.Attach(type, handler);
        }

        public void RemoveHandler(Type type, Action<IMessageBase> handler)
        {
            _networkEventHandler.Detach(type, handler);
        }
    }
}
