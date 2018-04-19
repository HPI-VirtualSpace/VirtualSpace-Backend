using System;
#if BACKEND
using System.Collections.Concurrent;
#endif
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;
#if UNITY
using UnityEngine;
#endif

namespace VirtualSpace.Shared
{
    public class NetworkingClient : NetworkingBaseClient
    {
        public Action<IMessageBase> OnHandleMessage;
        public Action OnConnectionEstablished;

        protected override void _handleMessage(IMessageBase message)
        {
            if (OnHandleMessage != null)
            {
                OnHandleMessage(message);
            }
        }

        protected override void _onConnectionEstablished()
        {
            if (OnConnectionEstablished != null)
                OnConnectionEstablished();
        }

        public NetworkingClient(string serverIp, int serverPort) : base(serverIp, serverPort)
        {

        }

        public void SendReliable(IMessageBase message)
        {
            _sendReliable(message);
        }
    }

    public abstract class NetworkingBaseClient : NetworkingBase
    {
        private NetPeer _server;
        private string _serverIp;
        private int _serverPort;
        private bool _shouldBeConnected;

        public NetworkingBaseClient(string serverIp, int serverPort) : base()
        {
            _serverIp = serverIp;
            _serverPort = serverPort;
            _server = null;
            _shouldBeConnected = false;
        }

        /* send/receive methods */
        protected abstract void _handleMessage(IMessageBase message);

        protected void _sendReliable(IMessageBase message)
        {
            _send(message, SendOptions.ReliableUnordered);
        }

        protected void _sendUnreliable(IMessageBase message)
        {
            _send(message, SendOptions.Unreliable);
        }

        private void _send(IMessageBase message, SendOptions options)
        {
            try
            {
                byte[] messageBytes = ProtobufUtility.Serialize(new PayloadMessage {payload = message});
                //Debug.Log("Sending message " + message.GetType());
                _server.Send(messageBytes, options);
            }
            catch (Exception e)
            {
                Debug.LogError("Error while sending message: " + e);
            }
        }

        /* LiteNetLib handler */
        public override void OnNetworkReceive(NetPeer connection, NetDataReader reader)
        {
            try
            {
                PayloadMessage message = ProtobufUtility.Unserialize<PayloadMessage>(reader.Data);
                //Debug.Log("Received message " + message.payload.GetType());
                _handleMessage(message.payload);
            }
            catch (Exception e)
            {
                Debug.LogError("Error while receiving message: " + e);
            }
        }

        public override void OnPeerConnected(NetPeer connection)
        {
            Debug.Log("Connection to server established.");
            _server = connection;
            _onConnectionEstablished();
        }

        private void _Reconnect()
        {
            Thread.Sleep(1000);
            StopListening();
            StartListening();
        }

        public override void OnPeerDisconnected(NetPeer connection, DisconnectInfo disconnectInfo)
        {
            if (_shouldBeConnected)
            {
                Debug.Log("Connection to the server couldn't be established. Retrying.");
                new Thread(new ThreadStart(_Reconnect)).Start();
            } else
            {
                Debug.Log("Connection to server lost.");
                _server = null;
            }
        }

        /* start/stop methods */
        public void StartListening()
        {
            Debug.Log("Start listening 1");
            _shouldBeConnected = true;
            Debug.Log("Start listening");
            _StartListening();
            Debug.Log("Sending connection request to server");
            _Connect(_serverIp, _serverPort);
        }

        protected abstract void _onConnectionEstablished();

        public void StopListening()
        {
            Logger.Debug("Disconnecting");
            _shouldBeConnected = false;
            Debug.Log("Disconnecting from server");
            _Disconnect(_server);
            Debug.Log("Stop listening");
            _StopListening();
        }

        public bool IsConnected()
        {
            return _server != null;
        }
    }
    
#if !UNITY
    public abstract class NetworkingBaseServer : NetworkingBase
    {
        int _nextSessionId = 0;
        BiDictionaryOneToOne<int, NetPeer> _sessions = new BiDictionaryOneToOne<int, NetPeer>();
        ConcurrentDictionary<int, long> _lastMessageTicks = new ConcurrentDictionary<int, long>();

        public NetworkingBaseServer() : base(Config.MaxPlayers + 10) { }

        /* send/receive methods */
        protected abstract void _handleMessage(IMessageBase message, int sessionId);

        protected void _sendReliable(IMessageBase message, int sessionId)
        {
            _send(message, sessionId, SendOptions.ReliableUnordered);
        }

        protected void _sendUnreliable(IMessageBase message, int sessionId)
        {
            _send(message, sessionId, SendOptions.Unreliable);
        }

