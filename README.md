# AttributeNetworkWrapperV2

Utility to mimic some Game Engine's functionality of marking functions as [RPC] that get auto serialized and sent 

## Use

This calls abstract functions that you must override to handle sending and recieving rpc's.

Classes that you must override are NetworkManager and Transport.

You call rpcs by using either Server/Client/Multi Rpc Attribute on methods, whose class has to have the 'partial' keyword:
```csharp
[ServerRpc(Reliable)]
public static void Server_DoStuff(int param1, bool param2)
{

}
```
(Server_ prefix just used for readability)

on build, this generates:

```csharp
[ServerRpc(Reliable)]
public static void CallRpc_Server_DoStuff(int param1, bool param2)
{
      if (NetworkManager.Instance == null) return;

      using NetworkWriter writer = new NetworkWriter();
      writer.WritesShort(FunctionHash); //defined on build
      writer.WriteInt(param1);
      writer.WriteBool(Param2);
      NetworkManager.Instance.SendRpcToServer(writer);
}
```
Note, all rpc's MUST be 'public static void'

When this function gets called, it sends the rpc to the Server, where it then will be deserialized and called with the supplied args.



Any method using [ClientRpc] will have a ClientNetworkConnection parameter added at the start, this arg is the client this rpc will be sent to.

Example:
```csharp
[ClientRpc]
public static void Client_DoStuff(int param1, bool param2)

// Generated
public static void CallRpc_Client_DoStuff(ClientNetworkConnection target, int param1, bool param2) 
```

And a function using [ServerRpc] can optionally have a ClientNetworkConnection parameter that will be suplied with the client who called this rpc.

Example:
```csharp
[ServerRpc]
public static void Server_DoStuff(ClientNetworkConnection sender)
{
      CallRpc_Client_DoStuff(sender, 1, true);
}
```

## Sending custom data

To send and receive your own data types, you can write Extension methods for NetworkWriter/NetworkReader, like:

```csharp
public static void WriteVector2(this NetworkWriter writer, Vector2 vector)
{
    writer.write(vector.x);
    writer.write(vector.y);
}

public static Vector2 ReadVector2(this NetworkReader reader)
{
    return new Vector2(reader.ReadSingle(), reader.ReadSingle());
}
```

These extensions are detected while bulding and used where necessary, such as:

```csharp
[MultiRpc(Unreliable)] //Gets invoked on every client, called by server.
public static void Multi_DoStuff(Vector2 vec) 
```

# Starting Server

You first init your NetworkManager with your transport

```csharp
CustomNetworkManager netManager = new();
netManager.Init(new CustomTransport()))   
```

Then you call StartServer(IsPeer) or ConnectToServer(Address), which should be handled by their respective Transport methods.
