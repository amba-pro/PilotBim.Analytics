using System;
using System.Collections.Generic;
using System.IO;
using PilotBim.Analytics.Models;
using PilotBim.Analytics.Services;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    public sealed class StateMappingServiceTests : IDisposable
    {
        private readonly string _path;
        private readonly string _backupPath;
        private readonly bool _hadFile;
        private readonly byte[] _backupBytes;

        public StateMappingServiceTests()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PilotBim.Analytics");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "state-mapping.json");
            _backupPath = _path + ".stage2.bak";
            _hadFile = File.Exists(_path);
            if (_hadFile)
            {
                _backupBytes = File.ReadAllBytes(_path);
                File.Copy(_path, _backupPath, true);
            }
        }

        public void Dispose()
        {
            if (_hadFile)
            {
                File.WriteAllBytes(_path, _backupBytes);
                if (File.Exists(_backupPath))
                    File.Delete(_backupPath);
            }
            else if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }

        [Fact]
        public void ApplyOverrides_NullOrEmpty_NoThrow()
        {
            var sut = new StateMappingService();
            sut.ApplyOverrides(null);
            sut.ApplyOverrides(new List<StateInventoryRecord>());
        }

        [Fact]
        public void ApplyOverrides_NoMappingFile_LeavesSemanticsUnchanged()
        {
            if (File.Exists(_path))
                File.Delete(_path);

            var state = new StateInventoryRecord
            {
                StateId = Guid.NewGuid(),
                Name = "open",
                SemanticStatus = "PRESET"
            };

            new StateMappingService().ApplyOverrides(new List<StateInventoryRecord> { state });
            Assert.Equal("PRESET", state.SemanticStatus);
        }

        [Fact]
        public void ApplyOverrides_MapsByGuid_Name_AndNormalizesKnownValues()
        {
            var idOpen = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var idCustom = Guid.Parse("11111111-2222-3333-4444-555555555555");

            File.WriteAllText(_path,
                "{\n" +
                "  \"" + idOpen + "\": \"open\",\n" +
                "  \"" + idCustom.ToString("D") + "\": \"CLOSED\",\n" +
                "  \"by-name\": \"rejected\",\n" +
                "  \"keep-weird\": \"IN_PROGRESS\",\n" +
                "  \"blank\": \"   \",\n" +
                "  # comment line\n" +
                "  \"ignored-no-colon\"\n" +
                "}\n");

            var states = new List<StateInventoryRecord>
            {
                new StateInventoryRecord { StateId = idOpen, Name = "x", SemanticStatus = CapabilityStatus.Unknown },
                new StateInventoryRecord { StateId = idCustom, Name = "y", SemanticStatus = CapabilityStatus.Unknown },
                new StateInventoryRecord { StateId = Guid.NewGuid(), Name = "by-name", SemanticStatus = CapabilityStatus.Unknown },
                new StateInventoryRecord { StateId = Guid.NewGuid(), Name = "keep-weird", SemanticStatus = CapabilityStatus.Unknown },
                new StateInventoryRecord { StateId = Guid.NewGuid(), Name = "blank", SemanticStatus = "KEEP" },
                new StateInventoryRecord { StateId = Guid.NewGuid(), Name = "unknown-key", SemanticStatus = "UNTOUCHED" }
            };

            new StateMappingService().ApplyOverrides(states);

            Assert.Equal("OPEN", states[0].SemanticStatus);
            Assert.Equal("CLOSED", states[1].SemanticStatus);
            Assert.Equal("REJECTED", states[2].SemanticStatus);
            Assert.Equal("IN_PROGRESS", states[3].SemanticStatus); // unknown token kept trimmed as-is
            // blank value "   " is stored (length>0 after quote trim) → Normalize → UNKNOWN
            Assert.Equal(CapabilityStatus.Unknown, states[4].SemanticStatus);
            Assert.Equal("UNTOUCHED", states[5].SemanticStatus);
        }
    }
}
