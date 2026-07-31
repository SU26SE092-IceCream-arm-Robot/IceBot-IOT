namespace IceBot.Machines
{
    // Optional extra for a machine that is wired directly to this PC over RS485 and needs its
    // own signal/command sent after its step's .lua file finishes running on the arm (arm is
    // already in position by then). Every peripheral machine triggers this way — there is no
    // DO/Fairino-control-box trigger path. A machine that is purely arm motion with no separate
    // physical hardware to control (e.g. a tray-placement position) implements only
    // IMachineModule and skips this.
    internal interface IMachineTrigger : IMachineModule
    {
        void Trigger(string comPort);

        // Opens comPort and confirms the device actually responds (throws if not) — the one
        // capability every RS485 machine can be checked with, regardless of its own protocol.
        // Used by the reusable "test connection" feature (Cli/ConsoleMenu.cs) so a newly added
        // machine gets connection-checking for free, no menu code to write.
        void TestConnection(string comPort);
    }
}
