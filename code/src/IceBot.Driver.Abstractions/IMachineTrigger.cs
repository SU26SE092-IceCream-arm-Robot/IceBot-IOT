namespace IceBot.Machines
{
    /// <summary>Machine driver capable of connection testing and physical activation.</summary>
    public interface IMachineTrigger : IMachineModule
    {
        void Trigger(string connectionName, string command);
        void TestConnection(string connectionName);
    }
}
