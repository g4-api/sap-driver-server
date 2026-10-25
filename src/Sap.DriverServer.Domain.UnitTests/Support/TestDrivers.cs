using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Sap.DriverServer.Domain;
using Sap.DriverServer.Inspector.Services;

namespace Sap.DriverServer.Domain.UnitTests.Support
{
    /// <summary>
    /// Creates in-process SAP driver servers over fake SAP GUI trees, and inspector clients that talk to them.
    /// </summary>
    /// <remarks>
    /// The server is the production ASP.NET Core pipeline; only the scripting host is replaced by one over a fake engine.
    /// Callers dispose the factory, which stops the server and its dispatcher thread.
    /// </remarks>
    public static class TestDrivers
    {
        #region *** Methods      ***
        /// <summary>
        /// Creates an inspector driver client over the in-process server.
        /// </summary>
        /// <param name="factory">The server factory.</param>
        /// <returns>The client; it owns and disposes its HTTP client.</returns>
        public static SapDriverClient NewClient(WebApplicationFactory<Program> factory)
        {
            // The test client's base address is http://localhost/, which the driver's "/" routes serve.
            return new SapDriverClient(factory.CreateClient(), isOwner: true);
        }

        /// <summary>
        /// Creates an in-process driver server whose scripting host runs over a fake tree.
        /// </summary>
        /// <param name="application">The fake application node.</param>
        /// <returns>The server factory; the caller disposes it.</returns>
        public static WebApplicationFactory<Program> NewFactory(FakeSapGuiNode application)
        {
            // Replace only the scripting host and the settings; everything else is the production pipeline.
            var engine = new FakeSapGuiEngine(application);

            return new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISapScriptingHost>();
                services.AddSingleton<ISapScriptingHost>(_ => TestHosts.NewHost(engine));
                services.AddSingleton(TestHosts.NewSettings());
            }));
        }
        #endregion
    }
}
