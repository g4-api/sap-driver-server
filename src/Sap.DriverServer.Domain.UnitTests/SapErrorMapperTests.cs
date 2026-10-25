using System;
using System.Reflection;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("SapErrorMapper")]
    [TestCategory("UnitTest")]
    public class SapErrorMapperTests
    {
        [TestMethod(DisplayName = "Verify that ConvertHResult classifies COM HRESULTs into W3C errors, SAP kinds, and status codes.")]
        #region *** Data Set ***
        [DataRow(unchecked((int)0x80070005), WebDriverErrorCodes.UnknownError, SapErrorKinds.AccessDenied, 500)]
        [DataRow(unchecked((int)0x80010001), WebDriverErrorCodes.UnknownError, SapErrorKinds.SessionBusy, 500)]
        [DataRow(unchecked((int)0x8001010A), WebDriverErrorCodes.UnknownError, SapErrorKinds.SessionBusy, 500)]
        [DataRow(unchecked((int)0x80010108), WebDriverErrorCodes.UnknownError, SapErrorKinds.SapGuiNotRunning, 500)]
        [DataRow(unchecked((int)0x800706BA), WebDriverErrorCodes.UnknownError, SapErrorKinds.SapGuiNotRunning, 500)]
        [DataRow(unchecked((int)0x800706BE), WebDriverErrorCodes.UnknownError, SapErrorKinds.SapGuiNotRunning, 500)]
        [DataRow(unchecked((int)0x800401FD), WebDriverErrorCodes.UnknownError, SapErrorKinds.SapGuiNotRunning, 500)]
        [DataRow(unchecked((int)0x800401E3), WebDriverErrorCodes.UnknownError, SapErrorKinds.SapGuiNotRunning, 500)]
        [DataRow(unchecked((int)0x80040154), WebDriverErrorCodes.UnknownError, SapErrorKinds.ComFailure, 500)]
        [DataRow(unchecked((int)0x80020006), WebDriverErrorCodes.UnsupportedOperation, SapErrorKinds.NotExposedByScripting, 500)]
        [DataRow(unchecked((int)0x80020003), WebDriverErrorCodes.UnsupportedOperation, SapErrorKinds.NotExposedByScripting, 500)]
        [DataRow(unchecked((int)0x80020005), WebDriverErrorCodes.InvalidArgument, SapErrorKinds.ComFailure, 400)]
        [DataRow(unchecked((int)0x8002000E), WebDriverErrorCodes.InvalidArgument, SapErrorKinds.ComFailure, 400)]
        [DataRow(unchecked((int)0x80004005), WebDriverErrorCodes.UnknownError, SapErrorKinds.ComFailure, 500)]
        #endregion
        public void ConvertHResultTest(int hresult, string expectedError, string expectedKind, int expectedStatus)
        {
            // Act: classify the HRESULT.
            var (error, errorKind, statusCode) = SapErrorMapper.ConvertHResult(hresult);

            // Assert: each member of the classification matches the table.
            Assert.AreEqual(expectedError, error);
            Assert.AreEqual(expectedKind, errorKind);
            Assert.AreEqual(expectedStatus, statusCode);
        }

        [TestMethod(DisplayName = "Verify that a COM failure wrapped by reflection is found and its message never reaches the client.")]
        public void ConvertWrappedComExceptionHidesMessageTest()
        {
            // Arrange: wrap a COM failure whose text quotes SAP screen data, as late binding does.
            var comException = new COMException("Field MATNR value 4711 is invalid", unchecked((int)0x80004005));
            var wrapped = new TargetInvocationException(comException);

            // Act: convert the wrapped failure.
            var driverException = SapErrorMapper.ConvertToDriverException(wrapped);

            // Assert: the failure is classified, names its HRESULT, and keeps the SAP text only in the inner exception.
            Assert.IsNotNull(driverException);
            Assert.AreEqual(SapErrorKinds.ComFailure, driverException.ErrorKind);
            Assert.Contains("0x80004005", driverException.Message);
            Assert.DoesNotContain("4711", driverException.Message);
            Assert.AreSame(wrapped, driverException.InnerException);
        }

        [TestMethod(DisplayName = "Verify that timeouts map to the W3C timeout error and non-COM failures map to null.")]
        public void ConvertTimeoutAndServerFaultTest()
        {
            // Act: convert a timeout and an ordinary server fault.
            var timeout = SapErrorMapper.ConvertToDriverException(new TimeoutException("late"));
            var fault = SapErrorMapper.ConvertToDriverException(new InvalidOperationException("bug"));

            // Assert: the timeout is classified and the server fault is left to the generic path.
            Assert.AreEqual(WebDriverErrorCodes.Timeout, timeout.Error);
            Assert.AreEqual(SapErrorKinds.Timeout, timeout.ErrorKind);
            Assert.IsNull(fault);
        }

        [TestMethod(DisplayName = "Verify that an already classified SapDriverException is returned unchanged.")]
        public void ConvertDriverExceptionUnchangedTest()
        {
            // Arrange: create a classified failure.
            var original = SapDriverException.NewNotExposed("not here");

            // Act: convert it.
            var converted = SapErrorMapper.ConvertToDriverException(original);

            // Assert: the same instance comes back.
            Assert.AreSame(original, converted);
        }
    }
}
