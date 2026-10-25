using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("ModelSerialization")]
    [TestCategory("UnitTest")]
    public class ModelSerializationTests
    {
        // Mirrors the server's MVC JSON settings (camelCase, nulls omitted), which match the UIA server.
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Mirrors the sessions repository's lenient options binding (case-insensitive member names).
        private static readonly JsonSerializerOptions LenientJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        [TestMethod(DisplayName = "Verify that element responses serialize to the W3C element reference shape used by the UIA server.")]
        public void ElementResponseShapeTest()
        {
            // Act: serialize single and multiple element responses.
            var single = JsonSerializer.Serialize(WebDriverResponseModel.NewElementResponse("s1", "e1"), JsonOptions);
            var multiple = JsonSerializer.Serialize(WebDriverResponseModel.NewElementsResponse("s1", ["e1", "e2"]), JsonOptions);

            // Assert: the exact UIA JSON is produced.
            Assert.AreEqual("{\"session\":\"s1\",\"value\":{\"element-6066-11e4-a52e-4f735466cecf\":\"e1\"}}", single);
            Assert.AreEqual(
                "{\"session\":\"s1\",\"value\":[{\"element-6066-11e4-a52e-4f735466cecf\":\"e1\"},{\"element-6066-11e4-a52e-4f735466cecf\":\"e2\"}]}",
                multiple);
        }

        [TestMethod(DisplayName = "Verify that error responses keep the UIA error and message members and omit absent data.")]
        public void ErrorResponseShapeTest()
        {
            // Act: serialize an invalid session error and an unsupported operation error.
            var invalidSession = JsonSerializer.Serialize(WebDriverResponseModel.NewInvalidSessionResponse("s1"), JsonOptions);
            var unsupported = JsonSerializer.Serialize(WebDriverResponseModel.NewUnsupportedOperationResponse("s1", "No."), JsonOptions);

            // Assert: the W3C code is present, data appears only when set, and no stack trace is emitted.
            Assert.Contains("\"error\":\"invalid session id\"", invalidSession);
            Assert.DoesNotContain("\"data\"", invalidSession);
            Assert.DoesNotContain("stackTrace", invalidSession);
            Assert.Contains("\"data\":{\"sapErrorKind\":\"notExposedByScripting\"}", unsupported);
        }

        [TestMethod(DisplayName = "Verify that the status model serializes the operating system under the UIA 'os' member.")]
        public void StatusShapeTest()
        {
            // Arrange: a status with OS data and an unavailable SAP report.
            var status = new SessionStatusModel
            {
                Message = "m",
                OperatingSystem = new Dictionary<string, object> { ["name"] = "Windows" },
                Ready = true,
                Sap = new SapScriptingStatusModel { Available = false, ErrorKind = SapErrorKinds.SapGuiNotRunning },
                Sessions = []
            };

            // Act: serialize the status.
            var json = JsonSerializer.Serialize(status, JsonOptions);

            // Assert: the UIA member names are kept and the SAP report is additive.
            Assert.Contains("\"os\":{\"name\":\"Windows\"}", json);
            Assert.Contains("\"sap\":{\"available\":false,\"connections\":0,\"errorKind\":\"sapGuiNotRunning\"}", json);
        }

        [TestMethod(DisplayName = "Verify that sap:options deserializes with any casing and leaves absent indexes at the NoIndex sentinel.")]
        public void OptionsDeserializationTest()
        {
            // Arrange: options with mixed casing and no indexes.
            const string json = "{\"SystemName\":\"DEV\",\"client\":\"100\"}";

            // Act: deserialize with case-insensitive matching, as the sessions repository does.
            var options = JsonSerializer.Deserialize<SapOptionsModel>(json, LenientJsonOptions);

            // Assert: provided values bind and absent indexes keep the sentinel.
            Assert.AreEqual("DEV", options.SystemName);
            Assert.AreEqual("100", options.Client);
            Assert.AreEqual(SapOptionsModel.NoIndex, options.ConnectionIndex);
            Assert.AreEqual(SapOptionsModel.NoIndex, options.SessionIndex);
        }
    }
}
