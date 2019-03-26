﻿using Microsoft.PSharp.TestingServices.Tracing.TreeTrace;
using System;
using System.Collections.Generic;
using System.Text;

namespace PSharpMinimizer.ControlUnits
{
    class MinimalTraceReplayControlUnit : ITraceEditorControlUnit
    {
        public TreeTraceEditor.TraceEditorMode RequiredTraceEditorMode { get { return TreeTraceEditor.TraceEditorMode.MinimizedTraceReplay; } }
        public EventTree BestTree { get; private set; }
        public int Left { get; private set; }
        public int Right { get; private set; }

        public bool Valid { get; private set; }

        public bool Completed { get; private set; }


        private int nReplays;
        MinimalTraceReplayControlUnit(EventTree guideTree, int nReplaysRequired)
        {
            BestTree = guideTree;
            nReplays = nReplaysRequired;
        }

        public bool PrepareForNextIteration(EventTree resultTree)
        {
            if (resultTree.reproducesBug() )
            {
                nReplays--;
                Completed = (nReplays <= 0); 
            }
            else
            {
                Valid = false;
                Completed = true;
            }
            return Completed;
        }
    }
}
