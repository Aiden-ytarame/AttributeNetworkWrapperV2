namespace AttributeNetworkWrapperV2.Tests;

public class LocalTransport : Transport
{
    public override void ConnectClient(string address)
    {
        IsActive = true;
        OnClientConnected?.Invoke(new ServerNetworkConnection(address));
    }

    public override void StopClient()
    {
        IsActive = false;
    }

    public override void StartServer()
    {
        IsActive = true;
    }

    public override void StopServer()
    {
        IsActive = false;
    }

    public override void KickConnection(int connectionId)
    {
        
    }

    public override void SendMessageToServer(ArraySegment<byte> data, SendType sendType = SendType.Reliable)
    {
    }

    public override void SendMessageToClient(int connectionId, ArraySegment<byte> data, SendType sendType = SendType.Reliable)
    {
    }

    public override void Shutdown()
    {
        IsActive = false;
    }
}