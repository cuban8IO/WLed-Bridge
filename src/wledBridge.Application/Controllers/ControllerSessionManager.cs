using wledBridge.Application.Abstractions.Controllers;
using wledBridge.Application.Abstractions.Midi;

namespace wledBridge.Application.Controllers;

public class ControllerSessionManager(IControllerDriverFactory driverFactory, IMidiPortFactory portFactory) : IControllerSessionManager
{
    public ControllerSession? Current { get; private set; }

    public event EventHandler? SessionChanged;

    public ControllerSession Connect(string driverKey, string inputDeviceName, string outputDeviceName) =>
        ConnectWithDriver(driverFactory.Create(driverKey), inputDeviceName, outputDeviceName);

    public ControllerSession ConnectGeneric(string displayName, IReadOnlyList<GenericControlDefinition> controls, string inputDeviceName, string outputDeviceName) =>
        ConnectWithDriver(driverFactory.CreateGeneric(displayName, controls), inputDeviceName, outputDeviceName);

    private ControllerSession ConnectWithDriver(IControllerDriver driver, string inputDeviceName, string outputDeviceName)
    {
        Disconnect();

        var input = portFactory.OpenInput(inputDeviceName);
        var output = portFactory.OpenOutput(outputDeviceName);

        var session = new ControllerSession(driver, input, output);
        session.Start();

        Current = session;
        SessionChanged?.Invoke(this, EventArgs.Empty);

        return session;
    }

    public void Disconnect()
    {
        if (Current is null)
        {
            return;
        }

        Current.Dispose();
        Current = null;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}
