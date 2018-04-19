using System;
using System.Collections.Generic;

namespace VirtualSpace.Shared
{
    public partial class ClientWorker : NetworkingBaseClient
    {
        protected override void _handleMessage(IMessageBase message)
        {
            Type messageType = SubtypeUtility.GetTypeOfSubtype(message);

            Logger.Debug("Received " + messageType.Name + ". Invoking handler.");

            //Thread childThread = new Thread(
            //    delegate ()
            //    {
            _networkEventHandler.Invoke(messageType, message);
            //    });
            //childThread.Start();
        }

        private List<MessageBase> messageQueue = new List<MessageBase>();

        public void SendReliable(MessageBase message)
        {
            if (!IsRegistered())
            {
                messageQueue.Add(message);
                return;
            }
            
            //Logger.Debug("Sending " + message.GetType());
            message.UserId = PlayerID;
            _sendReliable(message);
        }

        public void SendUnreliable(MessageBase message)
        {
            if (!IsRegistered())
            {
                return;
            }

            //Logger.Debug("Sending " + message.GetType());
            message.UserId = PlayerID;
            _sendUnreliable(message);
        }
    }
}
