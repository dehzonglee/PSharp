﻿
using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.PSharp.IO;
using Microsoft.PSharp.Runtime;
using Microsoft.PSharp.TestingServices.SchedulingStrategies;
using Microsoft.PSharp.TestingServices.Tracing.Error;
using Microsoft.PSharp.TestingServices.Tracing.Schedule;
using Microsoft.PSharp.TestingServices.Tracing.TreeTrace;

namespace Microsoft.PSharp.TestingServices.Scheduling
{
    /// <summary>
    /// Class representing a replaying scheduling strategy.
    /// </summary>
    internal sealed class MinimizationStrategy : ISchedulingStrategy
    {
        #region fields

        /// <summary>
        /// The configuration.
        /// </summary>
        private Configuration Configuration;

        private int originalTraceLength;

        /// <summary>
        /// The suffix strategy.
        /// </summary>
        private ISchedulingStrategy SuffixStrategy;
        
        /// <summary>
        /// Is the scheduler that produced the
        /// schedule trace fair?
        /// </summary>
        private bool IsOriginalSchedulerFair;

        /// <summary>
        /// Is the scheduler replaying the trace?
        /// </summary>
        private bool IsReplayingPsharpScheduleTrace { get {
                return traceEditor.currentMode == TreeTraceEditor.TraceEditorMode.ScheduleTraceReplay;
            }
        }

        /// <summary>
        /// The number of scheduled steps.
        /// </summary>
        private int ScheduledSteps;

        /// <summary>
        /// Text describing a replay error.
        /// </summary>
        internal string ErrorText { get; private set; }
        public bool IsMintraceDump;
        public bool HAX_findCriticalTransition = true ;



        #endregion

        #region public API

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configuration">Configuration</param>
        /// <param name="scheduleDump">scheduleDump</param>
        /// <param name="suffixStrategy">The suffix strategy.</param>
        public MinimizationStrategy(Configuration configuration, string[] scheduleDump, ISchedulingStrategy suffixStrategy)
        {
            Configuration = configuration;
            ScheduledSteps = 0;
            
            replayStrategy = null;
            SuffixStrategy = suffixStrategy;
            ErrorText = string.Empty;

            ParseScheduleDumpMeta(scheduleDump);

            ConstructTree = true;
            WasAbandoned = false;

            if ( IsMintraceDump )
            {
                // TODO: We need to create the traceEditor tree up here
                EventTree et = EventTree.FromTrace(scheduleDump);
                traceEditor = new TreeTraceEditor();
                traceEditor.prepareForMinimalTraceReplay(et); // TODO: False or true?
                originalTraceLength = et.CountSteps();
            }
            else
            {
                traceEditor = new TreeTraceEditor();
                ScheduleTrace trace = new ScheduleTrace(scheduleDump);
                replayStrategy = new ReplayStrategy(Configuration, trace, IsOriginalSchedulerFair);
                traceEditor.prepareForScheduleTraceReplay();
                originalTraceLength = trace.Count;
            }

        }

        private void ParseScheduleDumpMeta(string[] scheduleDump)
        {
            IsOriginalSchedulerFair = false;
            IsMintraceDump = false;
            foreach (string line in scheduleDump)
            {
                if (!line.StartsWith("--"))
                {
                    break;
                }

                if (line.Equals("--fair-scheduling"))
                {
                    IsOriginalSchedulerFair = true;
                }
                else if (line.Equals("--cycle-detection"))
                {
                    this.Configuration.EnableCycleDetection = true;
                }
                else if (line.StartsWith("--liveness-temperature-threshold:"))
                {
                    this.Configuration.LivenessTemperatureThreshold =
                        Int32.Parse(line.Substring("--liveness-temperature-threshold:".Length));
                }
                else if (line.StartsWith("--test-method:"))
                {
                    this.Configuration.TestMethodName =
                        line.Substring("--test-method:".Length);
                }else if (line.StartsWith("--is-mintrace"))
                {
                    IsMintraceDump = true;
                }
            }
        }

        //private bool isFirstStep;

        /// <summary>
        /// Returns the next choice to schedule.
        /// </summary>
        /// <param name="next">Next</param>
        /// <param name="choices">Choices</param>
        /// <param name="current">Curent</param>
        /// <returns>Boolean</returns>
        public bool GetNext(out ISchedulable next, List<ISchedulable> choices, ISchedulable current)
        {
            if (IsReplayingPsharpScheduleTrace)
            {
                return GetNextReplayMode(out next, choices, current);
            }
            else
            {
                return GetNextEditMode(out next, choices, current);
                
            }
        }

        private bool GetNextReplayMode(out ISchedulable next, List<ISchedulable> choices, ISchedulable current)
        {
            if (ConstructTree)
            {
                traceEditor.recordSchedulingChoiceResult(current, choices.ToDictionary(x => x.Id), (ulong)GetScheduledSteps());
            }

            if (replayStrategy.GetNext(out next, choices, current)){

                if (ConstructTree)
                {
                    traceEditor.recordSchedulingChoiceStart(next, (ulong)GetScheduledSteps());
                }
                return true;
            }else{
                return false;
            }
        }

