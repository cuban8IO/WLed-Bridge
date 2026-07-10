namespace wledBridge.Domain.Midi;

public record MidiTrigger(int Channel, MidiTriggerType Type, int Number);
