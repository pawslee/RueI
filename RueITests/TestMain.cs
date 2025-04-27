using System.Reflection;

namespace RueITest;

[TestClass]
public class TestGeneral
{
    [TestMethod]
    public void TestReflection()
    {
        const string RUEIMAIN = "RueI.RueIMain";
        const string ENSUREINIT = "EnsureInit";
        const string REFLECTIONHELPERS = "RueI.Extensions.ReflectionHelpers";
        const string GETELEMENTSHOWER = "GetElementShower";

        Assembly assembly = typeof(RueI.RueIMain).Assembly;
        Type reflectionHelpers = assembly.GetType(REFLECTIONHELPERS);
        MethodInfo elementShower = reflectionHelpers.GetMethod(GETELEMENTSHOWER);
        var result = elementShower.Invoke(null, new object[] { });

        if (result is not Action<ReferenceHub, string, float, TimeSpan>)
        {
            Assert.Fail();
        }

        MethodInfo init = assembly.GetType(RUEIMAIN).GetMethod(ENSUREINIT);
        Assert.IsNotNull(init);

        init.Invoke(null, new object[] { });
    }

    [TestMethod]
    public void TestMain()
    {
        RueI.RueIMain.EnsureInit();
    }
}