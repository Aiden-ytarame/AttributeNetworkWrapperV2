namespace AttributeNetworkWrapperV2
{
    /// <summary>
    /// Rpc is reliable or Unreliable
    /// </summary>
    public enum SendType : ushort
    {
        Reliable,
        Unreliable
    }
    
    /// <summary>
    /// Rpc from Client to Server
    /// ServerRpc can have a ClientNetworkConnection as a parameter, where we pass the sender connection.
    /// When calling this, pass null for any ClientNetworkConnection parameter as they are not used.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ServerRpcAttribute : Attribute
    {
        SendType sendType;

        public ServerRpcAttribute(SendType sendType = SendType.Reliable)
        {
            this.sendType = sendType;
        }
    }

    /// <summary>
    /// Rpc from Server to Client.
    /// First Param MUST be a ClientNetworkConnection, which will be the user this is sent to.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ClientRpcAttribute : Attribute
    {
        SendType sendType;

        public ClientRpcAttribute(SendType sendType = SendType.Reliable)
        {
            this.sendType = sendType;
        }
    }
    
    
    /// <summary>
    /// Rpc from Server to all Clients
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class MultiRpcAttribute : Attribute
    {
        SendType sendType;
        public MultiRpcAttribute(SendType sendType = SendType.Reliable)
        {
            this.sendType = sendType;
        }
    }
}