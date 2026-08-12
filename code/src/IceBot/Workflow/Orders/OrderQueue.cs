using System;
using System.Collections.Concurrent;
using System.Threading;
using IceBot.Config;

namespace IceBot.Workflow
{
    // Runs accepted orders on the robot one at a time, off the HTTP thread. There is only one
    // arm — two orders must never call WorkflowRunner.RunQueue concurrently — so orders are
    // handed to a single background worker instead of running inline in LocalApiServer.
    internal static class OrderQueue
    {
        private static readonly BlockingCollection<OrderRequest> Pending = new BlockingCollection<OrderRequest>();

        static OrderQueue()
        {
            var worker = new Thread(ProcessLoop)
            {
                IsBackground = true,
                Name = "IceBot-OrderQueue"
            };
            worker.Start();
        }

        public static void Enqueue(OrderRequest order)
        {
            Pending.Add(order);
        }

        private static void ProcessLoop()
        {
            foreach (var order in Pending.GetConsumingEnumerable())
            {
                Console.WriteLine();
                Console.WriteLine($"[ORDER] Bat dau chay don '{order.OrderId}' ({order.Steps.Count} buoc)");
                try
                {
                    WorkflowRunner.RunQueue(order.Steps, AppConfig.RobotIp);
                    Console.WriteLine($"[ORDER] Hoan tat don '{order.OrderId}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ORDER] Loi khi chay don '{order.OrderId}': {ex.Message}");
                }
            }
        }
    }
}
