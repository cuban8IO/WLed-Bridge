using wledBridge.Domain.Controllers;

namespace wledBridge.Application.Abstractions.Controllers;

public record ControlDescriptor(
    string ControlId,
    ControlType Type,
    string Label,
    int? Row,
    int? Column,
    bool SupportsColor,
    bool SupportsLedFeedback);
