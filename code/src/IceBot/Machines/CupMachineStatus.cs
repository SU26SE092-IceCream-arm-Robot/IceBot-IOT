namespace IceBot.Machines
{
    internal enum CupMachineState : byte
    {
        Standby = 0,
        Dispensing = 1,
        Fault = 2,
    }

    internal sealed class CupMachineStatus
    {
        public bool NoCup { get; set; }
        public bool CupNotTaken { get; set; }
        public bool DrawerOpen { get; set; }
        public bool MotorFault { get; set; }
        public bool ArmInPlace { get; set; }
        public CupMachineState SystemState { get; set; }

        public bool HasFault => NoCup || CupNotTaken || DrawerOpen || MotorFault || SystemState == CupMachineState.Fault;

        public override string ToString() =>
            $"NoCup={NoCup}, CupNotTaken={CupNotTaken}, DrawerOpen={DrawerOpen}, MotorFault={MotorFault}, ArmInPlace={ArmInPlace}, State={SystemState}";
    }
}
