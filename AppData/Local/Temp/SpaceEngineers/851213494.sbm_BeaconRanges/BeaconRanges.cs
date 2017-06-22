using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
 
using VRage.Game;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI;
 
using System.Collections.Generic;
 
namespace theimmersion.ModdableBeaconRange
{
    /*
     * Thanks to Cheetah on Steam (http://steamcommunity.com/profiles/76561198177407838),
     * who inspired this script mod and made half of this.
	 * and Clone_Commando for trying to fix it.
	 * But mostly thanks to Phoera for the helpful and all knowing guy that he is.
    */

/*
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Loader : MySessionComponentBase
    {
        private static bool InitedSession;
        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session != null && !InitedSession)
            {
                InitAntennae();
                InitedSession = true;
            }
        }
        public static readonly Dictionary<string, float> AntennaRanges = new Dictionary<string, float>();
        public static void InitAntennae()
        {
            List<IMyTerminalControl> antennactrls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<IMyBeacon>(out antennactrls);
            IMyTerminalControlSlider RadiusSlider = antennactrls.Find(x => x.Id == "Radius") as IMyTerminalControlSlider;

            RadiusSlider.SetLogLimits((Antenna) => 1, (Antenna) => (Antenna as IMyBeacon).GetMaxRange());
        }
    }
*/
//MyEntityComponentDescriptor
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), false)]
    public class BeaconSettings : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void Close()
        {
            NeedsUpdate = MyEntityUpdateEnum.NONE; // HACK required because of an issue with new gamelogic attachments not going away after entity is removed.
        }

		IMyBeacon beacon;

		public override void UpdateOnceBeforeFrame()
		{
            beacon = Entity as IMyBeacon;

            List<IMyTerminalControl> antennactrls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<IMyBeacon>(out antennactrls);

            IMyTerminalControlSlider RadiusSlider = antennactrls.Find(x => x.Id == "Radius") as IMyTerminalControlSlider;

            float maxVal = beacon.ParseMaxRange();
            RadiusSlider.SetLimits(0, Extensions.ParseMaxRange(beacon));
		}
    }

    public static class Extensions
    {
        /// <summary>
        /// Returns &lt;Description&gt; tag contents from block's .sbc definition.
        /// </summary>
        public static string GetCustomDefinition(this IMyCubeBlock Block)
        {
            return (Block as Sandbox.Game.Entities.MyCubeBlock).BlockDefinition.DescriptionString;
        }
 
        public static float GetDefaultRange(this IMyBeacon Antenna)
        {
            return Antenna.CubeGrid.GridSizeEnum == MyCubeSize.Large ? 10000 : 5000;
        }
 
        public static float ParseMaxRange(this IMyBeacon Antenna)
        {
            float range = GetDefaultRange(Antenna);
            string description = GetCustomDefinition(Antenna);

			if(description!=null)
			{
				var Description = description.Replace("\r\n", "\n").Trim().Split('\n');
	 
				foreach (string DescriptionLine in Description)
				{
					if (DescriptionLine.Trim().StartsWith("MaxRange:"))
						float.TryParse(DescriptionLine.Split(':')[1].Trim(), out range);
				}
			}
            return range;
        }
    }
}
/*
custom antenna range within the .sbc files antenna definition
<Description>MaxRange:ANY NUMBER IN METERS</Description>
Esample:
<Description>MaxRange:15000</Description>
*/