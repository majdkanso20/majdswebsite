using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MajdsApp.SharedKernel.Audit;

/// <summary>
/// Keeps secrets out of the audit trail (FR-AUDIT-004): any field whose name looks like a credential is
/// replaced with a placeholder, binary payloads are never written, and long values are truncated.
/// </summary>
public static partial class AuditRedactor
{
    public const string Placeholder = "[redacted]";
    private const int MaxValueLength = 500;
    private const int MaxParametersLength = 4000;

    [GeneratedRegex("password|passwd|secret|token|hash|stamp|credential|otp|recoverycode|apikey|privatekey|^code$|^pin$", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveName();

    public static bool IsSensitive(string propertyName) => SensitiveName().IsMatch(propertyName);

    /// <summary>Redacts a stored value: sensitive property names, and anything that is already an encrypted setting.</summary>
    public static string? Redact(string propertyName, string? value)
    {
        if (value is null) return null;
        if (IsSensitive(propertyName) || value.StartsWith("enc:", StringComparison.Ordinal)) return Placeholder;
        return value.Length > MaxValueLength ? value[..MaxValueLength] + "…" : value;
    }

    /// <summary>JSON of a request's parameters with sensitive fields redacted, for the audit record.</summary>
    public static string? SerializeParameters(object request)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new BinaryPlaceholderConverter() }
            };

            var node = JsonSerializer.SerializeToNode(request, request.GetType(), options);
            RedactNode(node);
            var json = node?.ToJsonString();
            return json is { Length: > MaxParametersLength } ? json[..MaxParametersLength] + "…" : json;
        }
        catch (Exception)
        {
            return "[unserializable]";
        }
    }

    private static void RedactNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (IsSensitive(key) && obj[key] is not null) obj[key] = Placeholder;
                    else RedactNode(obj[key]);
                }
                break;
            case JsonArray array:
                foreach (var item in array) RedactNode(item);
                break;
        }
    }

    private sealed class BinaryPlaceholderConverter : JsonConverter<Stream>
    {
        public override Stream Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, Stream value, JsonSerializerOptions options) =>
            writer.WriteStringValue("[binary]");
    }
}
