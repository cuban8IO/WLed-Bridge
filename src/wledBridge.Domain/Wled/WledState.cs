namespace wledBridge.Domain.Wled;

public record WledState(bool On, byte Brightness, byte Red, byte Green, byte Blue);
