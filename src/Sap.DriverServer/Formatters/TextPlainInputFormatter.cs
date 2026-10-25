using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.Formatters;

namespace Sap.DriverServer.Formatters
{
    /// <summary>
    /// Reads <c>text/plain</c> request bodies as strings, ported from the UIA driver server for client parity.
    /// </summary>
    public class TextPlainInputFormatter : InputFormatter
    {
        #region *** Constants    ***
        // Names the media type this formatter accepts.
        private const string ContentType = MediaTypeNames.Text.Plain;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="TextPlainInputFormatter"/> class.
        /// </summary>
        public TextPlainInputFormatter()
        {
            // Register text/plain so MVC selects this formatter for plain-text bodies.
            SupportedMediaTypes.Add(ContentType);
        }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public override bool CanRead(InputFormatterContext context)
        {
            // Accept any text/plain variant, including a charset parameter.
            return context.HttpContext.Request.ContentType?.StartsWith(ContentType) == true;
        }

        /// <inheritdoc />
        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
        {
            // Read the whole body as the bound string value.
            using var streamReader = new StreamReader(context.HttpContext.Request.Body);
            var requestBody = await streamReader.ReadToEndAsync().ConfigureAwait(false);

            return await InputFormatterResult.SuccessAsync(requestBody).ConfigureAwait(false);
        }
        #endregion
    }
}
