using System;
using VirtualSpace.Shared;

namespace VirtualSpace.Backend
{
    public partial class BackendWorker
    {
        protected override void _handleMessage(IMessageBase message, int sessionId)
        {
            Type messageType = SubtypeUtility.GetTypeOfSubtype(message);

            Registration reg = message as Registration;
            if (reg != null)
                reg.sessionId = sessionId;
            FrontendRegistration fReg = message as FrontendRegistration;
            if (fReg != null)
                fReg.SessionId = sessionId;

            _networkEventHandler.GetHandler(messageType).Invoke(message);
        }

        public bool SendReliable(IMessageBase message)
        {
            var sessionId = GetSessionId(message);
            var sendMessage = sessionId >= 0;
            if (sendMessage)
            {
                _sendReliable(message, sessionId);
            }

            return sendMessage;
        }
        
        public bool SendUnreliable(IMessageBase message)
        {
            var sessionId = GetSessionId(message);
            var sendMessage = sessionId > 0;

            if (sendMessage)
            {
                _sendUnreliable(message, sessionId);
            }

            return sendMessage;
        }

        private int GetSessionId(IMessageBase message)
        {
            int sessionId = -1;
            
            MessageBase messageBase = (MessageBase)message;
            if (!_sessionIds.TryGetValue(messageBase.UserId, out sessionId) ||
                    !_isSessionConnected(sessionId))
                sessionId = -1;
            
            return sessionId;
        }
    }
}
