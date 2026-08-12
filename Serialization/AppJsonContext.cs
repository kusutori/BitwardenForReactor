using BitwardenForReactor.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BitwardenForReactor.Serialization;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    WriteIndented = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
