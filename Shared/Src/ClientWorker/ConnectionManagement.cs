using System.Threading;

namespace VirtualSpace.Shared
{
    public partial class ClientWorker : NetworkingBaseClient
    {
        public bool _reregistered = false;

        private void _registerLoop()
        {
            while (!IsConnected())
            {
                Thread.Sleep(100);
            }

            Logger.Info("Sending registration message");
            _sendReliable(new Registration { UserName = _clientName });
        }

        private void _registerAtServer()
        {
            Thread childThread = new Thread(new ThreadStart(_registerLoop));
            childThread.Start();
        }

        private void _deregisterFromServer()
        {
            SendReliable(new Deregistration()
            {
                UserId = PlayerID
            });
        }

        protected void _confirmRegistrationAtServer(IMessageBase messageBase)
        {
            RegistrationSuccess registrationSuccess = (RegistrationSuccess)messageBase;
            PlayerID = registrationSuccess.UserId;
            _reregistered = registrationSuccess.Reregistration;
            Logger.Info("Registered with player id " + PlayerID);

            _registeredAtServer = true;

            messageQueue.ForEach(message => SendReliable(message));
            messageQueue.Clear();
        }

        private void _confirmDeregistrationFromServer(IMessageBase messageBase)
        {
            PlayerID = -1;
            _registeredAtServer = false;
            StopListening();
        }
    }
}
