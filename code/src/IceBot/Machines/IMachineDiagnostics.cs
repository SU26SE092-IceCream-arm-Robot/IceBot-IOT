namespace IceBot.Machines
{
    // Optional extra for a machine module that can report a human-readable status
    // (e.g. a query command in its protocol). Implement alongside IMachineModule to make
    // the module's status show up in the console test menu; not every machine has this.
    internal interface IMachineDiagnostics
    {
        string GetStatusText(string comPort);
    }
}
