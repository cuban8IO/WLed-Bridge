using System.Text.Json.Serialization;

namespace wledBridge.Infrastructure.Wled;

internal sealed class WledStateDto
{
    [JsonPropertyName("on")]
    public bool On { get; set; }

    [JsonPropertyName("bri")]
    public byte Bri { get; set; }

    [JsonPropertyName("seg")]
    public List<WledSegmentDto>? Seg { get; set; }
}

internal sealed class WledSegmentDto
{
    [JsonPropertyName("col")]
    public List<List<byte>>? Col { get; set; }
}
