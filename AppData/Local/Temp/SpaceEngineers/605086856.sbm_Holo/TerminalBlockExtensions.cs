using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Hologram
{
	public static class TerminalBlockExtensions
	{
		public static bool IsRadar(this IMyTerminalBlock block)
		{
			return block?.GameLogic?.GetAs<Radar>() != null && block.GameLogic.GetAs<Radar>().valid;
		}
	}
}
