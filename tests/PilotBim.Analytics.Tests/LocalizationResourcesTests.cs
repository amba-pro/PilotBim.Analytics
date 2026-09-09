using System;
using System.Linq;
using PilotBim.Analytics.Properties;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class LocalizationResourcesTests
    {
        [Fact]
        public void Resources_Class_IsPublicAndAccessible()
        {
            Assert.True(typeof(Resources).IsPublic);
            Assert.NotNull(Resources.ResourceManager);
        }

        [Fact]
        public void Smoke_Strings_MatchExpectedDefaults()
        {
            Assert.Equal("Сначала выполните сканирование.", Resources.Common_ScanRequiredFirst);
            Assert.Equal("Укажите заголовок.", Resources.Widget_EnterTitle);
            Assert.Equal("Укажите имя снимка.", Resources.Snapshot_EnterName);
        }

        [Fact]
        public void OpenFailedPrefixes_EndWithNewline_AndAreNonEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(Resources.Catalog_OpenFailedPrefix));
            Assert.False(string.IsNullOrWhiteSpace(Resources.Analytics_OpenFailedPrefix));
            Assert.EndsWith("\n", Resources.Catalog_OpenFailedPrefix.Replace("\r\n", "\n"));
            Assert.EndsWith("\n", Resources.Analytics_OpenFailedPrefix.Replace("\r\n", "\n"));
            Assert.Contains("каталог данных", Resources.Catalog_OpenFailedPrefix);
            Assert.Contains("аналитику", Resources.Analytics_OpenFailedPrefix);
        }

        [Fact]
        public void ManifestResource_IsEmbeddedInPluginAssembly()
        {
            var names = typeof(Resources).Assembly.GetManifestResourceNames();
            Assert.Contains(
                names,
                n => n.Equals("PilotBim.Analytics.Properties.Resources.resources", StringComparison.Ordinal));
        }
    }
}
