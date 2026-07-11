using wledBridge.Application.Abstractions.Controllers;

namespace wledBridge.Infrastructure.Controllers;

public class ControllerDriverFactory : IControllerDriverFactory
{
    private static readonly Dictionary<string, Func<IControllerDriver>> Drivers = new()
    {
        [Apc40Mk2Driver.Key] = () => new Apc40Mk2Driver()
    };

    public IReadOnlyList<string> AvailableDriverKeys => Drivers.Keys.ToList();

    public IControllerDriver Create(string driverKey) =>
        Drivers.TryGetValue(driverKey, out var factory)
            ? factory()
            : throw new InvalidOperationException($"No controller driver registered for key '{driverKey}'.");

    public IControllerDriver CreateGeneric(string displayName, IReadOnlyList<GenericControlDefinition> controls) =>
        new GenericMidiControllerDriver(displayName, controls);
}
