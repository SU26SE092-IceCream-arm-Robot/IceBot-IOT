namespace IceBot.Machines
{
    // Optional extra for a machine that is wired directly to this PC over a serial port
    // (Path A) and needs its own signal/command sent after its step's .lua file finishes
    // running on the arm (arm is already in position by then). Machines that are purely
    // arm motion — or use the Fairino DO trigger (Path B) inside their .lua file — implement
    // only IMachineModule and skip this.
    internal interface IMachineTrigger : IMachineModule
    {
        void Trigger(string comPort);
    }
}
