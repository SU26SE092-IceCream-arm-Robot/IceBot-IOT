using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace IceBot.Workflow.Execution
{
    internal static class LuaWorkflowParser
    {
        private const int MaxExpandedInstructions = 10000;
        private static readonly Regex Motion = new Regex(
            @"^(MoveJ|MoveL)\s*\(\s*\{(?<values>[^}]*)\}\s*,\s*[^,]+\s*,\s*[^,]+\s*,\s*(?<speed>[^,]+)\s*,\s*(?<acc>[^,]+)\s*,.*\)\s*$",
            RegexOptions.Compiled);
        private static readonly Regex SetDo = new Regex(
            @"^(SetDO|SetToolDO)\s*\(\s*(?<index>\d+)\s*,\s*(?<state>[01])\s*,\s*[^,]+\s*(?:,\s*[^)]+)?\)\s*$",
            RegexOptions.Compiled);
        private static readonly Regex Wait = new Regex(@"^WaitMs\s*\(\s*(?<ms>\d+)\s*\)\s*$", RegexOptions.Compiled);
        private static readonly Regex Trigger = new Regex(
            "^TriggerDevice\\s*\\(\\s*\"(?<machine>[^\"]+)\"\\s*,\\s*\"(?<command>[^\"]+)\"\\s*\\)\\s*$",
            RegexOptions.Compiled);
        private static readonly Regex CycleLoop = new Regex(@"^for\s+loop_idx\s*=\s*1\s*,\s*(?<count>\d+)\s+do\s*$", RegexOptions.Compiled);

        public static IReadOnlyList<WorkflowInstruction> Parse(string lua, string sourceFile)
        {
            if (lua == null) throw new ArgumentNullException(nameof(lua));
            var result = new List<WorkflowInstruction>();
            var loops = new Stack<LoopFrame>();
            var inToDoubleHelper = false;
            var lines = lua.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            for (var index = 0; index < lines.Length; index++)
            {
                var lineNumber = index + 1;
                var line = lines[index].Trim();
                if (line.Length == 0 || line.StartsWith("--", StringComparison.Ordinal)) continue;

                if (inToDoubleHelper)
                {
                    if (line == "end") inToDoubleHelper = false;
                    else if (!line.StartsWith("return tonumber(tostring(v))", StringComparison.Ordinal))
                        Invalid(sourceFile, lineNumber, "Noi dung ham toDouble khong hop le.");
                    continue;
                }

                if (line.StartsWith("local function toDouble", StringComparison.Ordinal))
                {
                    inToDoubleHelper = true;
                    continue;
                }
                if (line.StartsWith("local ACTIVE_TOOL_NAME", StringComparison.Ordinal) ||
                    line.StartsWith("local USED_DEVICES", StringComparison.Ordinal))
                    continue;

                var loop = CycleLoop.Match(line);
                if (loop.Success)
                {
                    var count = ParseInt(loop.Groups["count"].Value, sourceFile, lineNumber);
                    if (count < 1 || count > 1000) Invalid(sourceFile, lineNumber, "So chu ky loop phai tu 1 den 1000.");
                    loops.Push(new LoopFrame(result.Count, count, lineNumber));
                    continue;
                }
                if (line.StartsWith("while", StringComparison.Ordinal) || line.StartsWith("local start_time", StringComparison.Ordinal))
                    Invalid(sourceFile, lineNumber, "Loop theo thoi gian khong duoc ho tro trong execution plan.");
                if (line == "end")
                {
                    if (loops.Count == 0) Invalid(sourceFile, lineNumber, "Lenh end khong co loop tuong ung.");
                    ExpandLoop(result, loops.Pop(), sourceFile, lineNumber);
                    continue;
                }

                var motion = Motion.Match(line);
                if (motion.Success)
                {
                    var values = motion.Groups["values"].Value.Split(',').Select(value => ParseNumber(value, sourceFile, lineNumber)).ToArray();
                    if (values.Length != 6) Invalid(sourceFile, lineNumber, "MoveJ/MoveL phai co dung 6 gia tri toa do.");
                    Add(result, new WorkflowInstruction
                    {
                        Kind = motion.Groups[1].Value == "MoveJ" ? WorkflowInstructionKind.MoveJ : WorkflowInstructionKind.MoveL,
                        SourceFile = sourceFile,
                        SourceLine = lineNumber,
                        Values = values,
                        Speed = ParseNumber(motion.Groups["speed"].Value, sourceFile, lineNumber),
                        Acceleration = ParseNumber(motion.Groups["acc"].Value, sourceFile, lineNumber)
                    }, sourceFile, lineNumber);
                    continue;
                }

                var setDo = SetDo.Match(line);
                if (setDo.Success)
                {
                    Add(result, new WorkflowInstruction
                    {
                        Kind = setDo.Groups[1].Value == "SetDO" ? WorkflowInstructionKind.SetDo : WorkflowInstructionKind.SetToolDo,
                        SourceFile = sourceFile,
                        SourceLine = lineNumber,
                        Index = ParseInt(setDo.Groups["index"].Value, sourceFile, lineNumber),
                        State = ParseInt(setDo.Groups["state"].Value, sourceFile, lineNumber)
                    }, sourceFile, lineNumber);
                    continue;
                }

                var wait = Wait.Match(line);
                if (wait.Success)
                {
                    Add(result, new WorkflowInstruction
                    {
                        Kind = WorkflowInstructionKind.Wait,
                        SourceFile = sourceFile,
                        SourceLine = lineNumber,
                        DelayMilliseconds = ParseInt(wait.Groups["ms"].Value, sourceFile, lineNumber)
                    }, sourceFile, lineNumber);
                    continue;
                }

                var trigger = Trigger.Match(line);
                if (trigger.Success)
                {
                    Add(result, new WorkflowInstruction
                    {
                        Kind = WorkflowInstructionKind.TriggerDevice,
                        SourceFile = sourceFile,
                        SourceLine = lineNumber,
                        MachineType = trigger.Groups["machine"].Value.Trim(),
                        Command = trigger.Groups["command"].Value.Trim()
                    }, sourceFile, lineNumber);
                    continue;
                }

                Invalid(sourceFile, lineNumber, $"Lenh Lua khong duoc Edge ho tro: {line}");
            }

            if (inToDoubleHelper) Invalid(sourceFile, lines.Length, "Ham toDouble chua dong bang end.");
            if (loops.Count > 0) Invalid(sourceFile, loops.Peek().SourceLine, "Loop chua dong bang end.");
            if (result.Count == 0) throw new InvalidDataException($"Workflow artifact '{sourceFile}' khong co lenh thuc thi.");
            Validate(result);
            return result;
        }

        private static void Validate(IEnumerable<WorkflowInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if ((instruction.Kind == WorkflowInstructionKind.MoveJ || instruction.Kind == WorkflowInstructionKind.MoveL) &&
                    (instruction.Speed <= 0 || instruction.Speed > 100 || instruction.Acceleration <= 0 || instruction.Acceleration > 100))
                    Invalid(instruction.SourceFile, instruction.SourceLine, "Speed va acceleration phai trong khoang (0, 100].");
                if (instruction.Kind == WorkflowInstructionKind.Wait && instruction.DelayMilliseconds > 600000)
                    Invalid(instruction.SourceFile, instruction.SourceLine, "WaitMs khong duoc vuot qua 600000 ms.");
                if (instruction.Kind == WorkflowInstructionKind.SetDo && (instruction.Index < 0 || instruction.Index > 15))
                    Invalid(instruction.SourceFile, instruction.SourceLine, "SetDO index phai tu 0 den 15.");
                if (instruction.Kind == WorkflowInstructionKind.SetToolDo && (instruction.Index < 0 || instruction.Index > 1))
                    Invalid(instruction.SourceFile, instruction.SourceLine, "SetToolDO index phai la 0 hoac 1.");
                if (instruction.Kind == WorkflowInstructionKind.TriggerDevice &&
                    (string.IsNullOrWhiteSpace(instruction.MachineType) || string.IsNullOrWhiteSpace(instruction.Command)))
                    Invalid(instruction.SourceFile, instruction.SourceLine, "TriggerDevice thieu machineType hoac command.");
            }
        }

        private static void ExpandLoop(List<WorkflowInstruction> result, LoopFrame frame, string sourceFile, int lineNumber)
        {
            var body = result.Skip(frame.StartIndex).ToArray();
            if (body.Length == 0) Invalid(sourceFile, frame.SourceLine, "Loop rong khong duoc ho tro.");
            for (var iteration = 1; iteration < frame.Count; iteration++)
                foreach (var instruction in body) Add(result, Clone(instruction), sourceFile, lineNumber);
        }

        private static WorkflowInstruction Clone(WorkflowInstruction value) => new WorkflowInstruction
        {
            Kind = value.Kind,
            SourceFile = value.SourceFile,
            SourceLine = value.SourceLine,
            Values = value.Values.ToArray(),
            Speed = value.Speed,
            Acceleration = value.Acceleration,
            Index = value.Index,
            State = value.State,
            DelayMilliseconds = value.DelayMilliseconds,
            MachineType = value.MachineType,
            Command = value.Command
        };

        private static void Add(List<WorkflowInstruction> result, WorkflowInstruction instruction, string sourceFile, int lineNumber)
        {
            if (result.Count >= MaxExpandedInstructions) Invalid(sourceFile, lineNumber, $"Execution plan vuot qua {MaxExpandedInstructions} lenh.");
            result.Add(instruction);
        }

        private static double ParseNumber(string raw, string sourceFile, int lineNumber)
        {
            var value = raw.Trim();
            var wrapped = Regex.Match(value, "^toDouble\\s*\\(\\s*\"(?<value>[-+0-9.eE]+)\"\\s*\\)$");
            if (wrapped.Success) value = wrapped.Groups["value"].Value;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || double.IsNaN(parsed) || double.IsInfinity(parsed))
                Invalid(sourceFile, lineNumber, $"Gia tri so khong hop le: {raw.Trim()}");
            return parsed;
        }

        private static int ParseInt(string raw, string sourceFile, int lineNumber)
        {
            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                Invalid(sourceFile, lineNumber, $"Gia tri so nguyen khong hop le: {raw}");
            return parsed;
        }

        private static void Invalid(string sourceFile, int lineNumber, string message) =>
            throw new InvalidDataException($"{sourceFile}:{lineNumber}: {message}");

        private sealed class LoopFrame
        {
            public LoopFrame(int startIndex, int count, int sourceLine)
            {
                StartIndex = startIndex;
                Count = count;
                SourceLine = sourceLine;
            }
            public int StartIndex { get; }
            public int Count { get; }
            public int SourceLine { get; }
        }
    }
}
