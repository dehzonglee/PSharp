﻿// ------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------------------------------------------

using Microsoft.PSharp.IO;
using Microsoft.PSharp.LanguageServices;
using Microsoft.PSharp.LanguageServices.Compilation;
using Microsoft.PSharp.LanguageServices.Parsing;

namespace Microsoft.PSharp
{
    /// <summary>
    /// The P# syntax rewriter.
    /// </summary>
    public sealed class SyntaxRewriterProcess
    {
        private static void Main(string[] args)
        {
            // Number of args must be even.
            if (args.Length % 2 != 0)
            {
                Output.WriteLine("Usage: PSharpSyntaxRewriterProcess.exe file1.psharp, outfile1.cs, file2.pshap, outfile2.cs, ...");
                return;
            }

            int count = 0;
            while (count < args.Length)
            {
                string inputFileName = args[count];
                count++;
                string outputFileName = args[count];
                count++;

                // Get input file as string.
                var inputString = string.Empty;
                try
                {
                    inputString = System.IO.File.ReadAllText(inputFileName);
                }
                catch (System.IO.IOException e)
                {
                    Output.WriteLine("Error: {0}", e.Message);
                    return;
                }

                // Translate and write to output file.
                string errors = string.Empty;
                var outputString = Translate(inputString, out errors);
                if (outputString is null)
                {
                    // Replace Program.psharp with the actual file name.
                    errors = errors.Replace("Program.psharp", System.IO.Path.GetFileName(inputFileName));

                    // Print a compiler error with log.
                    System.IO.File.WriteAllText(
                        outputFileName,
                        string.Format("#error Psharp Compiler Error {0} /* {0} {1} {0} */ ", "\n", errors));
                }
                else
                {
                    // Tagging the generated .cs files with the "<auto-generated>" tag so as to avoid StyleCop build errors.
                    outputString = "//  <auto-generated />\n" + outputString;

                    System.IO.File.WriteAllText(outputFileName, outputString);
                }
            }
        }

        /// <summary>
        /// Translates the specified text from P# to C#.
        /// </summary>
        public static string Translate(string text, out string errors)
        {
            var configuration = Configuration.Create();
            configuration.IsVerbose = true;
            errors = null;

            var context = CompilationContext.Create(configuration).LoadSolution(text);

            try
            {
                ParsingEngine.Create(context).Run();
                RewritingEngine.Create(context).Run();

                var syntaxTree = context.GetProjects()[0].PSharpPrograms[0].GetSyntaxTree();

                return syntaxTree.ToString();
            }
            catch (ParsingException ex)
            {
                errors = ex.Message;
                return null;
            }
            catch (RewritingException ex)
            {
                errors = ex.Message;
                return null;
            }
        }
    }
}
