using System;

namespace Cython.PowerTransmission
{
	public class Configuration
	{
		public bool RadialFalloff = true;
		public bool UseMaximumPower = true;

		public RadialPowerTransmitterConfiguration LargeBlockSmallRadialPowerTransmitter = new RadialPowerTransmitterConfiguration();
		public OpticalPowerTransmitterConfiguration LargeBlockSmallOpticalPowerTransmitter = new OpticalPowerTransmitterConfiguration();
		public RadialPowerTransmitterConfiguration SmallBlockSmallRadialPowerTransmitter = new SmallRadialPowerTransmitterConfiguration();
		public OpticalPowerTransmitterConfiguration SmallBlockSmallOpticalPowerTransmitter = new SmallOpticalPowerTransmitterConfiguration();



		public Configuration ()
		{
		}
	}
}

