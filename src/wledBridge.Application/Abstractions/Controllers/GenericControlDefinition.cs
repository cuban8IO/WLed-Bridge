using wledBridge.Domain.Controllers;

namespace wledBridge.Application.Abstractions.Controllers;

public record GenericControlDefinition(
    string ControlId,
    string Label,
    ControlType Type,
    LedCapability LedCapability,
    bool IsRelative,
    int Channel,
    int CommandCode,
    int DataNumber);
