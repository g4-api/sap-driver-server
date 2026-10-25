using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("SapKeyMapper")]
    [TestCategory("UnitTest")]
    public class SapKeyMapperTests
    {
        [TestMethod(DisplayName = "Verify that W3C keys and modifiers map to the SAP virtual key table.")]
        #region *** Data Set ***
        [DataRow('', false, false, 0)]
        [DataRow('', false, false, 0)]
        [DataRow('', false, false, 12)]
        [DataRow('', false, false, 1)]
        [DataRow('', false, false, 3)]
        [DataRow('', false, false, 12)]
        [DataRow('', true, false, 13)]
        [DataRow('', false, true, 25)]
        [DataRow('', true, true, 48)]
        [DataRow('', false, false, 81)]
        [DataRow('', false, false, 82)]
        [DataRow('', false, true, 80)]
        [DataRow('', false, true, 83)]
        #endregion
        public void ConvertToVirtualKeyTest(char key, bool isShift, bool isControl, int expected)
        {
            // Act: map the key.
            var virtualKey = SapKeyMapper.ConvertToVirtualKey(key, isShift, isControl);

            // Assert: the SAP virtual key matches the table.
            Assert.AreEqual(expected, virtualKey);
        }

        [TestMethod(DisplayName = "Verify that keys without a SAP virtual key are unsupported operations.")]
        #region *** Data Set ***
        [DataRow('a', false, false)]
        [DataRow('', false, false)]
        [DataRow('', true, false)]
        [DataRow('', false, false)]
        #endregion
        public void ConvertToVirtualKeyUnsupportedTest(char key, bool isShift, bool isControl)
        {
            // Act: map the key.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => SapKeyMapper.ConvertToVirtualKey(key, isShift, isControl));

            // Assert: the failure is an explicit unsupported operation.
            Assert.AreEqual(WebDriverErrorCodes.UnsupportedOperation, exception.Error);
        }

        [TestMethod(DisplayName = "Verify that a key sequence with Shift held and released produces modified and plain virtual keys with pauses.")]
        public void ConvertToStepsSequenceTest()
        {
            // Arrange: Shift+F3, release Shift, pause 50 ms, Enter.
            var actions = NewKeyActions(
                ("keyDown", ""),
                ("keyDown", ""),
                ("keyUp", ""),
                ("keyUp", ""),
                ("pause", null),
                ("keyDown", ""),
                ("keyUp", ""));

            // Act: convert the payload.
            var steps = SapKeyMapper.ConvertToSteps(actions);

            // Assert: Shift+F3 (15), a 50 ms pause, then Enter (0).
            var expectedKeys = new[] { 15, SapKeyMapper.PauseStep, 0 };
            Assert.AreSequenceEqual(expectedKeys, steps.Select(i => i.VirtualKey));
            Assert.AreEqual(50, steps[1].PauseMilliseconds);
        }

        [TestMethod(DisplayName = "Verify that pointer actions and character keys fail before any step is produced.")]
        public void ConvertToStepsUnsupportedTest()
        {
            // Arrange: a pointer source with a move, and a key source that types a character.
            var pointer = new ActionsModel
            {
                Actions =
                [
                    new ActionsModel.ActionModel
                    {
                        Id = "mouse",
                        Type = "pointer",
                        Actions = [new Dictionary<string, object> { ["type"] = "pointerMove", ["x"] = 1, ["y"] = 1 }]
                    }
                ]
            };
            var typing = NewKeyActions(("keyDown", ""), ("keyDown", "a"));

            // Act: convert both payloads.
            var pointerException = Assert.ThrowsExactly<SapDriverException>(() => SapKeyMapper.ConvertToSteps(pointer));
            var typingException = Assert.ThrowsExactly<SapDriverException>(() => SapKeyMapper.ConvertToSteps(typing));

            // Assert: both are unsupported operations.
            Assert.AreEqual(WebDriverErrorCodes.UnsupportedOperation, pointerException.Error);
            Assert.AreEqual(WebDriverErrorCodes.UnsupportedOperation, typingException.Error);
        }

        [TestMethod(DisplayName = "Verify that an empty payload produces no steps.")]
        public void ConvertToStepsEmptyTest()
        {
            // Act: convert null and empty payloads.
            var fromNull = SapKeyMapper.ConvertToSteps(null);
            var fromEmpty = SapKeyMapper.ConvertToSteps(new ActionsModel { Actions = [] });

            // Assert: both are empty.
            Assert.IsEmpty(fromNull);
            Assert.IsEmpty(fromEmpty);
        }

        // Builds one key input source from (type, value) items; pause items get a 50 ms duration.
        private static ActionsModel NewKeyActions(params (string Type, string Value)[] items)
        {
            var actions = items
                .Select(i => i.Type == "pause"
                    ? new Dictionary<string, object> { ["type"] = "pause", ["duration"] = 50 }
                    : new Dictionary<string, object> { ["type"] = i.Type, ["value"] = i.Value })
                .ToList();

            return new ActionsModel
            {
                Actions = [new ActionsModel.ActionModel { Actions = actions, Id = "keyboard", Type = "key" }]
            };
        }
    }
}
