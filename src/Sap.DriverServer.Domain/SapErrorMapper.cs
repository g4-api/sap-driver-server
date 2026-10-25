/*
 * RESOURCES
 * https://learn.microsoft.com/en-us/windows/win32/com/com-error-codes-1
 * https://learn.microsoft.com/en-us/windows/win32/rpc/rpc-return-values
 */
using System;
using System.Reflection;
using System.Runtime.InteropServices;

using Microsoft.AspNetCore.Http;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Converts COM and dispatcher failures into classified <see cref="SapDriverException"/> instances.
    /// </summary>
    /// <remarks>
    /// The mapping is pure (no COM calls), so it runs and is tested on any platform. Client-facing messages carry
    /// only the HRESULT, never the COM error text, because SAP error texts can quote screen or field contents.
    /// </remarks>
    public static class SapErrorMapper
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies <c>CO_E_OBJNOTCONNECTED</c>: the COM object's server disconnected.
        /// </summary>
        public const int ObjectNotConnected = unchecked((int)0x800401FD);

        /// <summary>
        /// Specifies <c>DISP_E_BADPARAMCOUNT</c>: the method received the wrong number of arguments.
        /// </summary>
        public const int BadParameterCount = unchecked((int)0x8002000E);

        /// <summary>
        /// Specifies <c>DISP_E_MEMBERNOTFOUND</c>: the object does not support the member for this access kind.
        /// </summary>
        public const int MemberNotFound = unchecked((int)0x80020003);

        /// <summary>
        /// Specifies <c>DISP_E_TYPEMISMATCH</c>: an argument had an incompatible type.
        /// </summary>
        public const int TypeMismatch = unchecked((int)0x80020005);

        /// <summary>
        /// Specifies <c>DISP_E_UNKNOWNNAME</c>: the object does not expose a member with the requested name.
        /// </summary>
        public const int UnknownName = unchecked((int)0x80020006);

        /// <summary>
        /// Specifies <c>E_ACCESSDENIED</c>: Windows denied access to the COM server.
        /// </summary>
        public const int AccessDenied = unchecked((int)0x80070005);

        /// <summary>
        /// Specifies <c>MK_E_UNAVAILABLE</c>: the running object table has no entry for the requested moniker.
        /// </summary>
        public const int MonikerUnavailable = unchecked((int)0x800401E3);

        /// <summary>
        /// Specifies <c>RPC_E_CALL_REJECTED</c>: the COM server rejected the call because it is busy.
        /// </summary>
        public const int CallRejected = unchecked((int)0x80010001);

        /// <summary>
        /// Specifies <c>RPC_E_DISCONNECTED</c>: the COM server disconnected from its clients.
        /// </summary>
        public const int Disconnected = unchecked((int)0x80010108);

        /// <summary>
        /// Specifies <c>RPC_E_SERVERCALL_RETRYLATER</c>: the COM server asked the caller to retry later.
        /// </summary>
        public const int RetryLater = unchecked((int)0x8001010A);

        /// <summary>
        /// Specifies <c>RPC_S_CALL_FAILED</c>: the remote procedure call failed, usually because the server exited.
        /// </summary>
        public const int CallFailed = unchecked((int)0x800706BE);

        /// <summary>
        /// Specifies <c>RPC_S_SERVER_UNAVAILABLE</c>: the COM server process is no longer available.
        /// </summary>
        public const int ServerUnavailable = unchecked((int)0x800706BA);
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Converts a failure raised during a SAP GUI Scripting operation into a classified driver exception.
        /// </summary>
        /// <param name="exception">The failure to convert.</param>
        /// <returns>
        /// The classified exception, the same instance when it is already a <see cref="SapDriverException"/>,
        /// or null when the failure is not a SAP or COM failure and represents a server fault.
        /// </returns>
        public static SapDriverException ConvertToDriverException(Exception exception)
        {
            // Keep failures that were already classified close to their cause.
            if (exception is SapDriverException driverException)
            {
                return driverException;
            }

            // Report dispatcher timeouts with the W3C timeout code.
            if (exception is TimeoutException timeoutException)
            {
                return new SapDriverException(
                    error: WebDriverErrorCodes.Timeout,
                    errorKind: SapErrorKinds.Timeout,
                    statusCode: StatusCodes.Status500InternalServerError,
                    message: timeoutException.Message,
                    data: null,
                    innerException: timeoutException);
            }

            // Leave non-COM failures to the generic server-fault path, because they are not SAP conditions.
            var comException = FindComException(exception);
            if (comException is null)
            {
                return null;
            }

            // Classify the HRESULT; the message names only the code so no SAP text reaches the client.
            var (error, errorKind, statusCode) = ConvertHResult(comException.HResult);
            var message = $"SAP GUI Scripting call failed ({errorKind}, HRESULT 0x{comException.HResult:X8}).";

            return new SapDriverException(error, errorKind, statusCode, message, data: null, innerException: exception);
        }

        /// <summary>
        /// Classifies an HRESULT into a W3C error code, SAP error kind, and HTTP status code.
        /// </summary>
        /// <param name="hresult">The COM HRESULT.</param>
        /// <returns>The classification tuple.</returns>
        public static (string Error, string ErrorKind, int StatusCode) ConvertHResult(int hresult)
        {
            // Map the HRESULT families to the driver's error kinds; unknown codes fall back to a COM failure.
            return hresult switch
            {
                AccessDenied => (WebDriverErrorCodes.UnknownError, SapErrorKinds.AccessDenied, StatusCodes.Status500InternalServerError),
                CallRejected or RetryLater => (WebDriverErrorCodes.UnknownError, SapErrorKinds.SessionBusy, StatusCodes.Status500InternalServerError),
                CallFailed or Disconnected or ObjectNotConnected or ServerUnavailable or MonikerUnavailable => (WebDriverErrorCodes.UnknownError, SapErrorKinds.SapGuiNotRunning, StatusCodes.Status500InternalServerError),
                MemberNotFound or UnknownName => (WebDriverErrorCodes.UnsupportedOperation, SapErrorKinds.NotExposedByScripting, StatusCodes.Status500InternalServerError),
                BadParameterCount or TypeMismatch => (WebDriverErrorCodes.InvalidArgument, SapErrorKinds.ComFailure, StatusCodes.Status400BadRequest),
                _ => (WebDriverErrorCodes.UnknownError, SapErrorKinds.ComFailure, StatusCodes.Status500InternalServerError)
            };
        }

        /// <summary>
        /// Finds the first <see cref="COMException"/> in an exception chain.
        /// </summary>
        /// <param name="exception">The exception to inspect, including inner and invocation-wrapped exceptions.</param>
        /// <returns>The COM exception, or null when the chain contains none.</returns>
        public static COMException FindComException(Exception exception)
        {
            // Walk the inner-exception chain because late-bound calls wrap COM failures in TargetInvocationException.
            var current = exception;

            while (current is not null)
            {
                if (current is COMException comException)
                {
                    return comException;
                }

                current = current is TargetInvocationException { InnerException: not null } invocationException
                    ? invocationException.InnerException
                    : current.InnerException;
            }

            return null;
        }

        /// <summary>
        /// Tests whether an HRESULT means the scripting object does not expose the requested member.
        /// </summary>
        /// <param name="hresult">The COM HRESULT.</param>
        /// <returns>True for <c>DISP_E_UNKNOWNNAME</c> and <c>DISP_E_MEMBERNOTFOUND</c>; otherwise false.</returns>
        public static bool TestMemberNotExposed(int hresult)
        {
            // Both dispatch codes mean the member is absent, either by name or for the requested access kind.
            return hresult is UnknownName or MemberNotFound;
        }
        #endregion
    }
}
