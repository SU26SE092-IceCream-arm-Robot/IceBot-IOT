namespace IceBot.Driver.IceCream
{
    internal enum IceCreamMachineState : byte
    {
        Standby = 0,
        Dispensing = 1,
        Fault = 2,
    }

    internal sealed class IceCreamMachineStatus
    {
        public bool LowStock { get; set; }
        public bool OutOfStock { get; set; }
        public bool MotorFault { get; set; }
        public bool Busy { get; set; }
        public IceCreamMachineState SystemState { get; set; }

        public bool HasFault => OutOfStock || MotorFault || SystemState == IceCreamMachineState.Fault;
        public bool IsStandby => !Busy && SystemState == IceCreamMachineState.Standby;

        public override string ToString() =>
            $"LowStock={LowStock}, OutOfStock={OutOfStock}, MotorFault={MotorFault}, Busy={Busy}, State={SystemState}";
    }
}