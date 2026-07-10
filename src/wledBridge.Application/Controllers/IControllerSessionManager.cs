namespace wledBridge.Application.Controllers;

public interface IControllerSessionManager
{
    ControllerSession? Current { get; }

    event EventHandler? SessionChanged;

    ControllerSession Connect(string driverKey, string inputDeviceName, string outputDeviceName);

    void Disconnect();
}
