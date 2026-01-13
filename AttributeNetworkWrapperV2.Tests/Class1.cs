
using System.Runtime.CompilerServices;

namespace AttributeNetworkWrapperV2.Tests;


public struct TestData(int data1, int data2)
{
    public int Data1 = data1;
    public int Data2 = data2;
}

public partial class TestClass
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    [ClientRpc]
    public static void TestMethodClient(int a, TestData b, int c = 11)
    {
        
    }

    [MethodImpl(8)]
    [ServerRpc]
    public static void TestMethodServer(ClientNetworkConnection caller, int a, TestData b, int c = 11)
    {
        
    }
    
    [MultiRpc]
    public static void TestMethodMulti(int a, TestData b, int c = 11)
    {
      
    }
    
    public static void Test()
    {
        CallRpc_TestMethodClient(null, 1, new TestData());
        CallRpc_TestMethodServer(1, new TestData());
        CallRpc_TestMethodMulti(1, new TestData());
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

