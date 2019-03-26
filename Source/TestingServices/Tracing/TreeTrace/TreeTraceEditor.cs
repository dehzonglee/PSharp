﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PSharp.TestingServices.SchedulingStrategies;
using Microsoft.PSharp.TestingServices.Tracing.Error;
using Microsoft.PSharp.TestingServices.Tracing.Schedule;

namespace Microsoft.PSharp.TestingServices.Tracing.TreeTrace
{
    internal class TreeTraceEditor
    {


        private EventTree BestGuideTree;

        // Use as read only. Let SchedulingStrategy do updates
        // TODO: Move to TraceIteration
        internal ProgramModel activeProgramModel;
        private bool isFirstStep;

        //private int currentIterationIndex;

        internal ulong actualStepsExecuted; // Count of how many steps we've actually executed ( is this even needed here? Let the strategy do it? )

        //int currentWithHoldCandidate; // 1 indexed :p
        Stack<Tuple<int,int>> bSearchWithHoldCandidateStack;
        //HashSet<int> withHeldCandidates;

        //internal bool ranOutOfEvents;
        int eventSeenThisTime;
        TraceIteration currentRun; // Make private
        internal int currentWithHoldRangeStart;
        internal int currentWithHoldRangeEnd;
        internal enum TraceEditorMode
        {
            Initial,

            ScheduleTraceReplay,
            MinimizedTraceReplay,
            TraceEdit,

            EpochCompleted,
            CriticalTransitionSearch,
        }

        internal TraceEditorMode currentMode;
        private static bool HAX_DoLinearScan = true; // HAX
        private int criticalTransitionSearch_Left;
        private int criticalTransitionSearch_Right;

        // Deletion Tracking
        internal class TraceIteration {

            EventTree guideTree;
            internal int totalOrderingIndex = 0;

            internal EventTreeNode activeNode;
            private int activeNode_boolIndex;
            private int activeNode_intIndex;
            HashSet<int> deletedIndices;
            internal int HAX_deletedCount;
            private int criticalTransitionIndex;

            public TraceIteration(EventTree GuideTree)
            {
                guideTree = GuideTree;
                totalOrderingIndex = 0;
                criticalTransitionIndex = -1;

                deletedIndices = new HashSet<int>();
                setActiveNode(null);
                HAX_deletedCount = 0;
            }

            public EventTreeNode peek()
            {
                return guideTree.totalOrdering[totalOrderingIndex];
            }

            public void next()
            {
                totalOrderingIndex++;
            }

