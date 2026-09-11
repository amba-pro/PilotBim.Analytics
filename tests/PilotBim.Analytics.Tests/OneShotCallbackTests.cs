using System;
using PilotBim.Analytics.Data;
using Xunit;

namespace PilotBim.Analytics.Tests
{
    /// <summary>
    /// Characterizes the TD-14 one-shot completion latch used by InventoryService.LoadChildren.
    /// </summary>
    public sealed class OneShotCallbackTests
    {
        [Fact]
        public void Wrap_NormalCompletion_InvokesOnce()
        {
            var calls = 0;
            var once = OneShotCallback.Wrap((string _) => calls++);
            once("ok");
            Assert.Equal(1, calls);
        }

        [Fact]
        public void Wrap_DuplicateCompletion_InvokesOnce()
        {
            var calls = 0;
            var once = OneShotCallback.Wrap((string _) => calls++);
            once("a");
            once("b");
            once("c");
            Assert.Equal(1, calls);
        }

        [Fact]
        public void Wrap_SuccessThenError_InvokesOnce()
        {
            var last = (string)null;
            var calls = 0;
            var once = OneShotCallback.Wrap((string value) =>
            {
                calls++;
                last = value;
            });

            once("success");
            once("error");

            Assert.Equal(1, calls);
            Assert.Equal("success", last);
        }

        [Fact]
        public void Wrap_ErrorThenSuccess_InvokesOnce()
        {
            var last = (string)null;
            var calls = 0;
            var once = OneShotCallback.Wrap((string value) =>
            {
                calls++;
                last = value;
            });

            once("error");
            once("success");

            Assert.Equal(1, calls);
            Assert.Equal("error", last);
        }

        [Fact]
        public void Wrap_NullAction_DoesNotThrow()
        {
            var once = OneShotCallback.Wrap<string>(null);
            once("x");
            once("y");
        }

        [Fact]
        public void Wrap_Parameterless_Duplicate_InvokesOnce()
        {
            var calls = 0;
            var once = OneShotCallback.Wrap(() => calls++);
            once();
            once();
            Assert.Equal(1, calls);
        }
    }
}
