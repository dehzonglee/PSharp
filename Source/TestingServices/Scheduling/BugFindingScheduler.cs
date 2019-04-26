﻿// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.PSharp.IO;
using Microsoft.PSharp.Runtime;
using Microsoft.PSharp.TestingServices.Runtime;
using Microsoft.PSharp.TestingServices.Scheduling.Strategies;

namespace Microsoft.PSharp.TestingServices.Scheduling
{
    /// <summary>
    /// Provides methods for controlling the schedule of asynchronous operations.
    /// </summary>
    internal sealed class BugFindingScheduler
    {
        /// <summary>
        /// The P# testing runtime.
        /// </summary>
        private readonly TestingRuntime Runtime;

        /// <summary>
        /// The scheduling strategy to be used for state-space exploration.
        /// </summary>
        private readonly ISchedulingStrategy Strategy;

        /// <summary>
        /// Map from unique source ids to asynchronous operations.
        /// </summary>
        private readonly Dictionary<ulong, MachineOperation> OperationMap;

        /// <summary>
        /// The scheduler completion source.
        /// </summary>
        private readonly TaskCompletionSource<bool> CompletionSource;

        /// <summary>
        /// Checks if the scheduler is running.
        /// </summary>
        private bool IsSchedulerRunning;

        /// <summary>
        /// The currently scheduled asynchronous operation.
        /// </summary>
        internal MachineOperation ScheduledOperation { get; private set; }

        /// <summary>
        /// Number of scheduled steps.
        /// </summary>
        internal int ScheduledSteps => this.Strategy.GetScheduledSteps();

        /// <summary>
        /// Checks if the schedule has been fully explored.
        /// </summary>
        internal bool HasFullyExploredSchedule { get; private set; }

        /// <summary>
        /// True if a bug was found.
        /// </summary>
        internal bool BugFound { get; private set; }

