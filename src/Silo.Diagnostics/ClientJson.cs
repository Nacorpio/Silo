using System.Text.Json.Serialization;

namespace Silo.Diagnostics;

/// <summary>Source-generated JSON metadata for the few values the client writes, so that no reflection is needed.</summary>
[JsonSerializable(typeof(string))]
internal sealed partial class ClientJson : JsonSerializerContext;
