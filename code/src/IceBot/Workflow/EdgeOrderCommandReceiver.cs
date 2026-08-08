using System;
using System.Threading;
using IceBot.Api;
using IceBot.Config;

namespace IceBot.Workflow
{
    internal sealed class EdgeOrderCommandReceiver : IDisposable
    {
        private readonly ManualResetEvent _stop = new ManualResetEvent(false);
        private Thread? _worker;

        public void Start()
        {
            if (_worker != null) return;
            if (string.IsNullOrWhiteSpace(AppConfig.BeApiUrl) || AppConfig.ExecutionEndpointId == Guid.Empty ||
                string.IsNullOrWhiteSpace(AppConfig.ExecutionClientCertificatePath))
            {
                Console.WriteLine("[ORDER-PULL] Chua du cau hinh BE private URL / execution endpoint / client certificate; tam chua nhan order tu BE.");
                return;
            }

            _worker = new Thread(PollLoop) { IsBackground = true, Name = "IceBot-EdgeOrderReceiver" };
            _worker.Start();
            Console.WriteLine("[ORDER-PULL] Da bat dau nhan ExecuteOrder tu BE.");
        }

        private void PollLoop()
        {
            while (!_stop.WaitOne(0))
            {
                try { PullOnce(); }
                catch (Exception ex) { Console.WriteLine("[ORDER-PULL] " + ex.Message); }
                _stop.WaitOne(TimeSpan.FromSeconds(5));
            }
        }

        internal static int PullOnce()
        {
            var pull = EdgeDeploymentApi.PullCommands(20);
            var received = 0;
            foreach (var command in pull.Commands)
            {
                if (!string.Equals(command.CommandType, "ExecuteOrder", StringComparison.OrdinalIgnoreCase)) continue;
                var order = EdgeOrderInbox.Validate(command.CommandId, command.PayloadJson);
                var isNew = EdgeOrderInbox.TryStore(order, AppConfig.GetOrderInboxDirectory());
                EdgeDeploymentApi.AcknowledgeReceived(command.CommandId);
                if (!isNew) continue;
                received++;
                Console.WriteLine($"[ORDER-PULL] Da nhan don {order.OrderNumber} ({order.OrderId:D}), command {order.CommandId:D}; da luu vao inbox.");
            }
            return received;
        }

        public void Dispose()
        {
            _stop.Set();
            _worker?.Join(TimeSpan.FromSeconds(35));
            _stop.Dispose();
        }
    }
}
