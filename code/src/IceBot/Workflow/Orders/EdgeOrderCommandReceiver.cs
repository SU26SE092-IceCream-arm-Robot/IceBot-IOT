using System;
using System.Threading;
using IceBot.Api;
using IceBot.Config;

namespace IceBot.Workflow
{
    internal sealed class EdgeOrderCommandReceiver : IDisposable
    {
        private readonly ManualResetEvent _stop = new ManualResetEvent(false);
        private readonly EdgeOrderExecutionWorker _executor = new EdgeOrderExecutionWorker();
        private Thread? _worker;

        public bool IsRunning => _worker != null && _worker.IsAlive;

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
            _executor.Start();
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
                if (string.Equals(command.CommandType, "DeployConfiguration", StringComparison.OrdinalIgnoreCase))
                {
                    HandleDeployment(command);
                    continue;
                }

                if (!string.Equals(command.CommandType, "ExecuteOrder", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var order = EdgeOrderInbox.Validate(command.CommandId, command.PayloadJson);
                    if (EdgeOrderExecutionQueue.Contains(command.CommandId, AppConfig.GetOrderJobsDirectory()))
                    {
                        EdgeDeploymentApi.AcknowledgeAccepted(command.CommandId);
                        EdgeOrderExecutionQueue.Activate(command.CommandId, AppConfig.GetOrderJobsDirectory());
                        continue;
                    }
                    var settings = SiteConfigStore.Load();
                    EdgeOrderInbox.ValidateForThisEdge(order, settings.KioskId, settings.ExecutionEndpointId,
                        settings.ActiveConfigurationReleaseId, settings.ActiveConfigurationReleaseChecksum,
                        AppConfig.GetWorkflowDirectory(), DateTimeOffset.UtcNow);
                    EdgeOrderInbox.TryStore(order, AppConfig.GetOrderInboxDirectory());
                    var admission = EdgeOrderExecutionQueue.TryAdmit(order, AppConfig.GetOrderJobsDirectory());
                    if (admission == OrderAdmissionResult.Busy)
                    {
                        EdgeDeploymentApi.AcknowledgeExecutorBusy(command.CommandId);
                        Console.WriteLine($"[ORDER-PULL] Tu choi tam thoi {order.OrderNumber}: kiosk dang xu ly mot phien khach hang.");
                        continue;
                    }

                    EdgeDeploymentApi.AcknowledgeAccepted(command.CommandId);
                    EdgeOrderExecutionQueue.Activate(command.CommandId, AppConfig.GetOrderJobsDirectory());
                    if (admission == OrderAdmissionResult.Accepted) received++;
                    Console.WriteLine($"[ORDER-PULL] Da chap nhan don {order.OrderNumber} ({order.TotalQuantity} cay), command {order.CommandId:D}.");
                }
                catch (OrderRejectionException ex)
                {
                    EdgeDeploymentApi.AcknowledgeRejected(command.CommandId, ex.Code, ex.Message);
                    Console.WriteLine($"[ORDER-PULL] Tu choi command {command.CommandId:D}: {ex.Message}");
                }
                catch (FormatException ex)
                {
                    EdgeDeploymentApi.AcknowledgeRejected(command.CommandId, "InvalidPayload", ex.Message);
                    Console.WriteLine($"[ORDER-PULL] Payload khong hop le {command.CommandId:D}: {ex.Message}");
                }
            }
            return received;
        }

        private static void HandleDeployment(EdgeCommandData command)
        {
            if (EdgeOrderExecutionQueue.HasActiveOrUnresolvedWork(AppConfig.GetOrderJobsDirectory()))
            {
                EdgeDeploymentApi.AcknowledgeExecutorBusy(command.CommandId);
                Console.WriteLine($"[DEPLOYMENT] Tam hoan deployment {command.CommandId:D}: kiosk dang co phien san xuat dang xu ly.");
                return;
            }

            var result = FullEdgeConfigurationInstaller.Install(command);
            if (result.Success)
            {
                Console.WriteLine("[DEPLOYMENT] " + result.Message);
                return;
            }

            if (result.Retryable)
            {
                Console.WriteLine("[DEPLOYMENT] Chua the cai dat; se thu lai: " + result.Message);
                return;
            }

            EdgeDeploymentApi.AcknowledgeRejected(command.CommandId, "DeploymentInstallRejected", result.Message);
            Console.WriteLine("[DEPLOYMENT] Tu choi " + command.CommandId.ToString("D") + ": " + result.Message);
        }

        public void Dispose()
        {
            _stop.Set();
            _worker?.Join(TimeSpan.FromSeconds(35));
            _executor.Dispose();
            _stop.Dispose();
        }
    }
}
