using System;
using System.Globalization;

namespace Sap.DriverServer.Simulator.Hosting
{
    /// <summary>
    /// Represents the simulator's command-line options.
    /// </summary>
    /// <remarks>
    /// The options select the scenario a test needs: how many sessions are open, and whether scripting is refused on
    /// the client side (<c>GetScriptingEngine</c> returns nothing) or on the server side (the connection reports
    /// <c>DisabledByServer</c> and hides its sessions).
    /// </remarks>
    public sealed class SimulatorOptions
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the largest number of sessions the simulator opens.
        /// </summary>
        public const int MaximumSessions = 4;

        // Names the switch that hides the connection's sessions, like a server with scripting disabled.
        private const string DisabledByServerSwitch = "--disabled-by-server";

        // Names the switch that suppresses message boxes, so unattended runs never wait for input.
        private const string QuietSwitch = "--quiet";

        // Names the switch that makes GetScriptingEngine refuse, like SAP GUI with scripting disabled.
        private const string ScriptingDisabledSwitch = "--scripting-disabled";

        // Names the option that sets the number of sessions; it takes a value.
        private const string SessionsOption = "--sessions";
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets a value indicating whether the connection reports scripting as disabled by the server.
        /// </summary>
        public bool IsDisabledByServer { get; init; }

        /// <summary>
        /// Gets a value indicating whether failures are reported only through the exit code, without message boxes.
        /// </summary>
        public bool IsQuiet { get; init; }

        /// <summary>
        /// Gets a value indicating whether the scripting engine is refused, as when SAP GUI Scripting is disabled.
        /// </summary>
        public bool IsScriptingDisabled { get; init; }

        /// <summary>
        /// Gets the number of sessions opened on the connection, from 1 to <see cref="MaximumSessions"/>.
        /// </summary>
        public int Sessions { get; init; } = 1;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Converts command-line arguments to options.
        /// </summary>
        /// <param name="arguments">The arguments, such as <c>--sessions 2 --quiet</c>.</param>
        /// <returns>The options; defaults for every argument that is not given.</returns>
        /// <exception cref="ArgumentException">An argument is unknown, or the session count is missing or out of range.</exception>
        public static SimulatorOptions ConvertFromArguments(string[] arguments)
        {
            // Treat no arguments as the default single-session scenario.
            arguments ??= [];
            var isDisabledByServer = false;
            var isQuiet = false;
            var isScriptingDisabled = false;
            var sessions = 1;

            // Read switches and the one valued option in any order.
            for (var index = 0; index < arguments.Length; index++)
            {
                switch (arguments[index].ToLowerInvariant())
                {
                    case DisabledByServerSwitch:
                        isDisabledByServer = true;
                        break;
                    case QuietSwitch:
                        isQuiet = true;
                        break;
                    case ScriptingDisabledSwitch:
                        isScriptingDisabled = true;
                        break;
                    case SessionsOption:
                        index++;
                        sessions = ConvertToSessions(index < arguments.Length ? arguments[index] : null);
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument '{arguments[index]}'.", nameof(arguments));
                }
            }

            return new SimulatorOptions
            {
                IsDisabledByServer = isDisabledByServer,
                IsQuiet = isQuiet,
                IsScriptingDisabled = isScriptingDisabled,
                Sessions = sessions
            };
        }

        // Parses the session count, accepting 1 through MaximumSessions.
        private static int ConvertToSessions(string value)
        {
            // Reject a missing or non-numeric value and counts outside the supported range.
            var isNumber = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var sessions);
            if (!isNumber || sessions < 1 || sessions > MaximumSessions)
            {
                var message = $"{SessionsOption} takes a number from 1 to {MaximumSessions}.";
                throw new ArgumentException(message, nameof(value));
            }

            return sessions;
        }
        #endregion
    }
}
