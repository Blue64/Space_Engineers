/*
using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
 
using VRage.Game;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI;
 
using System.Collections.Generic;
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using Havok;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Lights;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Input;
using VRageMath;
using VRage;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;

using IMyControllableEntity = Sandbox.Game.Entities.IMyControllableEntity;
using Sandbox.Game.Entities.Character.Components;
using VRage.Library.Utils;

using Sandbox.Graphics.GUI;
using Sandbox.Game.GameSystems.Electricity;
using Sandbox.Engine.Utils;
using Sandbox.Game.Entities.Cube;
using System.Reflection;
using Sandbox.Common;
using Sandbox.Engine.Physics;
using Sandbox.Game.World;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Gui;
using Sandbox.Game.Screens;
using Sandbox.Game.Screens.Terminal.Controls;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Localization;
using Sandbox.Engine.Multiplayer;
using VRage.Network;
using VRage.Sync;

using Ingame = Sandbox.ModAPI.Ingame;
 
namespace theimmersion.ModdableAntennaRange
{
    /*
     * Thanks to Cheetah on Steam (http://steamcommunity.com/profiles/76561198177407838),
     * who inspired this script mod and made half of this.
	 * and Clone_Commando for trying to fix it.
	 * But mostly thanks to Phoera for the helpful and all knowing guy that he is.
    */

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_RadioAntenna), false)]
    public class RadioAntennaSettings : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void Close()
        {
            NeedsUpdate = MyEntityUpdateEnum.NONE; // HACK required because of an issue with new gamelogic attachments not going away after entity is removed.
        }

		IMyRadioAntenna radio;

		public override void UpdateOnceBeforeFrame()
		{
			radio = Entity as IMyRadioAntenna;

			List<IMyTerminalControl> antennactrls = new List<IMyTerminalControl>();
			MyAPIGateway.TerminalControls.GetControls<IMyRadioAntenna>(out antennactrls);

			IMyTerminalControlSlider RadiusSlider = antennactrls.Find(x => x.Id == "Radius") as IMyTerminalControlSlider;

            float maxVal = radio.ParseMaxRange();
            RadiusSlider.SetLimits(0, Extensions.ParseMaxRange(radio));
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
 
		public static float GetDefaultRange(this IMyRadioAntenna Antenna)
		{
			return Antenna.CubeGrid.GridSizeEnum == MyCubeSize.Large ? 10000 : 5000;
		}

		public static float ParseMaxRange(this IMyRadioAntenna Antenna)
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

*/
/*
custom antenna range within the .sbc files antenna definition
<Description>MaxRange:ANY NUMBER IN METERS</Description>
Esample:
<Description>MaxRange:15000</Description>
*/