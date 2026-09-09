using System;
using System.Collections.Generic;
using System.Threading;
using IceBot.Machines;

namespace IceBot.Driver.IceCream
{
    public sealed class IceCreamDriver : IMachineTrigger, IMachineDiagnostics
    {
        internal const byte CycleSpeedPercent = 20;
        internal const byte UpDurationTenths = 30;
        internal const byte DownDurationTenths = 10;
        private const int LimitPollIntervalMilliseconds = 100;
        private const int LimitWaitTimeoutMilliseconds = 30000;

        private readonly Func<string, IIceCreamMachineClient> _clientFactory;
        private readonly Action<int> _delay;

        public IceCreamDriver()
            : this(port => new IceCreamMachineClient(port), Thread.Sleep)
        {
        }

        internal IceCreamDriver(Func<string, IIceCreamMachineClient> clientFactory, Action<int> delay)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _delay = delay ?? throw new ArgumentNullException(nameof(delay));
        }

        public string MachineType => "ice_cream";
        public string DisplayName => "May lam kem";
        public IReadOnlyCollection<string> StepNames { get; } = new[] { "ice_cream" };

        public void Trigger(string comPort, string command)
        {
            using (var client = _clientFactory(comPort))
            {
                client.Connect();
                var status = client.QueryStatus();
                if (status.HasFault)
                    throw new InvalidOperationException($"May lam kem bao loi: {status}");
                if (!status.IsStandby)
                    throw new InvalidOperationException($"May lam kem dang ban, khong the bat dau chu ky: {status}");

                try
                {
                    if (string.Equals(command, "UP", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[MACHINE] {DisplayName} @ {comPort}: UP {CycleSpeedPercent}% until upper limit...");
                        RunUntilLimit(client, true);
                    }
                    else if (string.Equals(command, "DOWN", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[MACHINE] {DisplayName} @ {comPort}: DOWN {CycleSpeedPercent}% until lower limit...");
                        RunUntilLimit(client, false);
                    }
                    else
                    {
                        throw new InvalidOperationException("May lam kem chi ho tro lenh UP hoac DOWN.");
                    }

                    var finalStatus = client.QueryStatus();
                    if (finalStatus.HasFault || !finalStatus.IsStandby)
                        throw new InvalidOperationException($"May lam kem khong ve Standby sau chu ky: {finalStatus}");
                    Console.WriteLine("[MACHINE] Ice-cream cycle completed.");
                }
                catch
                {
                    try { client.Stop(); } catch { }
                    throw;
                }
            }
        }

        public void TestConnection(string comPort)
        {
            using (var client = _clientFactory(comPort))
            {
                client.Connect();
                client.QueryStatus();
            }
        }

        public string GetStatusText(string comPort)
        {
            using (var client = _clientFactory(comPort))
            {
                client.Connect();
                return client.QueryStatus().ToString();
            }
        }

        private void RunUntilLimit(IIceCreamMachineClient client, bool up)
        {
            var operation = up ? "UP" : "DOWN";
            var accepted = up
                ? client.RunUp(CycleSpeedPercent, 0)
                : client.RunDown(CycleSpeedPercent, 0);
            RequireAccepted(accepted, operation);

            for (var elapsed = 0; elapsed < LimitWaitTimeoutMilliseconds; elapsed += LimitPollIntervalMilliseconds)
            {
                var status = client.QueryStatus();
                if (status.HasFault)
                    throw new InvalidOperationException($"May lam kem bao loi trong khi chay {operation}: {status}");
                if (status.IsStandby)
                    return;
                _delay(LimitPollIntervalMilliseconds);
            }

            try { client.Stop(); } catch { }
            throw new InvalidOperationException($"May lam kem khong cham cong tac gioi han trong {LimitWaitTimeoutMilliseconds / 1000}s khi chay {operation}.");
        }

        private static void RequireAccepted(bool accepted, string operation)
        {
            if (!accepted) throw new InvalidOperationException($"May lam kem tu choi lenh {operation}.");
        }
    }
}