        /// <summary>
        /// Bug report.
        /// </summary>
        internal string BugReport { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BugFindingScheduler"/> class.
        /// </summary>
        internal BugFindingScheduler(TestingRuntime runtime, ISchedulingStrategy strategy)
        {
            this.Runtime = runtime;
            this.Strategy = strategy;
            this.OperationMap = new Dictionary<ulong, MachineOperation>();
            this.CompletionSource = new TaskCompletionSource<bool>();
            this.IsSchedulerRunning = true;
            this.BugFound = false;
            this.HasFullyExploredSchedule = false;
        }

        /// <summary>
        /// Schedules the next asynchronous operation.
        /// </summary>
        internal void ScheduleNextOperation(AsyncOperationType type, AsyncOperationTarget target, ulong targetId)
        {
            int? taskId = Task.CurrentId;

            // If the caller is the root task, then return.
            if (taskId != null && taskId == this.Runtime.RootTaskId)
            {
                return;
            }

            if (!this.IsSchedulerRunning)
            {
                this.Stop();
            }

            // Checks if synchronisation not controlled by P# was used.
            this.CheckIfExternalSynchronizationIsUsed();

            // Checks if the scheduling steps bound has been reached.
            this.CheckIfSchedulingStepsBoundIsReached();

            MachineOperation current = this.ScheduledOperation;
            current.SetNextOperation(type, target, targetId);

            // Get and order the operations by their id.
            var ops = this.OperationMap.Values.OrderBy(op => op.SourceId).Select(op => op as IAsyncOperation).ToList();
            if (!this.Strategy.GetNext(out IAsyncOperation next, ops, current))
            {
                // Checks if the program has livelocked.
                this.CheckIfProgramHasLivelocked(ops.Select(op => op as MachineOperation));

                Debug.WriteLine("<ScheduleDebug> Schedule explored.");
                this.HasFullyExploredSchedule = true;
                this.Stop();
            }

            this.ScheduledOperation = next as MachineOperation;
            this.Runtime.ScheduleTrace.AddSchedulingChoice(next.SourceId);

            Debug.WriteLine($"<ScheduleDebug> Schedule '{next.SourceName}' with task id '{this.ScheduledOperation.Task.Id}'.");

            if (current != next)
            {
                current.IsActive = false;
                lock (next)
                {
                    this.ScheduledOperation.IsActive = true;
                    System.Threading.Monitor.PulseAll(next);
                }

                lock (current)
                {
                    if (!current.IsInboxHandlerRunning)
                    {
                        return;
                    }

                    while (!current.IsActive)
                    {
                        Debug.WriteLine($"<ScheduleDebug> Sleep '{current.SourceName}' with task id '{current.Task.Id}'.");
                        System.Threading.Monitor.Wait(current);
                        Debug.WriteLine($"<ScheduleDebug> Wake up '{current.SourceName}' with task id '{current.Task.Id}'.");
                    }

                    if (!current.IsEnabled)
                    {
                        throw new ExecutionCanceledException();
                    }
                }
            }
        }

        /// <summary>
        /// Returns the next nondeterministic boolean choice.
        /// </summary>
        internal bool GetNextNondeterministicBooleanChoice(int maxValue, string uniqueId = null)
        {
            // Checks if synchronisation not controlled by P# was used.
            this.CheckIfExternalSynchronizationIsUsed();

            // Checks if the scheduling steps bound has been reached.
            this.CheckIfSchedulingStepsBoundIsReached();

            if (!this.Strategy.GetNextBooleanChoice(maxValue, out bool choice))
            {
                Debug.WriteLine("<ScheduleDebug> Schedule explored.");
                this.Stop();
            }

            if (uniqueId is null)
            {
                this.Runtime.ScheduleTrace.AddNondeterministicBooleanChoice(choice);
            }
            else
            {
                this.Runtime.ScheduleTrace.AddFairNondeterministicBooleanChoice(uniqueId, choice);
            }

            return choice;
        }

        /// <summary>
        /// Returns the next nondeterministic integer choice.
        /// </summary>
        internal int GetNextNondeterministicIntegerChoice(int maxValue)
        {
            // Checks if synchronisation not controlled by P# was used.
            this.CheckIfExternalSynchronizationIsUsed();

            // Checks if the scheduling steps bound has been reached.
            this.CheckIfSchedulingStepsBoundIsReached();

            if (!this.Strategy.GetNextIntegerChoice(maxValue, out int choice))
            {
                Debug.WriteLine("<ScheduleDebug> Schedule explored.");
                this.Stop();
            }

            this.Runtime.ScheduleTrace.AddNondeterministicIntegerChoice(choice);

            return choice;
        }

        /// <summary>
        /// Waits for the event handler to start.
        /// </summary>
        internal void WaitForEventHandlerToStart(MachineOperation op)
        {
            lock (op)
            {
                if (this.OperationMap.Count == 1)
                {
                    op.IsActive = true;
                    System.Threading.Monitor.PulseAll(op);
                }
                else
                {
                    while (!op.IsInboxHandlerRunning)
                    {
                        System.Threading.Monitor.Wait(op);
                    }
                }
            }
        }

        /// <summary>
        /// Stops the scheduler.
        /// </summary>
        internal void Stop()
        {
            this.IsSchedulerRunning = false;
            this.KillRemainingMachines();

            // Check if the completion source is completed. If not synchronize on
            // it (as it can only be set once) and set its result.
            if (!this.CompletionSource.Task.IsCompleted)
            {
                lock (this.CompletionSource)
                {
                    if (!this.CompletionSource.Task.IsCompleted)
                    {
                        this.CompletionSource.SetResult(true);
                    }
                }
            }

            throw new ExecutionCanceledException();
        }

        /// <summary>
        /// Blocks until the scheduler terminates.
        /// </summary>
        internal void Wait() => this.CompletionSource.Task.Wait();

        /// <summary>
        /// Notify that an event handler has been created.
        /// </summary>
        internal void NotifyEventHandlerCreated(MachineOperation op)
        {
            if (!this.OperationMap.ContainsKey(op.SourceId))
            {
                if (this.OperationMap.Count == 0)
                {
                    this.ScheduledOperation = op;
                }

                this.OperationMap.Add(op.SourceId, op);
            }

            Debug.WriteLine($"<ScheduleDebug> Created event handler of '{op.SourceName}' with task id '{op.Task.Id}'.");
        }

        /// <summary>
        /// Notify that a monitor was registered.
        /// </summary>
        internal void NotifyMonitorRegistered(MachineOperation op)
        {
            this.OperationMap.Add(op.SourceId, op);
            Debug.WriteLine($"<ScheduleDebug> Created monitor of '{op.SourceName}'.");
        }

        /// <summary>
        /// Notify that the event handler has started.
        /// </summary>
        internal static void NotifyEventHandlerStarted(MachineOperation op)
        {
            Debug.WriteLine($"<ScheduleDebug> Started event handler of '{op.SourceName}' with task id '{op.Task.Id}'.");

            lock (op)
            {
                op.IsInboxHandlerRunning = true;
                System.Threading.Monitor.PulseAll(op);
                while (!op.IsActive)
                {
                    Debug.WriteLine($"<ScheduleDebug> Sleep '{op.SourceName}' with task id '{op.Task.Id}'.");
                    System.Threading.Monitor.Wait(op);
                    Debug.WriteLine($"<ScheduleDebug> Wake up '{op.SourceName}' with task id '{op.Task.Id}'.");
                }

                if (!op.IsEnabled)
                {
                    throw new ExecutionCanceledException();
                }
            }
        }

        /// <summary>
        /// Notify that an assertion has failed.
        /// </summary>
        internal void NotifyAssertionFailure(string text, bool killTasks = true)
        {
            if (!this.BugFound)
            {
                this.BugReport = text;

                this.Runtime.Logger.OnError($"<ErrorLog> {text}");
                this.Runtime.Logger.OnStrategyError(this.Runtime.Configuration.SchedulingStrategy, this.Strategy.GetDescription());

                this.BugFound = true;

                if (this.Runtime.Configuration.AttachDebugger)
                {
                    System.Diagnostics.Debugger.Break();
                }
            }

            if (killTasks)
            {
                this.Stop();
            }
        }

        /// <summary>
        /// Returns the enabled schedulable ids.
        /// </summary>
        internal HashSet<ulong> GetEnabledSchedulableIds()
        {
            var enabledSchedulableIds = new HashSet<ulong>();
            foreach (var machineInfo in this.OperationMap.Values)
            {
                if (machineInfo.IsEnabled)
                {
                    enabledSchedulableIds.Add(machineInfo.SourceId);
                }
            }

            return enabledSchedulableIds;
        }

        /// <summary>
        /// Returns a test report with the scheduling statistics.
        /// </summary>
        internal TestReport GetReport()
        {
            TestReport report = new TestReport(this.Runtime.Configuration);

            if (this.BugFound)
            {
                report.NumOfFoundBugs++;
                report.BugReports.Add(this.BugReport);
            }

            if (this.Strategy.IsFair())
            {
                report.NumOfExploredFairSchedules++;
                report.TotalExploredFairSteps += this.ScheduledSteps;

                if (report.MinExploredFairSteps < 0 ||
                    report.MinExploredFairSteps > this.ScheduledSteps)
                {
                    report.MinExploredFairSteps = this.ScheduledSteps;
                }

                if (report.MaxExploredFairSteps < this.ScheduledSteps)
                {
                    report.MaxExploredFairSteps = this.ScheduledSteps;
                }

                if (this.Strategy.HasReachedMaxSchedulingSteps())
                {
                    report.MaxFairStepsHitInFairTests++;
                }

                if (this.ScheduledSteps >= report.Configuration.MaxUnfairSchedulingSteps)
                {
                    report.MaxUnfairStepsHitInFairTests++;
                }
            }
            else
            {
                report.NumOfExploredUnfairSchedules++;

                if (this.Strategy.HasReachedMaxSchedulingSteps())
                {
                    report.MaxUnfairStepsHitInUnfairTests++;
                }
            }

            return report;
        }

        /// <summary>
        /// Checks for a livelock. This happens when there are no more enabled
        /// operations, but there is one or more non-enabled operations that are
        /// waiting to receive an event.
        /// </summary>
        private void CheckIfProgramHasLivelocked(IEnumerable<MachineOperation> ops)
        {
            var blockedOperations = ops.Where(op => op.IsWaitingToReceive).ToList();
            if (blockedOperations.Count > 0)
            {
                string message = "Livelock detected.";
                for (int i = 0; i < blockedOperations.Count; i++)
                {
                    message += string.Format(CultureInfo.InvariantCulture, " '{0}'", blockedOperations[i].SourceName);
                    if (i == blockedOperations.Count - 2)
                    {
                        message += " and";
                    }
                    else if (i < blockedOperations.Count - 1)
                    {
                        message += ",";
                    }
                }

                message += blockedOperations.Count == 1 ? " is " : " are ";
                message += "waiting to receive an event, but no other controlled tasks are enabled.";
                this.Runtime.Scheduler.NotifyAssertionFailure(message, true);
            }
        }

        /// <summary>
        /// Checks if external (non-P#) synchronisation was used to invoke
        /// the scheduler. If yes, it stops the scheduler, reports an error
        /// and kills all enabled machines.
        /// </summary>
        private void CheckIfExternalSynchronizationIsUsed()
        {
            int? taskId = Task.CurrentId;
            if (taskId is null)
            {
                string message = "Detected concurrency that is not controlled by the P# runtime.";
                this.NotifyAssertionFailure(message, true);
            }

            if (this.ScheduledOperation.Task.Id != taskId.Value)
            {
                string message = $"Detected task with id '{taskId}' that is not controlled by the P# runtime.";
                this.NotifyAssertionFailure(message, true);
            }
        }

        /// <summary>
        /// Checks if the scheduling steps bound has been reached. If yes,
        /// it stops the scheduler and kills all enabled machines.
        /// </summary>
        private void CheckIfSchedulingStepsBoundIsReached()
        {
            if (this.Strategy.HasReachedMaxSchedulingSteps())
            {
                int bound = this.Strategy.IsFair() ? this.Runtime.Configuration.MaxFairSchedulingSteps : this.Runtime.Configuration.MaxUnfairSchedulingSteps;
                string message = $"Scheduling steps bound of {bound} reached.";

                if (this.Runtime.Configuration.ConsiderDepthBoundHitAsBug)
                {
                    this.Runtime.Scheduler.NotifyAssertionFailure(message, true);
                }
                else
                {
                    Debug.WriteLine($"<ScheduleDebug> {message}");
                    this.Stop();
                }
            }
        }

        /// <summary>
        /// Kills any remaining machines at the end of the schedule.
        /// </summary>
        private void KillRemainingMachines()
        {
            foreach (var machine in this.OperationMap.Values)
            {
                machine.IsActive = true;
                machine.IsEnabled = false;

                if (machine.IsInboxHandlerRunning)
                {
                    lock (machine)
                    {
                        System.Threading.Monitor.PulseAll(machine);
                    }
                }
            }
        }
    }
}
