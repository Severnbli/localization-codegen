using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Severnbli.LocalizationCodegen.Tests
{
    public class LocalizedTablesTests
    {
        [Test]
        public void GetString_ReturnsKey_WhenSettingsAreMissing()
        {
            if (LocalizationSettings.HasSettings)
                Assert.Ignore("This test project has LocalizationSettings configured.");

            Assert.AreEqual("Key", LocalizedTables.GetString("Table", "Key"));
        }

        [Test]
        public void GetStringWithArguments_ReturnsKey_WhenSettingsAreMissing()
        {
            if (LocalizationSettings.HasSettings)
                Assert.Ignore("This test project has LocalizationSettings configured.");

            Assert.AreEqual("Key", LocalizedTables.GetString("Table", "Key", 1, 2));
        }

        [Test]
        public void GetStringMixed_ReturnsKey_WhenSettingsAreMissing()
        {
            if (LocalizationSettings.HasSettings)
                Assert.Ignore("This test project has LocalizationSettings configured.");

            var named = new System.Collections.Generic.Dictionary<string, object> { ["count"] = 2 };
            Assert.AreEqual("Key", LocalizedTables.GetStringMixed("Table", "Key", new object[] { 1 }, named));
        }

        [Test]
        public void GetAsset_ReturnsNull_WhenSettingsAreMissing()
        {
            if (LocalizationSettings.HasSettings)
                Assert.Ignore("This test project has LocalizationSettings configured.");

            Assert.IsNull(LocalizedTables.GetAsset<Object>("Table", "Key"));
        }
    }
}
