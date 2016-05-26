﻿//-----------------------------------------------------------------------
// <copyright file="FieldDeclaration.cs">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//      EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//      MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
//      IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
//      CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
//      TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//      SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.PSharp.LanguageServices.Parsing;
using Microsoft.PSharp.Utilities;

namespace Microsoft.PSharp.LanguageServices.Syntax
{
    /// <summary>
    /// Field declaration syntax node.
    /// </summary>
    internal class FieldDeclaration : PSharpSyntaxNode
    {
        #region fields

        /// <summary>
        /// The machine parent node.
        /// </summary>
        protected MachineDeclaration Machine;

        /// <summary>
        /// The access modifier.
        /// </summary>
        internal AccessModifier AccessModifier;

        /// <summary>
        /// The type identifier.
        /// </summary>
        internal Token TypeIdentifier;

        /// <summary>
        /// The identifier token.
        /// </summary>
        internal Token Identifier;

        /// <summary>
        /// The semicolon token.
        /// </summary>
        internal Token SemicolonToken;

        #endregion

        #region internal API

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="program">Program</param>
        /// <param name="machineNode">MachineDeclarationNode</param>
        internal FieldDeclaration(IPSharpProgram program, MachineDeclaration machineNode)
            : base(program)
        {
            this.Machine = machineNode;
        }

        /// <summary>
        /// Rewrites the syntax node declaration to the intermediate C#
        /// representation.
        /// </summary>
        internal override void Rewrite()
        {
            string text = "";

            try
            {
                text = this.GetRewrittenFieldDeclaration();
            }
            catch (Exception ex)
            {
                IO.Debug("Exception was thrown during rewriting:");
                IO.Debug(ex.Message);
                IO.Debug(ex.StackTrace);
                ErrorReporter.ReportAndExit("Failed to rewrite field '{0}' of machine '{1}'.",
                    this.Identifier.TextUnit.Text, this.Machine.Identifier.TextUnit.Text);
            }

            base.TextUnit = new TextUnit(text, this.TypeIdentifier.TextUnit.Line);
        }

        #endregion

        #region private methods

        /// <summary>
        /// Returns the rewritten field declaration.
        /// </summary>
        /// <returns>Text</returns>
        private string GetRewrittenFieldDeclaration()
        {
            string text = "";

            if (this.AccessModifier == AccessModifier.Protected)
            {
                text += "protected ";
            }
            else if (this.AccessModifier == AccessModifier.Private)
            {
                text += "private ";
            }

            text += this.TypeIdentifier.TextUnit.Text;
            text += " ";

            text += this.Identifier.TextUnit.Text;

            text += this.SemicolonToken.TextUnit.Text + "\n";

            return text;
        }

        #endregion
    }
}
