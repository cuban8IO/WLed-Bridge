namespace wledBridge.Application.Abstractions.Midi;

public readonly record struct MidiMessage(int Status, int Data1, int Data2)
{
    public int Channel => Status & 0x0F;

    public int CommandCode => Status & 0xF0;

    public static MidiMessage NoteOn(int channel, int note, int velocity) =>
        new(0x90 | (channel & 0x0F), note, velocity);

    public static MidiMessage NoteOff(int channel, int note) =>
        new(0x80 | (channel & 0x0F), note, 0);

    public static MidiMessage ControlChange(int channel, int controller, int value) =>
        new(0xB0 | (channel & 0x0F), controller, value);
}
