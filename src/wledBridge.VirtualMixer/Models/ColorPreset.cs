namespace wledBridge.VirtualMixer.Models;

public class ColorPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string ColorHex { get; set; }
    public int Order { get; set; }
    public bool IsFavorite { get; set; }
}