        public bool GetNextEditMode(out ISchedulable next, List<ISchedulable> choices, ISchedulable current)
        {
            if (ConstructTree)
            {
                traceEditor.recordSchedulingChoiceResult(current, choices.ToDictionary(x => x.Id), (ulong)GetScheduledSteps());
            }

            bool result = traceEditor.GetNext(out next, choices, current);
            if (result)
            {
                ScheduledSteps++;
                if (ConstructTree)
                {
                    traceEditor.recordSchedulingChoiceStart(next, (ulong)GetScheduledSteps());
                }
            }
            else if(!result && traceEditor.reachedEnd() && SuffixStrategy!=null)
            {
                // Stop logging past this point
                ConstructTree = false;
                result = SuffixStrategy.GetNext(out next, choices, current);
            }
            
            return result;
        }

        /// <summary>
        /// Returns the next boolean choice.
        /// </summary>
        /// <param name="maxValue">The max value.</param>
        /// <param name="next">Next</param>
        /// <returns>Boolean</returns>
        public bool GetNextBooleanChoice(int maxValue, out bool next)
        {
            if (IsReplayingPsharpScheduleTrace)
            {
                return GetNextBooleanChoiceReplayMode(maxValue, out next);
            }
            else
            {
                return GetNextBooleanChoiceEditMode(maxValue, out next);
            }
        }

