namespace AttributeNetworkWrapperV2
{
    /// <summary>
    /// Transport responsible for sending data to the Clients/Server
    /// Inheritors must be sure to set all variables and call the respective Events of the Transport
    /// </summary>
    public abstract class Transport
    {
        public delegate void OnClientDataReceivedDelegate(ReadOnlySpan<byte> data);
        public delegate void OnServerDataReceivedDelegate(ClientNetworkConnection connection, ReadOnlySpan<byte> data);
        
        /// <summary>
        /// Singleton of a Transport
        /// </summary>
        public static Transport? Instance { get; set; }
        
        /// <summary>
        /// Whether this transport is connected and active.
        /// </summary>
        public bool IsActive { get; protected set; }
        
        /// <summary>
        /// Whether this transport is a server.
        /// </summary>
        public bool IsServer { get; protected set; }
        
        // CLIENT //////////////////////////////////
        
        //Events called by the Transport, used by NetworkManager
        public OnClientDataReceivedDelegate OnClientDataReceived;
        public Action<ServerNetworkConnection> OnClientConnected;
        public Action OnClientDisconnected;
      
        /// <summary>
        /// Connects to a server on the Address
        /// </summary>
        /// <param name="address">Address of the server to connect to</param>
        public abstract void ConnectClient(string address);
        public abstract void StopClient();
        
     
        // SERVER /////////////////////
        
        //Events called by the Transport, used by NetworkManager
        public OnServerDataReceivedDelegate OnServerDataReceived;
        public Action<ClientNetworkConnection> OnServerClientConnected;
        public Action<ClientNetworkConnection> OnServerClientDisconnected;
        public Action OnServerStarted;
        
        /// <summary>
        /// Starts the Server
        /// </summary>
        public abstract void StartServer();
        /// <summary>
        /// Stops the Server
        /// </summary>
        public abstract void StopServer();
        /// <summary>
        /// Kicks a player with that ConnectionId
        /// </summary>
        public abstract void KickConnection(int connectionId);
     
        //Send messages
        public abstract void SendMessageToServer(ReadOnlySpan<byte> data, SendType sendType = SendType.Reliable);
        public abstract void SendMessageToClient(int connectionId, ReadOnlySpan<byte> data, SendType sendType = SendType.Reliable);
        
        /// <summary>
        /// Closes both Client and Server and sets yourself to null
        /// </summary>
        public abstract void Shutdown();
    }
}