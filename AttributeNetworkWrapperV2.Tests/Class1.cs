
using System.Numerics;

namespace AttributeNetworkWrapperV2.Tests;

    public partial class TestClass
    {
        [MultiRpc(SendType.Unreliable)]
        public static void TestMethod(int a, int b, int c = 11)
        {
            CallRpc_TestMethod(1,1);
        }
    }

    public static class Extension
    {
     
    }

