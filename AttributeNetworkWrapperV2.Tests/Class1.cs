
using System.Runtime.CompilerServices;

namespace AttributeNetworkWrapperV2.Tests;


public struct TestData(int data1, int data2)
{
    public int Data1 = data1;
    public int Data2 = data2;
}

public partial class TestClass
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    [ClientRpc]
    public static void TestMethodClient(int a, TestData b, int c = 11)
    {
        Console.WriteLine($"TestMethodClient: {a}, {b.Data1}/{b.Data2}, {c}");
    }

    [MethodImpl(8)]
    [ServerRpc]
    public static void TestMethodServer(ClientNetworkConnection caller, int a, TestData b, int c = 11)
    {
        Console.WriteLine($"TestMethodServer({a}, {b.Data1}/{b.Data2}, {c})");
    }
    
    [MultiRpc]
    public static void TestMethodMulti(int a, TestData b, int c = 11)
    {
      Console.WriteLine($"TestMethodMulti({a}, {b.Data1}/{b.Data2}, {c})");
    }
    
    public static void Test()
    {
        //this isnt a good test, fix later
        //the source gen looks right, and functionally it works on my test project
        NetworkManager server = new NetworkManager();
        server.Init(new LocalTransport());
        server.StartServer(true);
        
        CallRpc_TestMethodServer(1, new TestData(10, 15), 20);
        CallRpc_TestMethodMulti(2, new TestData(1, 2));
        CallRpc_TestMethodClient(server.ServerSelfPeerConnection, 1, new TestData());
    }
}

public static class Extension
{
    public static void WriteTestData(this NetworkWriter writer, TestData data)
    {
        writer.Write(data.Data1);
        writer.Write(data.Data2);
    }

    public static TestData ReadTestData(this NetworkReader reader)
    {
        return new TestData(reader.ReadInt32(), reader.ReadInt32());
    }
}

