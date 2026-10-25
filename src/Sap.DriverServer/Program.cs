using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using Sap.DriverServer.Converters;
using Sap.DriverServer.Domain;
using Sap.DriverServer.Extensions;
using Sap.DriverServer.Formatters;
using Sap.DriverServer.Middlewares;
using Sap.DriverServer.Models;
using Sap.DriverServer.Scripting;

// Write the application logo to the console; the release pipeline replaces the version placeholder.
SapDriverUtilities.WriteSapAsciiLogo(version: "0000.00.00.0000");

// Initialize the web application builder.
var builder = WebApplication.CreateBuilder(args);

#region *** Url & Kestrel ***
// Listen on all interfaces on the --port argument, defaulting to 5556 so the server can run beside the UIA server (5555).
const int DefaultPort = 5556;
var port = GetPort(args, DefaultPort);
builder.WebHost.UseUrls($"http://+:{port}");
#endregion

#region *** Service       ***
// Use lowercase routes, matching the UIA server.
builder.Services.AddRouting(i => i.LowercaseUrls = true);

// Add controllers with the UIA server's JSON conventions and its W3C-shaped model validation response.
builder
    .Services
    .AddControllers(i => i.InputFormatters.Add(new TextPlainInputFormatter()))
    .AddJsonOptions(i =>
    {
        i.JsonSerializerOptions.Converters.Add(new TypeConverter());
        i.JsonSerializerOptions.Converters.Add(new ExceptionConverter());
        i.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        i.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        i.JsonSerializerOptions.WriteIndented = true;
    })
    .ConfigureApiBehaviorOptions(i => i.InvalidModelStateResponseFactory = ErrorHandlingMiddleware.NewBadRequestResult);

// Add the OpenAPI document and Swagger UI metadata.
builder.Services.AddSwaggerGen(i =>
{
    i.SwaggerDoc("v4", new OpenApiInfo { Title = "SapDriver Server", Version = "v4" });
    i.OrderActionsBy(a => a.HttpMethod);
    i.EnableAnnotations();
});

// Allow cross-origin requests from any origin, matching the UIA server.
builder
    .Services
    .AddCors(o => o.AddPolicy("CorsPolicy", policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
#endregion

#region *** Dependencies  ***
// Bind the SapDriver settings section and expose the bound values directly.
builder.Services.Configure<SapDriverSettings>(builder.Configuration.GetSection(SapDriverSettings.SectionName));
builder.Services.AddSingleton(provider => provider.GetRequiredService<IOptions<SapDriverSettings>>().Value);

// Share one session dictionary across all repositories.
builder.Services.AddSingleton<IDictionary<string, SapSessionModel>>(new ConcurrentDictionary<string, SapSessionModel>());

// Own one STA dispatcher and one scripting engine for the process lifetime; the host disposes both at shutdown.
builder.Services.AddSingleton<ISapScriptingHost>(provider => new SapScriptingHost(
    dispatcher: new StaComDispatcher(),
    engine: new ComSapGuiEngine(),
    settings: provider.GetRequiredService<SapDriverSettings>(),
    logger: provider.GetRequiredService<ILogger<SapScriptingHost>>()));

// Add the repositories and the domain aggregate.
builder.Services.AddTransient<IActionsRepository, ActionsRepository>();
builder.Services.AddTransient<IDocumentRepository, DocumentRepository>();
builder.Services.AddTransient<IElementsRepository, ElementsRepository>();
builder.Services.AddTransient<ISessionsRepository, SessionsRepository>();
builder.Services.AddTransient<ISapDomain, SapDomain>();

// Register the MVC JSON options so the middleware serializes errors exactly like the controllers.
builder.Services.AddSingleton(provider => provider.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions);
#endregion

#region *** Configuration ***
// Build the application.
var app = builder.Build();

// Add CORS and the OpenAPI document and UI.
app.UseCors("CorsPolicy");
app.UseSwagger();
app.UseSwaggerUI(i =>
{
    i.SwaggerEndpoint("/swagger/v4/swagger.json", "SapDriver Server v4");
    i.DisplayRequestDuration();
    i.EnableFilter();
    i.EnableTryItOutByDefault();
});

// Route before the error middleware so it can read the session route value and reject unknown sessions early.
app.UseRouting();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.MapControllers();
#endregion

// Run the application until shutdown.
await app.RunAsync();

// Reads the --port argument, falling back to the default when it is absent or not an integer.
static int GetPort(string[] arguments, int defaultPort)
{
    // Scan for "--port <value>" pairs.
    for (var i = 0; i < arguments.Length - 1; i++)
    {
        if (arguments[i] == "--port" && int.TryParse(arguments[i + 1], out var port))
        {
            return port;
        }
    }

    return defaultPort;
}
