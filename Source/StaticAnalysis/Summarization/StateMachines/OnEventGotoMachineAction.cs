using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.PSharp.DataFlowAnalysis;

namespace Microsoft.PSharp.StaticAnalysis
{
    /// <summary>
    /// The P# on event goto machine action.
    /// </summary>
    internal sealed class OnEventGotoMachineAction : MachineAction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OnEventGotoMachineAction"/> class.
        /// </summary>
        internal OnEventGotoMachineAction(MethodDeclarationSyntax methodDecl, MachineState state)
            : base(methodDecl, state)
        {
        }
    }
}
