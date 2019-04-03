﻿// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.PSharp.TestingServices.SchedulingStrategies
{
    /// <summary>
    /// Interface of a generic scheduling strategy.
    /// </summary>
    public interface ISchedulingStrategy
    {
        /// <summary>
        /// Returns the next entity to schedule.
        /// </summary>
        /// <param name="next">The next entity to schedule.</param>
        /// <param name="choices">List of entities that can be scheduled.</param>
        /// <param name="current">The currently scheduled entity.</param>
        /// <returns>True if there is a next choice, else false.</returns>
        bool GetNext(out ISchedulable next, List<ISchedulable> choices, ISchedulable current);

        /// <summary>
        /// Returns the next boolean choice.
        /// </summary>
        /// <param name="maxValue">The max value.</param>
        /// <param name="next">The next boolean choice.</param>
        /// <returns>True if there is a next choice, else false.</returns>
        bool GetNextBooleanChoice(int maxValue, out bool next);

        /// <summary>
        /// Returns the next integer choice.
        /// </summary>
        /// <param name="maxValue">The max value.</param>
        /// <param name="next">The next integer choice.</param>
        /// <returns>True if there is a next choice, else false.</returns>
        bool GetNextIntegerChoice(int maxValue, out int next);

        /// <summary>
        /// Forces the next entity to be scheduled.
        /// </summary>
        /// <param name="next">The next entity to schedule.</param>
        /// <param name="choices">List of entities that can be scheduled.</param>
        /// <param name="current">The currently scheduled entity.</param>
        void ForceNext(ISchedulable next, List<ISchedulable> choices, ISchedulable current);

        /// <summary>
        /// Forces the next boolean choice.
        /// </summary>
        /// <param name="maxValue">The max value.</param>
        /// <param name="next">The next boolean choice.</param>
        void ForceNextBooleanChoice(int maxValue, bool next);

        /// <summary>
        /// Forces the next integer choice.
        /// </summary>
        /// <param name="maxValue">The max value.</param>
        /// <param name="next">The next integer choice.</param>
        void ForceNextIntegerChoice(int maxValue, int next);

        /// <summary>
        /// Prepares for the next scheduling iteration. This is invoked
        /// at the end of a scheduling iteration. It must return false
        /// if the scheduling strategy should stop exploring.
        /// </summary>
        /// <returns>True to start the next iteration.</returns>
        bool PrepareForNextIteration();

        /// <summary>
        /// Resets the scheduling strategy. This is typically invoked by
        /// parent strategies to reset child strategies.
        /// </summary>
        void Reset();

        /// <summary>
        /// Returns the scheduled steps.
        /// </summary>
        /// <returns>The scheduled steps.</returns>
        int GetScheduledSteps();

        /// <summary>
        /// True if the scheduling strategy has reached the max
        /// scheduling steps for the given scheduling iteration.
        /// </summary>
        bool HasReachedMaxSchedulingSteps();

        /// <summary>
        /// Checks if this is a fair scheduling strategy.
        /// </summary>
        bool IsFair();

        /// <summary>
        /// Returns a textual description of the scheduling strategy.
        /// </summary>
        string GetDescription();
    }
}