        private void _send(IMessageBase message, int sessionId, SendOptions options)
        {
            NetPeer connection = null;
            _sessions.TryGetByFirst(sessionId, out connection);

            if (connection == null)
            {
                Logger.Trace("Not sending because connection for session id is missing.");
                return;
            }

            byte[] messageBytes = ProtobufUtility.Serialize(new PayloadMessage { payload = message });

            //if (message.GetType() != typeof(Incentives))
            //    Debug.Log("Send message " + message.GetType() + " to session " + sessionId);
            connection.Send(messageBytes, options);
        }

        /* LiteNetLib handler */
        public override void OnNetworkReceive(NetPeer connection, NetDataReader reader)
        {
            PayloadMessage message = ProtobufUtility.Unserialize<PayloadMessage>(reader.Data);
            var sessionId = _sessions.GetBySecond(connection);
            _lastMessageTicks[sessionId] = DateTime.Now.Ticks;
            _handleMessage(message.payload, sessionId);
        }

        public float LastMessageSecondsAgoSessionId(int sessionId)
        {
            var ticksNow = DateTime.Now.Ticks;
            if (!_lastMessageTicks.ContainsKey(sessionId))
                return float.MaxValue;

            var ticksThen = _lastMessageTicks[sessionId];

            var lastMessageSeconds = (float)(ticksNow - ticksThen) / TimeSpan.TicksPerSecond;

            return lastMessageSeconds;
        }

        public override void OnPeerConnected(NetPeer connection)
        {
            _sessions.Add(_nextSessionId, connection);
            _nextSessionId += 1;
        }

        public override void OnPeerDisconnected(NetPeer connection, DisconnectInfo disconnectInfo)
        {
            var sessionId = _sessions.GetBySecond(connection);
            //Debug.Log($"Session {sessionId} disconnecting");
            _sessions.RemoveBySecond(connection);
            _lastMessageTicks.TryRemove(sessionId, out _);
        }

        /* start/stop methods */
        protected void StartListening()
        {
            Logger.Debug("Start listening on " + Config.ServerPort);
            _StartListening(Config.ServerPort);
        }

        protected void StopListening()
        {
            _StopListening();
        }
    }
#endif
    public abstract class NetworkingBase : INetEventListener
    {
        NetManager _netManager;
        int _pollIntervalInMilliseconds = Config.PollIntervalInMilliseconds;
        Thread _pollTask;
        bool _receiveInLoop = false;

        protected NetworkingBase(int maxConnections=1)
        {
            _netManager = new NetManager(this, 100, Config.ApplicationNetworkIdentifier);
        }

        /* client/server helper */
        protected void _StartListening(int port=0)
        {
            if (port == 0)
                _netManager.Start();
            else
                _netManager.Start(port);

            _receiveInLoop = true;
            _pollTask = new Thread(new ThreadStart(_ReceiveLoop));
            _pollTask.Start();
              
            Debug.Log("Ready to receive on " + _netManager.LocalPort);
        }

        protected void _StartListeningWithoutPolling(int port = 0)
        {
            if (port == 0)
                _netManager.Start();
            else
                _netManager.Start(port);
        }

        private void _ReceiveLoop()
        {
            try
            {
                while (_receiveInLoop)
                {
                    ReceiveOnce();
                    Thread.Sleep(_pollIntervalInMilliseconds);
                }
            } catch (ThreadAbortException e)
            {
                Debug.Log("Received abort request: " + e.Message);
            }
            
        }

        public void ReceiveOnce()
        {
            _netManager.PollEvents();
        }
        
        protected void _StopListening() 
        {
            Debug.Log("Terminating thread");
            _receiveInLoop = false;
            if (_pollTask != null)
                _pollTask.Abort();
            Debug.Log("Stopping manager");
            if (_netManager != null)
                _netManager.Stop();
        }

        protected void _Connect(string address, int port)
        {
            _netManager.Connect(address, port);
        }

        protected void _Disconnect(NetPeer connection)
        {
            _netManager.DisconnectPeer(connection);
        }

        /* LiteNetLib handler */
        /* specific */
        public abstract void OnNetworkReceive(NetPeer connection, NetDataReader reader);
        public abstract void OnPeerConnected(NetPeer connection);
        public abstract void OnPeerDisconnected(NetPeer connection, DisconnectInfo disconnectInfo);

        /* general */
        public virtual void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
        {
            Debug.Log("Network error with" + endPoint + ". Socket Error Code: " + socketErrorCode);
        }

        public virtual void OnNetworkLatencyUpdate(NetPeer connection, int latency) { }

        public virtual void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
        {
            Debug.Log("Received message from unconnected " + remoteEndPoint + ".");
        }
    }
}
