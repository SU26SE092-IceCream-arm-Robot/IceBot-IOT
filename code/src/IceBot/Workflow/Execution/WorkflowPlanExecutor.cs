using System;

namespace IceBot.Workflow.Execution
{
    internal interface IWorkflowRuntime
    {
        void MoveJ(WorkflowInstruction instruction);
        void MoveL(WorkflowInstruction instruction);
        void SetDo(int index, int state, bool toolOutput);
        void Wait(int milliseconds);
        void TriggerDevice(string machineType, string command);
    }

    internal static class WorkflowPlanExecutor
    {
        public static void Execute(WorkflowPlan plan, IWorkflowRuntime runtime)
        {
            for (var index = 0; index < plan.Instructions.Count; index++)
            {
                var instruction = plan.Instructions[index];
                Console.WriteLine($"[WORKFLOW] Lenh {index + 1}/{plan.Instructions.Count}: {instruction.Kind} ({instruction.SourceFile}:{instruction.SourceLine})");
                switch (instruction.Kind)
                {
                    case WorkflowInstructionKind.MoveJ: runtime.MoveJ(instruction); break;
                    case WorkflowInstructionKind.MoveL: runtime.MoveL(instruction); break;
                    case WorkflowInstructionKind.SetDo: runtime.SetDo(instruction.Index, instruction.State, false); break;
                    case WorkflowInstructionKind.SetToolDo: runtime.SetDo(instruction.Index, instruction.State, true); break;
                    case WorkflowInstructionKind.Wait: runtime.Wait(instruction.DelayMilliseconds); break;
                    case WorkflowInstructionKind.TriggerDevice: runtime.TriggerDevice(instruction.MachineType, instruction.Command); break;
                    default: throw new InvalidOperationException($"Loai workflow instruction khong duoc ho tro: {instruction.Kind}");
                }
            }
        }
    }
}
