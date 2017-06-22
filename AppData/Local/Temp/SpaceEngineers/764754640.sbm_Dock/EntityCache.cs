using System;
using SpaceEngineers.Game.ModAPI;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace DockingAssist
{
	public class EntityCache : IEnumerable<IMyFunctionalBlock>
	{
		List<IMyFunctionalBlock> Blocks = new List<IMyFunctionalBlock>();
		internal void Remove(IMyFunctionalBlock block)
		{
			if(Blocks.Contains(block))
			{
				Blocks.Remove(block);
			}
		}

		internal int Add(IMyFunctionalBlock block)
		{
			if (!Blocks.Contains(block))
			{
				Blocks.Add(block);
			}
			return Blocks.IndexOf(block);
				
		}

		internal void Copy(EntityCache CopyFrom)
		{
			foreach(var item in CopyFrom)
			{
				Add(item);
			}
		}

		public IEnumerator<IMyFunctionalBlock> GetEnumerator()
		{
			return ((IEnumerable<IMyFunctionalBlock>)Blocks).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<IMyFunctionalBlock>)Blocks).GetEnumerator();

		}
		public void Clear()
		{
			Blocks.Clear();
		}
	}
}