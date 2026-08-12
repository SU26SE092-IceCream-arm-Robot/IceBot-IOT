namespace IceBot.Machines
{
    public interface IMachineDiagnostics
    {
        string GetStatusText(string connectionName);
    }
}
