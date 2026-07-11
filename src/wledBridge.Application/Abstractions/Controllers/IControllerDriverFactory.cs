namespace wledBridge.Application.Abstractions.Controllers;

public interface IControllerDriverFactory
{
    IReadOnlyList<string> AvailableDriverKeys { get; }

    IControllerDriver Create(string driverKey);

    IControllerDriver CreateGeneric(string displayName, IReadOnlyList<GenericControlDefinition> controls);
}
