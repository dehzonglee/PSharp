﻿// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.PSharp.Runtime;
using Microsoft.PSharp.TestingServices.Runtime.Scheduling.ProgramAwareScheduling.ProgramModel;
using Microsoft.PSharp.TestingServices.Runtime.Scheduling.Strategies.ProgramAware.ProgramAwareMetrics.StepSignatures;
using Microsoft.PSharp.TestingServices.Scheduling;
using Microsoft.PSharp.TestingServices.Scheduling.Strategies;

namespace Microsoft.PSharp.TestingServices.Runtime.Scheduling.Strategies.ProgramAware
{
    /// <summary>
    /// Base class which implements program model construction
    /// </summary>
    public abstract class AbstractBaseProgramModelStrategy : IProgramAwareSchedulingStrategy
    {
        // Some handy constants
        protected private const ulong TESTHARNESSMACHINEID = 0;
        protected private const ulong TESTHARNESSMACHINEHASH = 199999;

        protected private ProgramModel ProgramModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="AbstractBaseProgramModelStrategy"/> class.
        /// </summary>
        public AbstractBaseProgramModelStrategy()
        {
            this.ResetProgramModel();
        }

        /// <summary>
        /// If true, The ProgramStepEventInfo.HashedState field will be set for all eventInfo
        /// </summary>
        protected abstract bool HashEvents { get; }

        /// <inheritdoc/>
        public virtual string GetDescription()
        {
            return "Abstract class which implements program model construction methods";
        }

        private void ResetProgramModel()
        {
            this.ProgramModel = new ProgramModel();
        }

        /// <inheritdoc/>
        public abstract bool IsFair();

        /// <summary>
        /// Resets program model. Call if you override.
        /// </summary>
        public virtual void Reset()
        {
            // TODO
            this.ResetProgramModel();
        }

        /// <summary>
        /// Resets program model. Call if you override.
        /// </summary>
        /// <returns>true if the reset succeeded ( which it would )</returns>
        public virtual bool PrepareForNextIteration()
        {
            // Please call even if you override
            this.ResetProgramModel();
            return true;
        }

        /// <inheritdoc/>
        public abstract int GetScheduledSteps();

        /// <inheritdoc/>
        public abstract bool HasReachedMaxSchedulingSteps();

        // Scheduling Choice(?)s

        /// <inheritdoc/>
        public abstract void ForceNext(IAsyncOperation next, List<IAsyncOperation> ops, IAsyncOperation current);

        /// <inheritdoc/>
        public abstract void ForceNextBooleanChoice(int maxValue, bool next);

        /// <inheritdoc/>
        public abstract void ForceNextIntegerChoice(int maxValue, int next);

        /// <inheritdoc/>
        public abstract bool GetNext(out IAsyncOperation next, List<IAsyncOperation> ops, IAsyncOperation current);

        /// <summary>
        /// Should be internal
        /// </summary>
        /// <returns>RootStep</returns>
        public IProgramStep GetRootStep()
        {
            return this.ProgramModel.Rootstep;
        }

        /// <summary>
        /// Not sure whether this should work.
        /// </summary>
        /// <returns>The program model</returns>
        public IProgramStep GetBugTriggeringStep()
        {
            return this.ProgramModel.BugTriggeringStep;
        }

        /// <inheritdoc/>
        public abstract bool GetNextBooleanChoice(int maxValue, out bool next);

        /// <inheritdoc/>
        public abstract bool GetNextIntegerChoice(int maxValue, out int next);

        // The program-aware part

        /// <inheritdoc/>
        public virtual void RecordCreateMachine(Machine createdMachine, Machine creatorMachine)
        {
            ProgramStep createStep = new ProgramStep(AsyncOperationType.Create, creatorMachine?.Id.Value ?? 0, createdMachine.Id.Value, null);
            this.ProgramModel.RecordStep(createStep, this.GetScheduledSteps()); // TODO: Should i do -1?
        }

        /// <inheritdoc/>
        public virtual void RecordStartMachine(Machine machine, Event initialEvent)
        {
            ProgramStepEventInfo pEventInfo = null;
            if (initialEvent != null)
            {
                pEventInfo = new ProgramStepEventInfo(initialEvent, 0);
            }
            else
            {
                pEventInfo = null;
            }

            ProgramStep startStep = new ProgramStep(AsyncOperationType.Start, machine.Id.Value, machine.Id.Value, pEventInfo);
            this.ProgramModel.RecordStep(startStep, this.GetScheduledSteps());
        }

        /// <inheritdoc/>
        public virtual void RecordReceiveEvent(Machine machine, Event evt)
        {
            ProgramStepEventInfo pEventInfo = new ProgramStepEventInfo(evt, 0);
            ProgramStep receiveStep = new ProgramStep(AsyncOperationType.Receive, machine.Id.Value, machine.Id.Value, pEventInfo);
            this.ProgramModel.RecordStep(receiveStep, this.GetScheduledSteps());
        }

        /// <inheritdoc/>
        public void RecordSendEvent(AsyncMachine sender, MachineId targetMachineId, Event e)
        {
            ProgramStepEventInfo pEventInfo = new ProgramStepEventInfo(e, sender?.Id.Value ?? 0);
            if (this.HashEvents)
            {
                pEventInfo.HashedState = this.HashEvent(e);
            }

            ProgramStep sendStep = new ProgramStep(AsyncOperationType.Send, sender?.Id.Value ?? 0, targetMachineId.Value, pEventInfo);
            this.ProgramModel.RecordStep(sendStep, this.GetScheduledSteps());
        }

        private int HashEvent(Event e)
        {
            return ReflectionBasedHasher.HashObject(e);
        }

        /// <inheritdoc/>
        public void RecordNonDetBooleanChoice(bool boolChoice)
        {
            ProgramStep ndBoolStep = new ProgramStep(this.ProgramModel.ActiveStep.SrcId, boolChoice);
            this.ProgramModel.RecordStep(ndBoolStep, this.GetScheduledSteps());
        }

        /// <inheritdoc/>
        public void RecordNonDetIntegerChoice(int intChoice)
        {
            ProgramStep ndIntStep = new ProgramStep(this.ProgramModel.ActiveStep.SrcId, intChoice);
            this.ProgramModel.RecordStep(ndIntStep, this.GetScheduledSteps());
        }

        /// <inheritdoc/>
        public void RecordMonitorEvent(Type monitorType, AsyncMachine sender, Event e)
        {
            // Do Nothing
            this.ProgramModel.RecordMonitorEvent(monitorType, sender);
        }

        /// <summary>
        /// Called at the end of a scheduling iteration.
        /// Please explicitly call base.NotifySchedulingEnded if you override.
        /// </summary>
        /// <param name="bugFound">Was bug found in this run</param>
        public virtual void NotifySchedulingEnded(bool bugFound)
        {
            // TODO: Liveness bugs.
            this.ProgramModel.RecordSchedulingEnded(bugFound, false);
        }

        /// <inheritdoc/>
        public string GetProgramTrace()
        {
            return this.ProgramModel.SerializeProgramTrace();
        }

        /// <inheritdoc/>
        public virtual string GetReport()
        {
            return null;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.GetType().Name;
        }
    }
}
