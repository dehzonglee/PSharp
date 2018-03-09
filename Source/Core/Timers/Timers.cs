//-----------------------------------------------------------------------
// <copyright file="Timers.cs">
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
using System.Timers;

namespace Microsoft.PSharp.Timers
{
	/// <summary>
	/// Extends the P# Machine with a simple timer.
	/// </summary>
	public abstract class TMachine : Machine
	{
		#region private fields
		/// <summary>
		/// True if timeouts are expected at regular intervals.
		/// False if a single timeout is expected.
		/// </summary>
		private bool IsPeriodic;

		/// <summary>
		/// False if machines are running in production mode.
		/// </summary>
		private bool IsTestingMode;

		/// <summary>
		/// If the timer is periodic, periodicity of the timeout events.
		/// Default is 100ms, same as the default in System.Timers.Timer
		/// </summary>
		private int period = 100;

		/// <summary>
		/// System timer to generate Elapsed timeout events in production mode.
		/// </summary>
		private System.Timers.Timer timer;

		/// <summary>
		/// Model timers generating timeout events in test mode.
		/// </summary>
		private MachineId modelTimer;

		/// <summary>
		/// Flag to prevent timeout events being sent after stopping the timer.
		/// </summary>
		private volatile bool IsTimerEnabled = false;

		/// <summary>
		/// Used to synchronize the Elapsed event handler with timer stoppage.
		/// </summary>
		private readonly Object tlock = new object();

		#endregion

		#region Timer API
		/// <summary>
		/// Start a timer. For periodic timeouts, a default period of 100ms is assumed.
		/// </summary>
		/// <param name="IsTestingMode">True if machines are in test mode, False if machines are running in production.</param>
		/// <param name="IsPeriodic">True if periodic timeouts are desired. False if a single timeout is desired.</param>
		protected void StartTimer(bool IsTestingMode, bool IsPeriodic)
		{
			this.IsTestingMode = IsTestingMode;
			this.IsPeriodic = IsPeriodic;
			StartTimer();
		}

		/// <summary>
		/// Start a timer.
		/// </summary>
		/// <param name="IsTestingMode">True if machines are in test mode, False if machines are running in production.</param>
		/// <param name="IsPeriodic">True if periodic timeouts are desired. False if a single timeout is desired.</param>
		/// <param name="period">Specify the periodicity of the timeout events.</param>
		protected void StartTimer(bool IsTestingMode, bool IsPeriodic, int period)
		{
			this.IsTestingMode = IsTestingMode;
			this.IsPeriodic = IsPeriodic;
			this.period = period;
			StartTimer();
		}

		/// <summary>
		/// Stop the timer.
		/// </summary>
		/// <param name="flush">True if the user wants to flush the input queue of all eTimeout events.</param>
		protected void StopTimer(bool flush)
		{
			if (!this.IsPeriodic)
			{
				lock (this.tlock)
				{
					this.IsTimerEnabled = false;
					this.timer.Stop();
					this.timer.Dispose();
				}
			}

			else
			{
				this.Send(this.modelTimer, new Halt());
			}

			// Clear the client machine's queue of eTimeout events.
			if (flush)
			{
				// Send a single Markup event to the queue.
				this.Send(this.Id, new Markup());

				// Dequeue all eTimeout events, until we see the Markup event.
				while (this.Receive(typeof(Markup), typeof(eTimeout)).GetType() != (typeof(Markup))) { }
			}
		}

		#endregion

		#region private methods
		/// <summary>
		/// Start the timer. 
		/// </summary>
		private void StartTimer()
		{
			// For production code, use the system timer.
			if (!IsTestingMode)
			{
				this.timer = new System.Timers.Timer(period);

				if (!IsPeriodic)
				{
					this.timer.AutoReset = false;
				}

				this.timer.Elapsed += ElapsedEventHandler;
				this.IsTimerEnabled = true;
				this.timer.Start();
			}

			// In testing mode, create a timer model, and pass the identifier of the calling machine.
			else
			{
				this.modelTimer = this.CreateMachine(typeof(TimerModel), new InitTimer(this.Id, this.IsPeriodic));
			}
		}

		/// <summary>
		/// Handler for the Elapsed event generated by the system timer.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="e"></param>
		private void ElapsedEventHandler(Object source, ElapsedEventArgs e)
		{
			lock (this.tlock)
			{
				if (this.IsTimerEnabled)
				{
					Runtime.SendEvent(this.Id, new eTimeout());
				}
			}
		}

		#endregion
	}
}