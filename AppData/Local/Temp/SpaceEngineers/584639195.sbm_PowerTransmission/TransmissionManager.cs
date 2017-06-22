using System.Collections.Generic;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;

namespace Cython.PowerTransmission {

	public static class TransmissionManager {

		public static Configuration configuration = new Configuration();

		public static Dictionary<long, RadialPowerTransmitterInfo> radialTransmitters = new Dictionary<long, RadialPowerTransmitterInfo>();
		public static Dictionary<long, OpticalPowerTransmitterInfo> opticalTransmitters = new Dictionary<long, OpticalPowerTransmitterInfo>();

		public static Dictionary<long, float> totalPowerPerGrid = new Dictionary<long, float>();

		public static MyDefinitionId electricityDefinition = new MyDefinitionId (typeof(MyObjectBuilder_GasProperties), "Electricity");
	}
}
