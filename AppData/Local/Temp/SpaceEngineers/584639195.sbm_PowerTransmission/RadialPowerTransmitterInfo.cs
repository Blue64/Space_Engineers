using System;
using Sandbox.ModAPI;

namespace Cython.PowerTransmission
{
	public class RadialPowerTransmitterInfo
	{

		public IMyFunctionalBlock functionalBlock;
		public string subtypeName;
		public uint channel;
		public bool sender;
		public bool enabled;
		public float strength;
		public float currentInput;

		public RadialPowerTransmitterInfo ()
		{
		}
	}
}

