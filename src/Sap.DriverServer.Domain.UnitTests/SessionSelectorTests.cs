using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("SessionSelector")]
    [TestCategory("UnitTest")]
    public class SessionSelectorTests
    {
        [TestMethod(DisplayName = "Verify that no open connection fails with noMatchingSession.")]
        public void SelectSessionNoConnectionTest()
        {
            // Act: select from an empty SAP Logon.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => SessionSelector.SelectSession([], new SapOptionsModel()));

            // Assert: the failure is a session-creation error with the matching kind.
            Assert.AreEqual(WebDriverErrorCodes.SessionNotCreated, exception.Error);
            Assert.AreEqual(SapErrorKinds.NoMatchingSession, exception.ErrorKind);
        }

        [TestMethod(DisplayName = "Verify that a single open session is selected when no criterion is supplied.")]
        public void SelectSessionSingleWithoutCriteriaTest()
        {
            // Arrange: one connection with one session.
            var connections = NewConnections(("DEV", "100", 1));

            // Act: select without criteria.
            var session = SessionSelector.SelectSession(connections, new SapOptionsModel());

            // Assert: the only session is selected.
            Assert.AreEqual("/app/con[0]/ses[0]", session.Id);
        }

        [TestMethod(DisplayName = "Verify that several open sessions without criteria fail as ambiguous and list every candidate.")]
        public void SelectSessionAmbiguousTest()
        {
            // Arrange: two connections, three sessions in total.
            var connections = NewConnections(("DEV", "100", 2), ("QAS", "200", 1));

            // Act: select without criteria.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => SessionSelector.SelectSession(connections, new SapOptionsModel()));

            // Assert: the failure is ambiguous and the candidates are listed for the caller.
            Assert.AreEqual(SapErrorKinds.AmbiguousSession, exception.ErrorKind);
            var candidates = ((IEnumerable<object>)exception.ErrorData["candidates"]).ToList();
            Assert.HasCount(3, candidates);
        }

        [TestMethod(DisplayName = "Verify that criteria narrow several sessions to exactly one.")]
        #region *** Data Set ***
        [DataRow("/app/con[1]/ses[0]", -1, -1, null, null, null, "/app/con[1]/ses[0]")]
        [DataRow("con[0]/ses[1]", -1, -1, null, null, null, "/app/con[0]/ses[1]")]
        [DataRow(null, 1, -1, null, null, null, "/app/con[1]/ses[0]")]
        [DataRow(null, 0, 1, null, null, null, "/app/con[0]/ses[1]")]
        [DataRow(null, -1, -1, null, "qas", null, "/app/con[1]/ses[0]")]
        [DataRow(null, -1, -1, "QAS - Quality", null, null, "/app/con[1]/ses[0]")]
        [DataRow(null, -1, 1, null, "DEV", "100", "/app/con[0]/ses[1]")]
        #endregion
        public void SelectSessionWithCriteriaTest(
            string sessionPath,
            int connectionIndex,
            int sessionIndex,
            string connectionDescription,
            string systemName,
            string client,
            string expectedId)
        {
            // Arrange: two DEV sessions on con[0] and one QAS session on con[1].
            var connections = NewConnections(("DEV", "100", 2), ("QAS", "200", 1));
            var options = new SapOptionsModel
            {
                Client = client,
                ConnectionDescription = connectionDescription,
                ConnectionIndex = connectionIndex,
                SessionIndex = sessionIndex,
                SessionPath = sessionPath,
                SystemName = systemName
            };

            // Act: select with the criteria.
            var session = SessionSelector.SelectSession(connections, options);

            // Assert: exactly the expected session is selected.
            Assert.AreEqual(expectedId, session.Id);
        }

        [TestMethod(DisplayName = "Verify that criteria matching several sessions still fail as ambiguous.")]
        public void SelectSessionAmbiguousCriteriaTest()
        {
            // Arrange: two DEV sessions on one connection.
            var connections = NewConnections(("DEV", "100", 2));

            // Act: select by system, which both sessions share.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => SessionSelector.SelectSession(connections, new SapOptionsModel { SystemName = "DEV" }));

            // Assert: the selector refuses to pick one.
            Assert.AreEqual(SapErrorKinds.AmbiguousSession, exception.ErrorKind);
        }

        [TestMethod(DisplayName = "Verify that criteria matching nothing fail with noMatchingSession.")]
        public void SelectSessionNoMatchTest()
        {
            // Arrange: one DEV session.
            var connections = NewConnections(("DEV", "100", 1));

            // Act: select a system that is not open.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => SessionSelector.SelectSession(connections, new SapOptionsModel { SystemName = "PRD" }));

            // Assert: the failure reports no match and lists the available sessions.
            Assert.AreEqual(SapErrorKinds.NoMatchingSession, exception.ErrorKind);
            Assert.IsTrue(exception.ErrorData.ContainsKey("candidates"));
        }

        [TestMethod(DisplayName = "Verify that sessions of server-disabled connections are never selected and the cause is reported.")]
        public void SelectSessionDisabledByServerTest()
        {
            // Arrange: one connection whose server disabled scripting.
            var connections = new List<SapConnectionInfoModel>
            {
                new()
                {
                    Description = "PRD - Production",
                    Id = "/app/con[0]",
                    Index = 0,
                    ScriptingDisabledByServer = true
                }
            };

            // Act: select without criteria.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => SessionSelector.SelectSession(connections, new SapOptionsModel()));

            // Assert: the failure names the server-side setting.
            Assert.AreEqual(SapErrorKinds.ScriptingDisabledByServer, exception.ErrorKind);
            Assert.Contains("sapgui/user_scripting", exception.Message);
        }

        [TestMethod(DisplayName = "Verify that malformed options fail with invalid argument (HTTP 400).")]
        #region *** Data Set ***
        [DataRow("/app/con[0]", -1)]
        [DataRow("ses[0]", -1)]
        [DataRow(null, -2)]
        #endregion
        public void SelectSessionInvalidOptionsTest(string sessionPath, int connectionIndex)
        {
            // Arrange: one open session and malformed options.
            var connections = NewConnections(("DEV", "100", 1));
            var options = new SapOptionsModel { ConnectionIndex = connectionIndex, SessionPath = sessionPath };

            // Act: select with the malformed options.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => SessionSelector.SelectSession(connections, options));

            // Assert: the failure is an invalid argument.
            Assert.AreEqual(WebDriverErrorCodes.InvalidArgument, exception.Error);
            Assert.AreEqual(400, exception.StatusCode);
        }

        // Builds connections from (system, client, session count) tuples; connection n gets description "<SID> - …".
        private static List<SapConnectionInfoModel> NewConnections(params (string SystemName, string Client, int Sessions)[] specifications)
        {
            var connections = new List<SapConnectionInfoModel>();

            for (var index = 0; index < specifications.Length; index++)
            {
                var (systemName, client, count) = specifications[index];
                var description = systemName == "QAS" ? "QAS - Quality" : $"{systemName} - Development";
                var connection = new SapConnectionInfoModel { Description = description, Id = $"/app/con[{index}]", Index = index };

                for (var sessionIndex = 0; sessionIndex < count; sessionIndex++)
                {
                    connection.Sessions.Add(new SapSessionInfoModel
                    {
                        Client = client,
                        ConnectionDescription = description,
                        ConnectionIndex = index,
                        Id = $"/app/con[{index}]/ses[{sessionIndex}]",
                        Index = sessionIndex,
                        SystemName = systemName
                    });
                }

                connections.Add(connection);
            }

            return connections;
        }
    }
}
