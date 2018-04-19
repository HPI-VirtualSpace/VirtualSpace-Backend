namespace VirtualSpace.Shared
{
    public partial class ClientWorker : NetworkingBaseClient
    {
        private void _setEventHandler()
        {
            _networkEventHandler.Attach(typeof(RegistrationSuccess), _confirmRegistrationAtServer);
            _networkEventHandler.Attach(typeof(DeregistrationSuccess), _confirmDeregistrationFromServer);
            _networkEventHandler.Attach(typeof(TimeMessage), _handleTimeMessage);

            _networkEventHandler.SetDefaultHandler(delegate (IMessageBase baseMessage)
            {
                Logger.Warn("Missing handler for message type " + baseMessage.GetType());
            });
        }
    }
}