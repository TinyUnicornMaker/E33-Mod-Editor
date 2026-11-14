using E33ModEditor.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace E33Packer.Tests;

[TestClass]
public sealed class PictoScalingTests
{
    [TestMethod]
    public void ScaleValue_IsLinearOnePercentPast33()
    {
        Assert.AreEqual(1000, PictoScalingKit.ScaleValue(1000, 33), 1e-9);
        Assert.AreEqual(1010, PictoScalingKit.ScaleValue(1000, 34), 1e-6);
        Assert.AreEqual(1020, PictoScalingKit.ScaleValue(1000, 35), 1e-6);
        Assert.AreEqual(1200, PictoScalingKit.ScaleValue(1000, 53), 1e-6);
        Assert.AreEqual(4000, PictoScalingKit.ScaleValue(1000, 333), 1e-6);
    }

    [TestMethod]
    public void ScaleValue_DoesNotChangeVanillaRows()
    {
        for (int level = 0; level <= PictoScalingKit.VanillaMaxLevel; level++)
            Assert.AreEqual(1234.5, PictoScalingKit.ScaleValue(1234.5, level), 1e-9);
    }
}
