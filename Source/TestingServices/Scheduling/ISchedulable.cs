﻿//-----------------------------------------------------------------------
// <copyright file="ISchedulable.cs">
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

namespace Microsoft.PSharp.TestingServices.Scheduling
{
    /// <summary>
    /// Interface of an entity that can be scheduled.
    /// </summary>
    public interface ISchedulable
    {
        /// <summary>
        /// Unique id of the entity.
        /// </summary>
        ulong Id { get; }

        /// <summary>
        /// Name of the entity.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Is the entity enabled.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Is the entity completed.
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Type of the next operation of the entity.
        /// </summary>
        OperationType NextOperationType { get; }

        /// <summary>
        /// The target type of the next operation of the entity.
        /// </summary>
        OperationTargetType NextTargetType { get; }

        /// <summary>
        /// Target id of the next operation of the entity.
        /// </summary>
        int NextTargetId { get; }

        /// <summary>
        /// If the next operation is <see cref="OperationType.Receive"/>
        /// then this gives the step index of the corresponding Send. 
        /// </summary>
        int NextOperationMatchingSendIndex { get; }

        /// <summary>
        /// Monotonically increasing operation count.
        /// </summary>
        int OperationCount { get; }
    }
}
