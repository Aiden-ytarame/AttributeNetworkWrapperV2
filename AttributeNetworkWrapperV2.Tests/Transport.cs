namespace AttributeNetworkWrapperV2.Tests;

public class LocalTransport : Transport
{
    private int counter = 0;
    public override void ConnectClient(string address)
    {
        IsActive = true;
        OnServerClientConnected?.Invoke(new (counter++, "localhost"));
        OnClientConnected?.Invoke(new ServerNetworkConnection("localhost"));
    }

    public override void StopClient()
    {
        IsActive = false;
    }

    public override void StartServer()
    {
        IsActive = true;
        IsServer = true;
    }

    public override void StopServer()
    {
        IsActive = false;
        IsServer = false;
    }

    public override void KickConnection(int connectionId)
    {
        
    }

    public override void SendMessageToServer(ReadOnlySpan<byte> data, SendType sendType = SendType.Reliable)
    {
        Console.WriteLine($"Server received {data.Length} bytes");
        OnServerDataReceived?.Invoke(new(0, "localhost"), data);
    }

    public override void SendMessageToClient(int connectionId, ReadOnlySpan<byte> data, SendType sendType = SendType.Reliable)
    {
        Console.WriteLine($"Client {connectionId} received {data.Length} bytes");
        OnClientDataReceived?.Invoke(data);
    }

    public override void Shutdown()
    {
        IsActive = false;
        IsServer = false;
    }
}