using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sap.DriverServer.Converters
{
    /// <summary>
    /// Writes <see cref="Type"/> values as <c>{"Type":"full name"}</c>, ported from the UIA driver server.
    /// </summary>
    public class TypeConverter : JsonConverter<Type>
    {
        #region *** Methods      ***
        /// <inheritdoc />
        public override Type Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Types are never read from client payloads.
            throw new NotSupportedException("Deserializing System.Type values is not supported.");
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options)
        {
            // Write the full type name only, avoiding reflection-heavy default serialization.
            writer.WriteStartObject();
            writer.WriteString(nameof(Type), value.FullName);
            writer.WriteEndObject();
        }
        #endregion
    }
}
