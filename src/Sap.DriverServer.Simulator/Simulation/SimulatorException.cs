using System;

namespace Sap.DriverServer.Simulator.Simulation
{
    /// <summary>
    /// Represents a refused scripting request, such as an unknown identifier or input to a blocked window.
    /// </summary>
    /// <remarks>
    /// The exception carries <c>E_FAIL</c> as its HRESULT, so a COM client receives a plain COM error with the message,
    /// as it does from SAP GUI, instead of an HRESULT that .NET clients map back to a .NET exception type.
    /// </remarks>
    public sealed class SimulatorException : InvalidOperationException
    {
        #region *** Constants    ***
        // Holds E_FAIL, the generic COM failure SAP GUI Scripting reports for refused requests.
        private const int GenericFailure = unchecked((int)0x80004005);
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="SimulatorException"/> class.
        /// </summary>
        public SimulatorException()
            : this(message: "The simulator refused the request.")
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimulatorException"/> class with a message.
        /// </summary>
        /// <param name="message">The message the COM client receives.</param>
        public SimulatorException(string message)
            : this(message, innerException: null)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimulatorException"/> class with a message and a cause.
        /// </summary>
        /// <param name="message">The message the COM client receives.</param>
        /// <param name="innerException">The cause, or null.</param>
        public SimulatorException(string message, Exception innerException)
            : base(message, innerException)
        {
            HResult = GenericFailure;
        }
        #endregion
    }
}
