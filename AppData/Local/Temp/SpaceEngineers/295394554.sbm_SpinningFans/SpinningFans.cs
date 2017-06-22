using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Sandbox.Common;
using Sandbox.Common.Components;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.VRageData;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Engine.Physics;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRageMath;
using VRage;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Components;
using VRage.Utils;
using Ingame = Sandbox.ModAPI.Ingame;
namespace Digi.SpinningFans
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_AirVent), "HydroponicVent")]
	public class Vent : FanAttachedBlock { }
	public class FanAttachedBlock : MyGameLogicComponent
	{
		private MyObjectBuilder_EntityBase objectBuilder;
		private bool first = true;
		private IMyEntity fan = null;
		private float angle = 0;
		private float spin = SPIN_OFF;
		private int skip = 0;
		private int lodDistSq = 0;
		private Vector3 lastColor = Vector3.Zero;
		private const float SPIN_OFF = 0;
		private const float SPIN_REVERSE = -4f;
		private const float SPIN_THRUST = 4f;
		private const float SPINUP_STEP = 0.2f;
		private const float SPINDOWN_STEP = 0.2f;
		private const float SPINDOWN_REVERSE_STEP = -0.2f;
		class BlockFanData
		{
			public string fanModel;
			public int lodDistance;
			public bool flipDirection = false;
			public Vector3? offset = null;
			public Base6Directions.Direction? tiltAxis = null;
			public float tiltAngle;
			public BlockFanData() { }
		}
		private static readonly Dictionary<string, BlockFanData> blockFanData = new Dictionary<string, BlockFanData>()
		{
			{ "HydroponicVent", new BlockFanData()
				{
					fanModel = "Hydroponic_Vent_Fan",
					lodDistance = 500,
					tiltAxis = Base6Directions.Direction.Down,
					tiltAngle = 0,
					offset = new Vector3(0f, 0f, -0.4f), // Up/Down, Left/Right, Forwards/Backwards in Meters.
				}
			},
		};
		public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{
			this.objectBuilder = objectBuilder;
			Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
		}
		public override void UpdateAfterSimulation()
		{
			try
			{
				if(MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated)
					return; // no reason to run this on a dedicated server's side
				var block = Entity as IMyCubeBlock;
				var data = blockFanData[block.BlockDefinition.SubtypeId];
				if(first)
				{
					first = false;
					lodDistSq = data.lodDistance * data.lodDistance;
				}
				if(!block.IsFunctional || Vector3D.DistanceSquared(block.WorldMatrix.Translation, MyAPIGateway.Session.Camera.WorldMatrix.Translation) > lodDistSq)
				{
					RemoveFan();
					return;
				}
				if(fan == null)
				{
					fan = SpawnPrefab(data.fanModel);
					if(fan == null)
						return;
				}
				// sync fan color
				if(++skip % 30 == 0 && Vector3.DistanceSquared(lastColor, block.Render.ColorMaskHsv) > 0)
				{
					skip = 0;
					(fan as IMyCubeGrid).ColorBlocks(Vector3I.Zero, Vector3I.Zero, block.Render.ColorMaskHsv);
					lastColor = block.Render.ColorMaskHsv;
				}
				if(block is Ingame.IMyAirVent)
				{
					var airVent = block as Ingame.IMyAirVent;
					if(airVent.IsWorking)
					{
						if(airVent.IsDepressurizing == true)
						{
							spin = Math.Min(spin + SPINUP_STEP, SPIN_THRUST);
						}
						else if(airVent.IsDepressurizing == false)
						{
							spin = Math.Max(spin - SPINDOWN_STEP, SPIN_REVERSE);
						}
					}
					else
					{
						if(airVent.IsDepressurizing == true)
						{
							spin = Math.Max(spin - SPINDOWN_STEP, SPIN_OFF);
						}
						else if(airVent.IsDepressurizing == false)
						{
							spin = Math.Min(spin + SPINUP_STEP, SPIN_OFF);
						}
					}
				}
				var matrix = block.WorldMatrix;
				angle -= spin;
				if(data.tiltAxis.HasValue)
				{
					var axis = block.WorldMatrix.GetDirectionVector(data.tiltAxis.Value);
					matrix *= MatrixD.CreateFromAxisAngle(axis, MathHelper.ToRadians(data.tiltAngle));
				}
				matrix *= MatrixD.CreateFromAxisAngle(matrix.Forward, MathHelper.ToRadians(angle));
				matrix.Translation = block.WorldMatrix.Translation;
				if(data.offset.HasValue)
				{
					matrix.Translation += block.WorldMatrix.Left * data.offset.Value.X + block.WorldMatrix.Up * data.offset.Value.Y + block.WorldMatrix.Forward * data.offset.Value.Z;
				}
				fan.SetWorldMatrix(matrix);
			}
			catch(Exception e)
			{
				MyLog.Default.WriteLineAndConsole(e.ToString());
				MyAPIGateway.Utilities.ShowNotification(e.Message, 3000, MyFontEnum.Red);
			}
		}
		private void RemoveFan()
		{
			if(fan != null)
			{
				fan.Close();
				fan = null;
			}
		}
		public override void Close()
		{
			RemoveFan();
		}
		public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
		{
			return copy ? (MyObjectBuilder_EntityBase)objectBuilder.Clone() : objectBuilder;
		}
		private IMyEntity SpawnPrefab(string name)
		{
			try
			{
				PrefabBuilder.CubeBlocks[0].SubtypeName = name;
				MyAPIGateway.Entities.RemapObjectBuilder(PrefabBuilder);
				var ent = MyAPIGateway.Entities.CreateFromObjectBuilder(PrefabBuilder);
				ent.Flags &= ~EntityFlags.Sync; // don't sync on MP
				ent.Flags &= ~EntityFlags.Save; // don't save this entity
				MyAPIGateway.Entities.AddEntity(ent, true);
				return ent;
			}
			catch(Exception e)
			{
				MyLog.Default.WriteLineAndConsole(e.ToString());
				MyAPIGateway.Utilities.ShowNotification(e.Message, 3000, MyFontEnum.Red);
			}
			return null;
		}
		private static SerializableVector3 PrefabVector0 = new SerializableVector3(0,0,0);
		private static SerializableVector3I PrefabVectorI0 = new SerializableVector3I(0,0,0);
		private static SerializableBlockOrientation PrefabOrientation = new SerializableBlockOrientation(Base6Directions.Direction.Forward, Base6Directions.Direction.Up);
		private static MyObjectBuilder_CubeGrid PrefabBuilder = new MyObjectBuilder_CubeGrid()
		{
			EntityId = 0,
			GridSizeEnum = MyCubeSize.Small,
			IsStatic = true,
			Skeleton = new List<BoneInfo>(),
			LinearVelocity = PrefabVector0,
			AngularVelocity = PrefabVector0,
			ConveyorLines = new List<MyObjectBuilder_ConveyorLine>(),
			BlockGroups = new List<MyObjectBuilder_BlockGroup>(),
			Handbrake = false,
			XMirroxPlane = null,
			YMirroxPlane = null,
			ZMirroxPlane = null,
			PersistentFlags = MyPersistentEntityFlags2.InScene,
			Name = "",
			DisplayName = "",
			CreatePhysics = false,
			PositionAndOrientation = new MyPositionAndOrientation(Vector3D.Zero, Vector3D.Forward, Vector3D.Up),
			CubeBlocks = new List<MyObjectBuilder_CubeBlock>()
			{
				new MyObjectBuilder_TerminalBlock()
				{
					EntityId = 1,
					SubtypeName = "",
					Min = PrefabVectorI0,
					BlockOrientation = PrefabOrientation,
					ShareMode = MyOwnershipShareModeEnum.None,
					DeformationRatio = 0,
					ShowOnHUD = false,
				}
			}
		};
	}
}