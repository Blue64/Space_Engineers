using System.Collections.Generic;
using VRage.ModAPI;
using Sandbox.ModAPI;
using System;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.Components;
using VRage.Game;

namespace Parachute
{
	[MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
	public class CoreParachute : MySessionComponentBase
	{
		public static CoreParachute instance;
		//public ParaSettings settings;
		public Dictionary<long, MyPlanet> planets = new Dictionary<long, MyPlanet>();
		public float large_max = 104.4f;
		public float small_max = 104.4f;
		int resolution = 1;
		bool init = false;
		bool isServer = false;
		bool isMultiplayer = false;
		bool isDedicated = false;
		internal static Action updateChute;

		//static event updateChute;
		public DebugLevel debug
		{
			set { Log.debug = value; }
			get { return Log.debug; }
		}
		public override void UpdateAfterSimulation()
		{
			Log.DebugWrite(DebugLevel.Info, "Update()");
			Update();
		}
		private void Update()
		{

			if (!init)
			{
				//if(instance == null) instance = this;
				if (MyAPIGateway.Session == null)
					return;
				if (MyAPIGateway.Multiplayer == null && MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE)
					return;
				Init();
			}
			if (MyAPIGateway.Session == null)
			{
				unload();
			}

			if(updateChute != null)
				updateChute();
			if (resolution % 20000 == 0 || (planets.Count == 0 && resolution % 60 == 0)) // mod should only be run on a map with planets, otherwise whats the point?
			{
				Log.DebugWrite(DebugLevel.Info, "Scanning for planets");
				HashSet<IMyEntity> ents = new HashSet<IMyEntity>();
				if (MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed > 100f)
					small_max = MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed;
				if (MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed > 100f)
					large_max = MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed;
				MyAPIGateway.Entities.GetEntities(ents, delegate (IMyEntity e)
				{
					if (e is MyPlanet)
					{
						if (!planets.ContainsKey(e.EntityId))
							planets.Add(e.EntityId, e as MyPlanet);
					}

					return false; // no reason to add to the list
				});
				Log.DebugWrite(DebugLevel.Info, string.Format("Found {0} planets.", planets.Count));
				resolution = 1;
			}
			else
				resolution++;
		}
		protected override void UnloadData()
		{
			unload();
		}
		public void unload()
		{
			Log.DebugWrite(DebugLevel.Info, "Closing Parachute Mod.");
			Log.Info("");
			init = false;
			isServer = false;
			isDedicated = false;
			Log.DebugWrite(DebugLevel.Info, "Closed.");
			Log.Close();
		}
		public void Init()
		{
			if (init) return;//script already initialized, abort.

			instance = this;
			Log.init = true;
			Log.DebugWrite(DebugLevel.Info, "Initialized");
			
			debug = DebugLevel.None;
			init = true;
			isServer = MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE || MyAPIGateway.Multiplayer.IsServer;
			isDedicated = (MyAPIGateway.Utilities.IsDedicated && isServer);


		}
	}
}
