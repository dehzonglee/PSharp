﻿// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace Microsoft.PSharp.TestingServices.Runtime.Scheduling.ProgramAwareScheduling.ProgramModel
{
    /// <summary>
    /// The minimal set of components of the program model needed to reproduce the program.
    /// </summary>
    public class ProgramModelSummary
    {
        /// <summary>
        /// The root of the partial order representing the execution
        /// </summary>
        public readonly ProgramStep PartialOrderRoot;

        /// <summary>
        /// A list of send steps ( contained in the partial order ) whose events were not enqueued
        /// </summary>
        public readonly List<ProgramStep> WithHeldSends;

        /// <summary>
        /// The step which triggers the bug. For assertion violatioons, It is the step during which the assert fails
        /// For liveness bugs, it is the step at which the monitor enters the hot state (which it never leaves).
        /// </summary>
        public readonly ProgramStep BugTriggeringStep;

        /// <summary>
        /// If the triggered bug is a liveness violation, The type of the monitor which triggered it.
        /// </summary>
        public readonly Type LivenessViolatingMonitorType;

        /// <summary>
        /// The number of steps executed
        /// </summary>
        public readonly int NumSteps;

#if WE_DECIDE_TO_INCLUDE_CRITICAL_TRANSITION_EXPLICITLY
        /// <summary>
        /// The critical transition - For liveness bugs, executing the trace till this step is guaranteed to reproduce the bug.
        /// </summary>
        public readonly ProgramStep CriticalTransition;
        /// <param name="criticalTransition">the critical transition step</param>
#endif

        /// <summary>
        /// Tells whether or not the bug is a liveness bug
        /// </summary>
        public bool IsLivenessBug
        {
            get => this.LivenessViolatingMonitorType != null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgramModelSummary"/> class.
        /// </summary>
        /// <param name="partialOrderRoot">the root of the partial order</param>
        /// <param name="bugTriggeringStep">the step which triggered the bug</param>
        /// <param name="withHeldSends">list of send steps for which the message was not enqueued</param>
        /// <param name="numSteps">The number of steps executed</param>
        /// <param name="hotMonitor">If the bug is a liveness bug, the type of the monitor which triggered the bug</param>
        public ProgramModelSummary(ProgramStep partialOrderRoot, ProgramStep bugTriggeringStep, List<ProgramStep> withHeldSends, int numSteps, Type hotMonitor)
        {
            this.PartialOrderRoot = partialOrderRoot;
            this.WithHeldSends = withHeldSends;
            this.BugTriggeringStep = bugTriggeringStep;
            this.LivenessViolatingMonitorType = hotMonitor;

            this.NumSteps = numSteps;
        }
    }
}
