using System;

namespace IceBot.Driver.IceCream
{
    internal interface IIceCreamMachineClient : IDisposable
    {
        void Connect();
        IceCreamMachineStatus QueryStatus();
        bool RunUp(byte speedPercent, byte durationTenths);
        bool RunDown(byte speedPercent, byte durationTenths);
        bool Stop();
    }
}