        public bool GetNextBooleanChoiceReplayMode(int maxValue, out bool next) {
            if (replayStrategy.GetNextBooleanChoice(maxValue, out next))
            {
                if (ConstructTree)
                {
                    traceEditor.RecordBooleanChoice(next);
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool GetNextBooleanChoiceEditMode(int maxValue, out bool next)
        {
            bool result = traceEditor.GetNextBooleanChoice(maxValue, out next);
            if (result)
            {
                ScheduledSteps++;
                if (ConstructTree)
                {
                    traceEditor.RecordBooleanChoice(next);
                }
            }
            else if (!result && traceEditor.reachedEnd() && SuffixStrategy != null)
            {
                // Stop logging past this point
                ConstructTree = false;
                result = SuffixStrategy.GetNextBooleanChoice(maxValue, out next);
            }
            return result;
        }

        internal EventTree getBestTree()
        {
            return traceEditor.getGuideTree();
        }

        /// <summary>
        /// Returns the next integer choice.
        /// </summary>
        /// <param name="maxValue">The max value.</param>
        /// <param name="next">Next</param>
        /// <returns>Boolean</returns>
        public bool GetNextIntegerChoice(int maxValue, out int next)
        {
            if (IsReplayingPsharpScheduleTrace)
            {
                return GetNextIntegerChoiceReplayMode(maxValue, out next);
            }
            else
            {
                return GetNextIntegerChoiceEditMode(maxValue, out next);
            }
        }

        private bool GetNextIntegerChoiceReplayMode(int maxValue, out int next)
        {
            if(replayStrategy.GetNextIntegerChoice(maxValue, out next)){
                if (ConstructTree)
                {
                    traceEditor.RecordIntegerChoice(next);
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool GetNextIntegerChoiceEditMode(int maxValue, out int next) {

            bool result = traceEditor.GetNextIntegerChoice(maxValue, out next);
            if (result)
            {
                ScheduledSteps++;
                if (ConstructTree)
                {
                    traceEditor.RecordIntegerChoice(next);
                }
            }
            else if (!result && traceEditor.reachedEnd() && SuffixStrategy != null)
            {
                // Stop logging past this point
                ConstructTree = false;
                result = SuffixStrategy.GetNextIntegerChoice(maxValue, out next);
            }
            return result;

        }

        /// <summary>
        /// Forces the next choice to schedule.
        /// </summary>
        /// <param name="next">Next</param>
        /// <param name="choices">Choices</param>
        /// <param name="current">Curent</param>
        /// <returns>Boolean</returns>
        public void ForceNext(ISchedulable next, List<ISchedulable> choices, ISchedulable current)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Forces the next boolean choice.
        /// </summary>
        /// <param name="maxValue">The max value.</param>
        /// <param name="next">Next</param>
        /// <returns>Boolean</returns>
        public void ForceNextBooleanChoice(int maxValue, bool next)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Forces the next integer choice.
        /// </summary>
        /// <param name="maxValue">The max value.</param>
        /// <param name="next">Next</param>
        /// <returns>Boolean</returns>
        public void ForceNextIntegerChoice(int maxValue, int next)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Prepares for the next scheduling iteration. This is invoked
        /// at the end of a scheduling iteration. It must return false
        /// if the scheduling strategy should stop exploring.
        /// </summary>
        /// <returns>True to start the next iteration</returns>
        public bool PrepareForNextIteration()
        {
            replayStrategy?.PrepareForNextIteration();
            ScheduledSteps = 0;
            ConstructTree = true;

            if (WasAbandoned)
            {   // The edit went catastrophically wrong. Re-enter edit mode
                //enterEditMode(false);
                WasAbandoned = false;
                traceEditor.prepareForTraceEditIteration();
            }
            else
            {
                ConstructTree = true;
                switch (traceEditor.currentMode)
                {
                    case TreeTraceEditor.TraceEditorMode.ScheduleTraceReplay:
                        traceEditor.prepareForMinimalTraceReplay();
                        //if (HAX_findCriticalTransition) {
                        //    traceEditor.prepareForCriticalTransitionSearchIteration();
                        //} else { 
                        //    traceEditor.prepareForMinimalTraceReplay();
                        //}
                        break;
                    case TreeTraceEditor.TraceEditorMode.MinimizedTraceReplay:
                        if (HAX_findCriticalTransition)
                        {
                            traceEditor.prepareForCriticalTransitionSearchIteration();
                        }
                        else
                        {
                            traceEditor.prepareForTraceEditIteration();
                        }
                        
                        break;
                    case TreeTraceEditor.TraceEditorMode.CriticalTransitionSearch:
                        if (!traceEditor.prepareForCriticalTransitionSearchIteration())
                        {
                            traceEditor.prepareForTraceEditIteration();
                        }
                        break;
                    case TreeTraceEditor.TraceEditorMode.TraceEdit:
                            traceEditor.prepareForTraceEditIteration();
                        break;
                    case TreeTraceEditor.TraceEditorMode.EpochCompleted:
                        break;
                    default:
                        throw new ArgumentException("TraceEditor is in invalid state");

                }
            }

                
            bool result = traceEditor.currentMode!= TreeTraceEditor.TraceEditorMode.EpochCompleted;
            if (SuffixStrategy != null)
            {
                result = result && SuffixStrategy.PrepareForNextIteration();
            }

            // TODO:
            return result;
        }

        /// <summary>
        /// Resets the scheduling strategy. This is typically invoked by
        /// parent strategies to reset child strategies.
        /// </summary>
        public void Reset()
        {
            ScheduledSteps = 0;
            replayStrategy?.Reset();
            SuffixStrategy?.Reset();
        }

        /// <summary>
        /// Returns the scheduled steps.
        /// </summary>
        /// <returns>Scheduled steps</returns>
        public int GetScheduledSteps()
        {
            if (IsReplayingPsharpScheduleTrace) // is replaying
            {
                return replayStrategy.GetScheduledSteps();
            }else
            {
                return ScheduledSteps + (SuffixStrategy?.GetScheduledSteps()??0); // I'm not even sure what this is
            }
        }

        /// <summary>
        /// True if the scheduling strategy has reached the depth
        /// bound for the given scheduling iteration.
        /// </summary>
        /// <returns>Boolean</returns>
        public bool HasReachedMaxSchedulingSteps()
        {
            return GetScheduledSteps() >  Configuration.LivenessTemperatureThreshold + originalTraceLength;
        }

        /// <summary>
        /// Checks if this is a fair scheduling strategy.
        /// </summary>
        /// <returns>Boolean</returns>
        public bool IsFair()
        {
            // TODO: This.
            if (SuffixStrategy != null)
            {
                return SuffixStrategy.IsFair();
            }
            else
            {
                return false; // Can't guarantee the edited trace is fair
            }
        }

        /// <summary>
        /// Returns a textual description of the scheduling strategy.
        /// </summary>
        /// <returns>String</returns>
        public string GetDescription()
        {
            if (SuffixStrategy != null)
            {
                return "Replay(" + SuffixStrategy.GetDescription() + ")";
            }
            else
            {
                return "Replay";
            }
        }

        #endregion


        #region Minimization Stuff



        // Minimization stuff
        // TODO: Binary search Delta Debugging. Till then -> linear search.

        // Edit mode stuff
        private bool WasAbandoned;
        private bool ConstructTree;

        // Deletion
        //private IMinimizationGuide MinimizationGuide;

        // Replay mode
        ReplayStrategy replayStrategy;
        //private int replaysRequired = 0; // How many before we conclude we hit the bug? For future if we want more confidence of reproducing
        //private int replaysRemaining = 0; // 

        // Exchange between the two modes:

        //private 
        internal TreeTraceEditor traceEditor;

        //private void enterEditMode(bool bugReproduced)
        //{
        //    traceEditor(bugReproduced);
        //    WasAbandoned = false;
        //    ConstructTree = true;
        //}

        
        public void recordResult(bool bugFound, ScheduleTrace scheduleTrace)
        {
            traceEditor.recordResult(bugFound, scheduleTrace);
            if (IsReplayingPsharpScheduleTrace && !bugFound)
            {
                throw new ArgumentException("Could not reproduce bug with ScheduleTrace");
            }

        }

        internal bool ShouldDeliverEvent(BaseMachine sender, Event e, Machine receiver)
        {
            if (IsReplayingPsharpScheduleTrace)
            {
                return true;
            }
            else
            {
                return traceEditor?.ShouldDeliverEvent(e) ?? true;
            }
        }

        #endregion
    }
}