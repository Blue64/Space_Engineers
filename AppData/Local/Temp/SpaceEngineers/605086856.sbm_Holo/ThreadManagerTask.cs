using Sandbox.ModAPI;
using System;
using ParallelTasks;
namespace Draygo.Utils
{
	internal class ThreadManagerTask
	{
		//can be set in other threads. This will be set to false so the thread management knows to skip.
		internal bool Valid = true;
		internal bool IsComplete = false;
		internal bool Added;
		private Action calcComplete;
		private Action refreshDragBox;
		public ThreadManagerTask(Action refreshDragBox, Action calcComplete)
		{
			this.refreshDragBox = refreshDragBox;
			this.calcComplete = calcComplete;
		}
		internal Task Run()
		{
			return MyAPIGateway.Parallel.StartBackground(refreshDragBox, calcComplete);
		}
	}
}