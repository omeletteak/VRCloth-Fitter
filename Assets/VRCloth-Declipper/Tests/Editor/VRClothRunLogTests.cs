using NUnit.Framework;

namespace VRClothDeclipper.Tests
{
    /// <summary>
    /// Pins the pure path-relativization the run log uses to record a cloth by
    /// its shop-relative prefab path (e.g. Milltina's vs Chocolat's edition of
    /// the same "Ash_Blue_1" no longer collide to the same name).
    /// </summary>
    public class VRClothRunLogTests
    {
        [Test]
        public void RelativeFromAssets_StripsAssetsPrefix_StartsAtShopFolder()
        {
            Assert.AreEqual(
                "Chocolate rice/Funky_Street_Vive/Milltina/Cotton/Ash_Blue_1.prefab",
                VRClothRunLog.RelativeFromAssets(
                    "Assets/Chocolate rice/Funky_Street_Vive/Milltina/Cotton/Ash_Blue_1.prefab"));
        }

        [Test]
        public void RelativeFromAssets_NormalizesBackslashes()
        {
            Assert.AreEqual(
                "Shop/Cotton/Outfit.prefab",
                VRClothRunLog.RelativeFromAssets(@"Assets\Shop\Cotton\Outfit.prefab"));
        }

        [Test]
        public void RelativeFromAssets_KeepsPathsOutsideAssets()
        {
            Assert.AreEqual(
                "Packages/com.vendor.pkg/Outfit.prefab",
                VRClothRunLog.RelativeFromAssets("Packages/com.vendor.pkg/Outfit.prefab"));
        }

        [Test]
        public void RelativeFromAssets_EmptyOrNull_ReturnsEmpty()
        {
            Assert.AreEqual("", VRClothRunLog.RelativeFromAssets(""));
            Assert.AreEqual("", VRClothRunLog.RelativeFromAssets(null));
        }
    }
}
