using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Http;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Selects exactly one SAP GUI session from discovered connections according to <see cref="SapOptionsModel"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Selection is deterministic: every supplied criterion must match, exactly one session must remain, and any
    /// other outcome fails with a classified <see cref="SapDriverException"/>. The selector never picks the first
    /// of several candidates. Sessions of connections whose server disabled scripting are never candidates.
    /// </para>
    /// <para>
    /// The selector is compute-only and works on COM-free discovery models, so it is fully unit-testable.
    /// </para>
    /// </remarks>
    public static class SessionSelector
    {
        #region *** Constants    ***
        // Accepts session paths with or without the leading /app/ segment, such as con[0]/ses[1].
        private static readonly Regex SessionPathPattern = new(@"^(/app/)?con\[\d+\]/ses\[\d+\]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Validates option values whose malformed forms can never match a session.
        /// </summary>
        /// <remarks>
        /// Called before discovery so a typo is reported as an invalid argument even while SAP GUI is unavailable.
        /// The options object is not modified.
        /// </remarks>
        /// <param name="options">The selection criteria.</param>
        /// <exception cref="SapDriverException">An index is below -1 or the session path is malformed (HTTP 400).</exception>
        public static void AssertOptions(SapOptionsModel options)
        {
            // Require the options object; the sessions repository always supplies one.
            ArgumentNullException.ThrowIfNull(argument: options);

            // Reject negative indexes other than the "not specified" sentinel.
            if (options.ConnectionIndex < SapOptionsModel.NoIndex || options.SessionIndex < SapOptionsModel.NoIndex)
            {
                throw NewInvalidArgumentException("sap:options connectionIndex and sessionIndex must be zero or greater.");
            }

            // Reject session paths that are not of the form /app/con[n]/ses[n].
            var isPathMalformed = !string.IsNullOrWhiteSpace(options.SessionPath) && !SessionPathPattern.IsMatch(options.SessionPath.Trim());
            if (isPathMalformed)
            {
                throw NewInvalidArgumentException("sap:options sessionPath must have the form /app/con[n]/ses[n].");
            }
        }

        /// <summary>
        /// Converts a session path to its absolute, lowercase-prefixed form, such as <c>/app/con[0]/ses[0]</c>.
        /// </summary>
        /// <param name="sessionPath">The session path, with or without the leading <c>/app/</c> segment.</param>
        /// <returns>The normalized path, or an empty string when <paramref name="sessionPath"/> is null or whitespace.</returns>
        public static string ConvertToSessionPath(string sessionPath)
        {
            // Treat an absent path as "no criterion".
            if (string.IsNullOrWhiteSpace(sessionPath))
            {
                return string.Empty;
            }

            // Prefix relative paths so comparisons always use the absolute scripting identifier.
            var path = sessionPath.Trim();

            return path.StartsWith("/app/", StringComparison.OrdinalIgnoreCase) ? path : $"/app/{path}";
        }

        /// <summary>
        /// Selects the single session that matches the options.
        /// </summary>
        /// <param name="connections">The discovered connections.</param>
        /// <param name="options">The selection criteria.</param>
        /// <returns>The selected session.</returns>
        /// <exception cref="SapDriverException">
        /// The options are invalid, or zero or several sessions match.
        /// </exception>
        public static SapSessionInfoModel SelectSession(IList<SapConnectionInfoModel> connections, SapOptionsModel options)
        {
            // Reject malformed criteria before matching so typos are reported as such.
            AssertOptions(options);

            // Fail fast when SAP Logon has no open connection at all.
            if (connections.Count == 0)
            {
                const string noConnectionMessage = "No SAP GUI connection is open. Open a connection in SAP Logon " +
                    "and log on before creating a session.";
                throw SapDriverException.NewSessionNotCreated(SapErrorKinds.NoMatchingSession, noConnectionMessage);
            }

            // Collect the scriptable candidates and keep those that satisfy every supplied criterion.
            var candidates = connections
                .Where(i => !i.ScriptingDisabledByServer)
                .SelectMany(i => i.Sessions)
                .ToList();

            var matches = candidates
                .Where(i => TestSession(i, options))
                .ToList();

            // Attach when exactly one session matches.
            if (matches.Count == 1)
            {
                return matches[0];
            }

            // Refuse to guess between several matches; list them so the caller can choose.
            if (matches.Count > 1)
            {
                var ambiguousMessage = $"{matches.Count} SAP GUI sessions match the requested selection. " +
                    "Set sap:options.sessionPath, or connectionIndex and sessionIndex, to choose one.";
                var ambiguousData = NewCandidateData(matches);
                throw SapDriverException.NewSessionNotCreated(SapErrorKinds.AmbiguousSession, ambiguousMessage, ambiguousData);
            }

            // Explain an empty result caused by server-side disabled scripting before the generic "no match".
            var isDisabledByServer = connections.Any(i => i.ScriptingDisabledByServer && TestConnection(i, options));
            if (isDisabledByServer)
            {
                const string disabledMessage = "SAP GUI Scripting is disabled by the SAP application server for the " +
                    "selected connection. Ask your SAP administrator to set the profile parameter sapgui/user_scripting.";
                throw SapDriverException.NewSessionNotCreated(SapErrorKinds.ScriptingDisabledByServer, disabledMessage);
            }

            // Report that nothing matched, with the available sessions for comparison.
            const string noMatchMessage = "No open SAP GUI session matches the requested selection.";
            var noMatchData = NewCandidateData(candidates);
            throw SapDriverException.NewSessionNotCreated(SapErrorKinds.NoMatchingSession, noMatchMessage, noMatchData);
        }

        /// <summary>
        /// Tests whether a session satisfies every supplied criterion.
        /// </summary>
        /// <param name="session">The discovered session.</param>
        /// <param name="options">The selection criteria.</param>
        /// <returns>True when every supplied criterion matches; otherwise false.</returns>
        public static bool TestSession(SapSessionInfoModel session, SapOptionsModel options)
        {
            // Evaluate each criterion only when the caller supplied it.
            var sessionPath = ConvertToSessionPath(options.SessionPath);
            var isPathMatch = sessionPath.Length == 0 || string.Equals(session.Id, sessionPath, StringComparison.OrdinalIgnoreCase);
            var isConnectionMatch = options.ConnectionIndex == SapOptionsModel.NoIndex || session.ConnectionIndex == options.ConnectionIndex;
            var isSessionMatch = options.SessionIndex == SapOptionsModel.NoIndex || session.Index == options.SessionIndex;
            var isDescriptionMatch = TestText(expected: options.ConnectionDescription, actual: session.ConnectionDescription);
            var isSystemMatch = TestText(expected: options.SystemName, actual: session.SystemName);
            var isClientMatch = TestText(expected: options.Client, actual: session.Client);

            return isPathMatch && isConnectionMatch && isSessionMatch && isDescriptionMatch && isSystemMatch && isClientMatch;
        }

        // Builds an invalid-argument failure for malformed sap:options values.
        private static SapDriverException NewInvalidArgumentException(string message)
        {
            // Invalid arguments use HTTP 400 and carry no SAP error kind, because no SAP call was made.
            return new SapDriverException(WebDriverErrorCodes.InvalidArgument, errorKind: null, StatusCodes.Status400BadRequest, message);
        }

        // Builds the client-safe data listing candidate sessions by identifier and system metadata.
        private static Dictionary<string, object> NewCandidateData(IEnumerable<SapSessionInfoModel> sessions)
        {
            // Expose identifiers, SID, and client only; user names and transactions are never collected.
            var candidates = sessions
                .Select(i => new { i.Id, i.SystemName, i.Client, i.ConnectionDescription })
                .ToList();

            return new Dictionary<string, object>
            {
                ["candidates"] = candidates
            };
        }

        // Tests whether a connection satisfies the connection-level criteria (index, description, path prefix).
        // Used only to explain an empty result for server-disabled connections, which expose no sessions.
        private static bool TestConnection(SapConnectionInfoModel connection, SapOptionsModel options)
        {
            // Compare the connection part of the session path, such as /app/con[0].
            var sessionPath = ConvertToSessionPath(options.SessionPath);
            var isPathMatch = sessionPath.Length == 0 || sessionPath.StartsWith($"{connection.Id}/", StringComparison.OrdinalIgnoreCase);
            var isIndexMatch = options.ConnectionIndex == SapOptionsModel.NoIndex || connection.Index == options.ConnectionIndex;
            var isDescriptionMatch = TestText(expected: options.ConnectionDescription, actual: connection.Description);

            return isPathMatch && isIndexMatch && isDescriptionMatch;
        }

        // Compares an optional text criterion case-insensitively after trimming; an absent criterion always matches.
        private static bool TestText(string expected, string actual)
        {
            // Treat null, empty, and whitespace criteria as "any value".
            if (string.IsNullOrWhiteSpace(expected))
            {
                return true;
            }

            return string.Equals(expected.Trim(), actual?.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        #endregion
    }
}
