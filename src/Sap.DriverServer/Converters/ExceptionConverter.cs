using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sap.DriverServer.Converters
{
    /// <summary>
    /// Writes <see cref="Exception"/> values as a flat diagnostic object, ported from the UIA driver server.
    /// </summary>
    public class ExceptionConverter : JsonConverter<Exception>
    {
        #region *** Methods      ***
        /// <inheritdoc />
        public override Exception Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Exceptions are never read from client payloads.
            throw new NotSupportedException("Deserializing exceptions is not supported.");
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, Exception value, JsonSerializerOptions options)
        {
            // Write the diagnostic members without the recursive default object graph.
            writer.WriteStartObject();
            writer.WriteString(nameof(value.Message), value.Message);
            writer.WriteString(nameof(value.StackTrace), value.StackTrace);
            writer.WriteString(nameof(value.HelpLink), value.HelpLink);
            writer.WriteNumber(nameof(value.HResult), value.HResult);
            writer.WriteString(nameof(value.Source), value.Source);
            writer.WriteString(nameof(value.TargetSite), $"{value.TargetSite?.DeclaringType?.FullName}.{value.TargetSite?.Name}");
            writer.WriteEndObject();
        }
        #endregion
    }
}
