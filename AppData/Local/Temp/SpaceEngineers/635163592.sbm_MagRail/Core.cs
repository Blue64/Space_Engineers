using System;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game;

namespace MagRails
{
	[MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
	public class CoreMagRails : MySessionComponentBase
	{
		public static CoreMagRails instance;
		public bool init = false;
		public bool isServer = false;
		public bool isMultiplayer = false;
		public bool isDedicated = false;
		public RailDictionary Definitions = new RailDictionary();
		internal static Action UpdateHook;
		public bool entityHandlerInit = false;

		private DebugLevel _debuglevel = DebugLevel.None;

		public DebugLevel debug
		{
			get { return Log._DebugLevel; }
			set { Log._DebugLevel = value; }
		}
		/// <summary>
		/// Called every game update.
		/// </summary>
		public override void UpdateAfterSimulation()
		{
			Update();
		}
		/// <summary>
		/// Called when game shuts down, cleans up handlers.
		/// </summary>
		protected override void UnloadData()
		{
			unload();
		}
		



		private void Update()
		{
			if (!init)
			{
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
			if (UpdateHook != null)
				UpdateHook();//apply physics to the grids, some reason doing the work here syncs up with the game rendering. Who knew. 
		}




		public void Init()
		{
			if (init) return;//script already initialized, abort.
			Log.running = true;
			Log._DebugLevel = _debuglevel;
			Log.Info("Initialized");

			instance = this;
			Definitions = new RailDictionary();//new defs

			init = true;
			isServer = MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE || MyAPIGateway.Multiplayer.IsServer;
			isDedicated = (MyAPIGateway.Utilities.IsDedicated && isServer);
			if (isDedicated) return;
		}
		public void unload()
		{
			Log.Info("Closing MagRail Mod.");
			if (init && !isDedicated)
			{
				init = false;
			}

			isServer = false;
			isDedicated = false;
			UpdateHook = null; //dump;
			Log.running = false;
			Log.Info("Closed.");
			Log.Close();
		}
	}
}
