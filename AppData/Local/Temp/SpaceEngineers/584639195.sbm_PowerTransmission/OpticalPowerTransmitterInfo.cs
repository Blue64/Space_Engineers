using System;
using Sandbox.ModAPI;

namespace Cython.PowerTransmission
{
	public class OpticalPowerTransmitterInfo
	{

		public IMyFunctionalBlock functionalBlock;
		public string subtypeName;
		public uint id;
		public bool sender;
		public bool enabled;
		public float strength;
		public float rayOffset;
		public float currentInput;

		public OpticalPowerTransmitterInfo ()
		{
		}
	}
}

