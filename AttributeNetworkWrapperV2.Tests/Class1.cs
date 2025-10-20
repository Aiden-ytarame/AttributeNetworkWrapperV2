
using System.Numerics;
using NUnit.Framework;

namespace AttributeNetworkWrapperV2.Tests;

public partial class TestClass
{
    [ServerRpc(SendType.Unreliable)]
    public void TestMethod(int a, int b, int c, Vector3 val = new())
    {
        CallRpc_TestMethod(1,1, 3);
    }
}

public static class Extension
{
    public static void WriteInt(this NetworkWriter writer, Vector3 value)
    {
        
    }
    
    public static Vector3 ReadInt(this NetworkReader reader)
    {
        return new Vector3();
    }
}