            public void addDeletion(int index)
            {
                deletedIndices.Add(index);
            }
            public bool checkDeleted(EventTreeNode etn)
            {
                if (deletedIndices.Contains(etn.totalOrderingIndex))
                {
                    if (etn.directChild != null) { deletedIndices.Add(etn.directChild.totalOrderingIndex); }
                    if (etn.createdChild != null) { deletedIndices.Add(etn.createdChild.totalOrderingIndex); }
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public void setActiveNode(EventTreeNode match)
            {
                activeNode = match;
                activeNode_boolIndex = 0;
                activeNode_intIndex = 0;
            }
            public bool getNondetBooleanChoice(out bool nextBool)
            {

                if (activeNode != null && activeNode.getBoolean(activeNode_boolIndex, out nextBool))
                {
                    activeNode_boolIndex++;
                    return true;
                }
                else
                {
                    nextBool = false;
                    return false;
                }
            }

            public bool getNondetIntegerChoice(out int nextInt)
            {

                if (activeNode != null && activeNode.getInteger(activeNode_intIndex, out nextInt))
                {
                    activeNode_intIndex++;
                    return true;
                }
                else
                {
                    nextInt = 0;
                    return false;
                }
            }

            internal bool coarseMatch(EventTreeNode etn)
            {
                // TODO: DO a coarse match
                return true;
            }

            internal bool reachedEnd()
            {
                if(guideTree?.totalOrdering != null)
                {
                    return ( criticalTransitionIndex >= 0 && totalOrderingIndex > criticalTransitionIndex ) 
                        || (totalOrderingIndex >= guideTree.totalOrdering.Count);
                }
                else
                {
                    return false;
                }
                
            }

            internal bool checkWithHeld(EventTreeNode etn)
            {
                return guideTree?.checkWithHeld(etn)??false;
            }

            internal void setCriticalTransition(int criticalTransitionIndex)
            {
                this.criticalTransitionIndex = criticalTransitionIndex;
            }
        }


        public TreeTraceEditor()
        {
            BestGuideTree = null;
            //currentWithHoldCandidate = -1; // This so that prepareForNextIteration makes it 0
            bSearchWithHoldCandidateStack = new Stack<Tuple<int, int>>();
            currentMode = TraceEditorMode.Initial;
            //ranOutOfEvents = false;

            criticalTransitionSearch_Left = -1;
            criticalTransitionSearch_Right = -1;
        }
        

        public void reset()
        {
            eventSeenThisTime = 0;
            currentWithHoldRangeStart = 0;
            currentWithHoldRangeEnd = 0;
            activeProgramModel = new ProgramModel();
            isFirstStep = true;
        }

        #region prepare for next iteration

        public bool prepareForScheduleTraceReplay()
        {
            if(currentMode!= TraceEditorMode.Initial)
            {
                throw new ArgumentException("TraceEditor must be in Initial state to do ScheduleTraceReplay");
            }
            currentMode = TraceEditorMode.ScheduleTraceReplay;
            currentRun = new TraceIteration(null);
            reset();
            return true;
        }

        public bool prepareForMinimalTraceReplay(EventTree candidateGuideTree)
        {
            currentMode = TraceEditorMode.MinimizedTraceReplay;
            currentRun = new TraceIteration(candidateGuideTree);
            reset();
            return true;
        }

        public bool prepareForCriticalTransitionSearchIteration()
        {
            bool result = false;
            switch (currentMode)
            {
                //case TraceEditorMode.Initial:
                //case TraceEditorMode.ScheduleTraceReplay:
                //    if (activeProgramModel.getTree().reproducesBug())
                //    {
                //        result = true;
                //        criticalTransitionSearch_Left = 0;
                //        criticalTransitionSearch_Right = activeProgramModel.getTree().totalOrdering.Count - 1;
                //    }
                //    else
                //    {
                //        throw new ArgumentException("Replay did not reproduce bug.");
                //    }
                //    break;

                // TODO: 26032019 you were here, Figuring out replays for critical transitions and rewriting the internal FSM
                case TraceEditorMode.MinimizedTraceReplay:
                    if (activeProgramModel.getTree().reproducesBug())
                    {
                        result = true;
                        BestGuideTree = activeProgramModel.getTree();
                        if (criticalTransitionSearch_Left == -1 && criticalTransitionSearch_Right == -1)
                        {
                            criticalTransitionSearch_Left = 0;
                            criticalTransitionSearch_Right = activeProgramModel.getTree().totalOrdering.Count - 1;
                        }
                    }
                    else if( BestGuideTree == null ) // && !activeProgramModel.getTree().reproducesBug()
                    {
                        throw new ArgumentException("No bug-reproducing guide tree for CriticalTransition.");
                    }
                    break;

                case TraceEditorMode.CriticalTransitionSearch:
                    if (activeProgramModel.getTree().reproducesBug())
                    {
                        BestGuideTree = activeProgramModel.getTree();
                        if ( criticalTransitionSearch_Left == criticalTransitionSearch_Right )
                        {
                            result = false;
                        }
                        else
                        {
                            result = true;
                            int mid = (criticalTransitionSearch_Left + criticalTransitionSearch_Right) / 2;
                            criticalTransitionSearch_Right = mid;
                        }
                    }
                    else
                    {
                        if ( criticalTransitionSearch_Left == criticalTransitionSearch_Right )
                        {
                            throw new ArgumentException("CriticalTransitionIndex converged on ");
                        }
                        else
                        {
                            result = true;

                            int mid = (criticalTransitionSearch_Left + criticalTransitionSearch_Right) / 2;
                            criticalTransitionSearch_Left = mid+1;
                        }

                    }
                    break;
                default:
                    throw new ArgumentException("TraceEditor cannot do CriticalTransitionSearch from state " + currentMode);


            }

            if (result) {
                prepareForMinimalTraceReplay(BestGuideTree);
                currentMode = TraceEditorMode.CriticalTransitionSearch;
                int criticalTransitionIndex = (criticalTransitionSearch_Left + criticalTransitionSearch_Right) / 2;
                currentRun.setCriticalTransition(criticalTransitionIndex);
            }
            return result;
        }


        public bool prepareForTraceEditIteration()
        {
            switch (currentMode)
            {
                case TraceEditorMode.Initial:
                case TraceEditorMode.ScheduleTraceReplay:
                case TraceEditorMode.EpochCompleted:
                    throw new ArgumentException("Cannot start TraceEditIteration from state" + currentMode );
                case TraceEditorMode.MinimizedTraceReplay:
                case TraceEditorMode.CriticalTransitionSearch:
                    if (BestGuideTree == null) // Means initial run
                    {
                        if (activeProgramModel.getTree().reproducesBug()) {
                            if (HAX_DoLinearScan)
                            {
                                for(int i = eventSeenThisTime; i>0 ; i--)
                                {
                                    bSearchWithHoldCandidateStack.Push(new Tuple<int, int>(i,i));
                                }
                                
                            }
                            else
                            {
                                bSearchWithHoldCandidateStack.Push(new Tuple<int, int>(1, eventSeenThisTime));
                            }
                        }
                        else
                        {
                            throw new ArgumentException("Cannot reproduce bug from ScheduleTrace");
                        }
                    }
                    break;
            }

            currentMode = TraceEditorMode.TraceEdit;

            if (activeProgramModel.getTree().reproducesBug())
            {
                BestGuideTree = activeProgramModel.getTree();
            }
            else
            {
                if (currentWithHoldRangeStart != currentWithHoldRangeEnd)
                {   // Nawsome. We need to recurse :(
                    int mid = (currentWithHoldRangeStart + currentWithHoldRangeEnd) / 2;
                    bSearchWithHoldCandidateStack.Push(new Tuple<int, int>(mid + 1, currentWithHoldRangeEnd));
                    bSearchWithHoldCandidateStack.Push(new Tuple<int, int>(currentWithHoldRangeStart, mid));
                }
            }

            
            reset();

            if (bSearchWithHoldCandidateStack.Count==0) {
                //ranOutOfEvents = true;
                currentMode = TraceEditorMode.EpochCompleted;
            }
            else
            {
                //ranOutOfEvents = false; // Let's not risk this
                Tuple<int, int> currentWithHoldTuple = bSearchWithHoldCandidateStack.Pop();
                currentWithHoldRangeStart = currentWithHoldTuple.Item1;
                currentWithHoldRangeEnd = currentWithHoldTuple.Item2;
            }
            currentRun = new TraceIteration(BestGuideTree);

            return currentMode==TraceEditorMode.EpochCompleted;
        }
        #endregion

        internal EventTree getGuideTree()
        {
            return BestGuideTree;
        }

        
        internal bool ShouldDeliverEvent(Event e)
        {
            //TODO: Properly
            if (currentRun.reachedEnd()){
                return true;
            }
            bool deliver = true;
            System.Console.WriteLine(e.GetType().FullName);
            if ( eventIsCandidateToBeDropped(e) )
            {
                eventSeenThisTime++;

                if (currentRun.checkWithHeld(currentRun.activeNode))
                {
                    deliver = false;
                    // Don't do optimization hacks right now. It'll get confusing. +  we're doing bsearch anyway
                    //if (eventSeenThisTime == currentWithHoldCandidate)
                    //{
                    //    // Don't waste this deletion run. It's already 
                    //    currentWithHoldCandidate++;
                    //}

                }
                else if (eventSeenThisTime >= currentWithHoldRangeStart && eventSeenThisTime <= currentWithHoldRangeEnd)
                {
                    deliver = false;
                }
            }

            if (deliver)
            {
                return true;
            }
            else
            { 
                if (currentRun.activeNode != null && currentRun.activeNode.createdChild != null)
                {
                    currentRun.addDeletion(currentRun.activeNode.createdChild.totalOrderingIndex);
                }
                recordEventWithHeld();
                return false;
            }
        }

        internal bool reachedEnd()
        {
            return currentRun.reachedEnd();
        }

        private bool eventIsCandidateToBeDropped(Event e)
        {
            return (
                e.GetType().FullName == "ReplicatingStorage.SyncTimer+Timeout" ||
                e.GetType().FullName == "Raft.PeriodicTimer+Timeout" || e.GetType().FullName == "Raft.ElectionTimer+Timeout" ||
                e.GetType().FullName == "ChainReplication.Client+Update" || e.GetType().FullName == "ChainReplication.Client+Query"
                );
        }



        // Queries
        #region program model queries
        internal bool GetNext(out ISchedulable next, List<ISchedulable> choices, ISchedulable current)
        {
            Dictionary<ulong, ISchedulable> enabledChoices = choices.Where(x => x.IsEnabled).ToDictionary(x => x.Id);
            // Skip everything deleted ?
            // TODO: Can we be smarter? Check if any of the deleted ones could actually be one worth delivering
            //      i.e., matches the enabledOne, whereas the next non-deleted one does not match (??) idk.
            while (!currentRun.reachedEnd() && currentRun.checkDeleted(currentRun.peek()))
            {
                currentRun.HAX_deletedCount++;
                currentRun.next();
            }

            // Now we need to see if we can schedule this event. If it doesn't exist, what do we do? Ignore?
            next = null;

            bool matched = false;
            while (!currentRun.reachedEnd() && !matched)
            {
                EventTreeNode currentNode = currentRun.peek();
                if (enabledChoices.ContainsKey(currentNode.srcMachineId))
                {

                    ISchedulable candidate = enabledChoices[currentNode.srcMachineId];
                    EventTreeNode potentialMatch;
                    if (activeProgramModel.getTreeNodeFromISchedulable(candidate, out potentialMatch))
                    {
                        if (currentRun.coarseMatch(potentialMatch))
                        {
                            // It's likely a match. 
                            currentRun.setActiveNode(currentNode);
                            // TODO: Should we backtrack?
                        }
                        else
                        {   // It's likely not the one we were looking for. 
                            // We could always assume it is and backtrack, but better keep coarse matching coarse.
                            // For now, just deliver this message assuming it's an added, unexpected extra.
                            // TODO: What if it's the next one? And the one we're expecting never appears because 
                            //  it's a hidden causal relation which got deleted :(
                            currentRun.setActiveNode(null); // TODO: Should this be currentNode anyway? Don't think so
                        }
                    }
                    else
                    {
                        // Hitting this would mean our program model is wrong,
                        // We can't match an enabled node in our program model to the scheduler
                        // The guidetree has nothing to do with it. That only comes into play during coarseMatch
                        throw new ArgumentException("This really shouldn't happen. ");
                    }

                    next = candidate;
                    matched = true;

                }
                else
                {   // The machine we expected is not here. Assume it got deleted and skip to next one ?
                    currentRun.next();
                }

            }

            return matched;

        }

        internal bool GetNextBooleanChoice(int maxValue, out bool next)
        {
            return currentRun.getNondetBooleanChoice(out next);
        }
        internal bool GetNextIntegerChoice(int maxValue, out int next)
        {
            bool result = currentRun.getNondetIntegerChoice(out next);
            if (result)
            {
                if (next > maxValue)
                {
                    throw new ArgumentException("Val is less than maxval");
                }
                return true;
            }
            else
            {
                return false;
            }

        }
        #endregion

        #region program model updates

        internal void recordEventWithHeld()
        {
            activeProgramModel.recordEventWithHeld();
        }
        internal void recordSchedulingChoiceStart(ISchedulable next, ulong scheduledSteps)
        {
            activeProgramModel.recordSchedulingChoiceStart(next, scheduledSteps);
        }

        internal void recordSchedulingChoiceResult(ISchedulable current, Dictionary<ulong, ISchedulable> enabledChoices, ulong scheduledSteps)
        {
            if (isFirstStep)
            {   // Needs some help here.
                EventTreeNode root = activeProgramModel.initializeWithTestHarnessMachine(current.Id); // Is this correct?
                currentRun?.setActiveNode(root);
                isFirstStep = false;
            }
            activeProgramModel.recordSchedulingChoiceResult(current, enabledChoices, scheduledSteps);
            if (currentRun!=null && currentRun.activeNode != null)
            {
                currentRun.next();
            }
        }

        internal void recordResult(bool bugFound, ScheduleTrace scheduleTrace)
        {
            activeProgramModel.getTree().setResult(bugFound, scheduleTrace);
        }

        internal void RecordIntegerChoice(int next)
        {
            activeProgramModel.RecordIntegerChoice(next);
        }

        internal void RecordBooleanChoice(bool next)
        {
            activeProgramModel.RecordBooleanChoice(next);
        }

        internal void prepareForMinimalTraceReplay()
        {
            prepareForMinimalTraceReplay(activeProgramModel.constructTree);
        }

        #endregion
    }
}