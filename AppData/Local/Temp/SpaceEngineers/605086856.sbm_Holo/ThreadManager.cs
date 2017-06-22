using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ParallelTasks;
namespace Draygo.Utils
{
	internal class ThreadManager
	{
		Queue<ThreadManagerTask> ProcessStack = new Queue<ThreadManagerTask>(100);
		Task CurrentTask;
		ThreadManagerTask ThreadTask;
		internal void Update()
		{
			if (CurrentTask.IsComplete)
			{
				if (ThreadTask != null)
				{
					ThreadTask.IsComplete = true;
					ThreadTask = null;
				}
				if (ProcessStack.TryDequeue(out ThreadTask)) CurrentTask = ThreadTask.Run();
				else
				{
				}
			}
		}
		internal ThreadManagerTask Add(Action NewTask, Action calcComplete)
		{
			ThreadManagerTask EnqueueTask = new ThreadManagerTask(NewTask, calcComplete);
			if (ProcessStack.Count < 100)
			{
				EnqueueTask.Added = true;
				ProcessStack.Enqueue(EnqueueTask);
			}
			else
			{
				EnqueueTask.Added = false;
			}
			return EnqueueTask;
		}
	}
}
