using System;

namespace Cython.PowerTransmission
{
	public class SmallOpticalPowerTransmitterConfiguration: OpticalPowerTransmitterConfiguration
	{
		public SmallOpticalPowerTransmitterConfiguration ()
		{
			this.MaximumRange = 9000.0f;
			this.MaximumPower = 10.0f;
		}
	}
}

