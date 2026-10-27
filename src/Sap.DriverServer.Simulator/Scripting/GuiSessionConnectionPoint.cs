/*
 * RESOURCES
 * https://learn.microsoft.com/en-us/windows/win32/api/ocidl/nn-ocidl-iconnectionpoint
 */
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Exposes a session's <c>Change</c> event as a COM connection point.
    /// </summary>
    /// <remarks>
    /// Subscriptions are kept by the session's <see cref="SessionRecorder"/>, not by this object, because scripting
    /// objects are created on every access and do not live as long as a subscription. Enumerating the subscriptions is
    /// not supported.
    /// </remarks>
    /// <param name="session">The session whose recorder keeps the subscriptions.</param>
    /// <param name="container">The session object that returned this point.</param>
    [ComVisible(true)]
    public sealed class GuiSessionConnectionPoint(SimulatedSession session, IConnectionPointContainer container)
        : ApartmentBoundObject, IConnectionPoint
    {
        #region *** Constants    ***
        /// <summary>
        /// The simulator's identifier for the session event interface; SAP GUI's own identifier comes from its type library.
        /// </summary>
        public static readonly Guid EventInterfaceId = new("3E5D1C77-2B9A-4F0E-8D44-6A1B9C0E5F21");
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void Advise(object pUnkSink, out int pdwCookie)
        {
            pdwCookie = session.Recorder.Advise(pUnkSink);
        }

        /// <inheritdoc />
        public void EnumConnections(out IEnumConnections ppEnum)
        {
            throw new NotSupportedException("The simulator does not enumerate event subscriptions.");
        }

        /// <inheritdoc />
        public void GetConnectionInterface(out Guid pIID)
        {
            pIID = EventInterfaceId;
        }

        /// <inheritdoc />
        public void GetConnectionPointContainer(out IConnectionPointContainer ppCPC)
        {
            ppCPC = container;
        }

        /// <inheritdoc />
        public void Unadvise(int dwCookie)
        {
            session.Recorder.Unadvise(dwCookie);
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Enumerates the session's single connection point.
        /// </summary>
        /// <param name="point">The connection point.</param>
        [ComVisible(true)]
        public sealed class Enumerator(IConnectionPoint point) : ApartmentBoundObject, IEnumConnectionPoints
        {
            // Holds the position: 0 before the point, 1 after it.
            private int _position;

            /// <inheritdoc />
            public void Clone(out IEnumConnectionPoints ppenum)
            {
                ppenum = new Enumerator(point) { _position = _position };
            }

            /// <inheritdoc />
            public int Next(int celt, IConnectionPoint[] rgelt, IntPtr pceltFetched)
            {
                // Return the point once; afterwards report the end with S_FALSE.
                var fetched = celt > 0 && _position == 0 ? 1 : 0;
                if (fetched == 1)
                {
                    rgelt[0] = point;
                    _position = 1;
                }

                if (pceltFetched != IntPtr.Zero)
                {
                    Marshal.WriteInt32(pceltFetched, fetched);
                }

                return fetched == celt ? 0 : 1;
            }

            /// <inheritdoc />
            public void Reset()
            {
                _position = 0;
            }

            /// <inheritdoc />
            public int Skip(int celt)
            {
                // Move past the point when asked to skip at least one.
                var isSkipped = celt > 0 && _position == 0;
                _position = celt > 0 ? 1 : _position;

                return isSkipped && celt == 1 ? 0 : 1;
            }
        }
        #endregion
    }
}
