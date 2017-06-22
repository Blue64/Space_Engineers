using Sandbox.Engine;
using Sandbox.Engine.Multiplayer;

using Sandbox.ModAPI;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;

using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

using System;
using System.Collections.Generic;
using System.Text;

using VRage.Game.ObjectBuilders.Definitions;

namespace IndustrialAutomaton.CloakingDevice
{

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, new string[] { "CloakingDevice_Large", "CloakingDevice_Small" })]
    public class CloakingDevice : MyGameLogicComponent
    {

		private bool _isInit;
		private IMyTerminalBlock m_block;
		private int _phase=0;
		private int _timeOut;
		private IMyCubeGrid _grid;
		private List<IMyFunctionalBlock> _myGunList = new List<IMyFunctionalBlock>();
		private List<Sandbox.ModAPI.Ingame.IMyLargeTurretBase> _gunList = new List<Sandbox.ModAPI.Ingame.IMyLargeTurretBase>();
		private List<IMyEntity> _entities = new List<IMyEntity>();
		private List<IMySlimBlock> _slimBlocks = new List<IMySlimBlock>();
		private bool firstRun = true;

        private MyObjectBuilder_EntityBase _builder = null;
        public static MyDefinitionId Electricity = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
        protected IMyUpgradeModule _imyUM;
        private MyResourceSinkComponent _sink;
        public float _power;
		public float _mass;
		
		private static bool _AllowAITargeting;
		private static bool _PowerBasedOnMass;
		private static float _PowerMultiplier;
		private static float _SmallGridDivisor;
		
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Container.Entity.GetObjectBuilder(copy);
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
			if (_isInit) return;
			m_block = Container.Entity as IMyTerminalBlock;
			m_block.AppendingCustomInfo += appendCustomInfo;
			_imyUM = Entity as IMyUpgradeModule;
			if (!_imyUM.Components.TryGet(out _sink))
			{
				_sink = new MyResourceSinkComponent();
				MyResourceSinkInfo info = new MyResourceSinkInfo();
				info.ResourceTypeId = Electricity;
				_sink.AddType(ref info);
				_imyUM.Components.Add(_sink);
			}
			this.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
//	deprecated	Container.Entity.NeedsUpdate |=  MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }
		
		public class ConfigInfo
		{
			public bool AllowAITargeting;
			public bool PowerBasedOnMass;
			public float PowerMultiplier;
			public float SmallGridDivisor;
		}
		
		public static void loadXML()
		{
			if (MyAPIGateway.Utilities.FileExistsInLocalStorage("config.cfg", typeof(ConfigInfo)))
			try {
				var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage("config.cfg", typeof(ConfigInfo));
				var xmlText = reader.ReadToEnd();
				reader.Close();
				ConfigInfo fileData = MyAPIGateway.Utilities.SerializeFromXML<ConfigInfo>(xmlText);
				_AllowAITargeting=fileData.AllowAITargeting;
				_PowerBasedOnMass=fileData.PowerBasedOnMass;
				if (fileData.PowerMultiplier!=0) _PowerMultiplier=fileData.PowerMultiplier;
				if (fileData.SmallGridDivisor!=0) _SmallGridDivisor=fileData.SmallGridDivisor;
			} catch (Exception ex) {}
		}

		public static void saveXML()
        {
			ConfigInfo fileData = new ConfigInfo();
			fileData.AllowAITargeting=_AllowAITargeting;
			fileData.PowerBasedOnMass=_PowerBasedOnMass;
			fileData.PowerMultiplier=_PowerMultiplier;
			fileData.SmallGridDivisor=_SmallGridDivisor;
			var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage("config.cfg", typeof(ConfigInfo));
			writer.Write(MyAPIGateway.Utilities.SerializeToXML(fileData));
			writer.Flush();
			writer.Close();
        }

		public override void Close()
		{
			if (firstRun) return;
			foreach (var block in _slimBlocks) try { block.Dithering = 0f; } catch (Exception ex) {}
			foreach (var ent in _entities)
			try {
				ent.Render.Transparency = 0f;
				ent.Render.RemoveRenderObjects();
				ent.Render.AddRenderObjects();
			} catch (Exception ex) {}
			foreach (var slim in _slimBlocks) try { slim.Dithering=0f; } catch (Exception ex) {}
			try { _grid.Render.Visible=true; } catch (Exception ex) {}
			_entities.Clear();
			_slimBlocks.Clear();
		}		

        public override void UpdateOnceBeforeFrame()
        {
            this.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            this.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            this.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
//            Container.Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
//            Container.Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
//            Container.Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
		}

        bool IsWorking()
        {
            if(_imyUM.Closed || _imyUM.MarkedForClose)
            {
                MarkForClose();
                return false;
            }
			if (!_sink.IsPowerAvailable(Electricity, _power) && _imyUM.Enabled) 
			{
				_timeOut=0;
				_imyUM.Enabled=false;
			}
            return (_imyUM.IsFunctional && _imyUM.Enabled);
        }

		public void appendCustomInfo(Sandbox.ModAPI.IMyTerminalBlock block, StringBuilder info)
		{
			info.Clear();
			info.AppendLine("Type: " + m_block.DefinitionDisplayNameText);
			info.AppendLine("Required input: " + _power.ToString("N") + " MW");
			info.AppendLine("Current input: " + _sink.CurrentInputByType(Electricity).ToString("N") + "MW");
		}

        void InitResourceSink()
        {
			_sink.SetMaxRequiredInputByType(Electricity, _power);
			_sink.SetRequiredInputFuncByType(Electricity, PowerConsumptionFunc);
			_sink.SetRequiredInputByType(Electricity, PowerConsumptionFunc());
			_sink.Update();
        }

        float PowerConsumptionFunc()
        {
			if (!IsWorking()) return 0f;
			return _power;
        }
		
        public override void UpdateBeforeSimulation()
        {
			if (!_isInit) return;
			if (IsWorking() || _timeOut>0)
				foreach (var gun in _myGunList)
					gun.Enabled=false;
			if (!IsWorking() && _gunList.Count>0)
			{
				foreach (var gun in _gunList)
				{
					gun.Enabled=true;
					gun.ResetTargetingToDefault();
				}
				_gunList.Clear();
			}
		}
		
        public override void UpdateBeforeSimulation100()
		{

		if (!_isInit)
			try {				
				_AllowAITargeting=true;
				_PowerBasedOnMass=false;
				_PowerMultiplier=1.0f;
				_SmallGridDivisor=8.0f;
				loadXML();
				saveXML();
				var f_block = Container.Entity as IMyFunctionalBlock;
				f_block.Enabled=false;
				InitResourceSink();
				_grid = m_block.CubeGrid as VRage.Game.ModAPI.IMyCubeGrid;
				if (_grid.GridSizeEnum==(byte)0) _SmallGridDivisor=1f; 
				_mass = _grid.Physics.Mass;
				_power = 300f * _PowerMultiplier / _SmallGridDivisor;
				if (_PowerBasedOnMass) _power *= (_mass/500000f);
				m_block.RefreshCustomInfo();		
				_isInit=true;
			} catch (Exception ex) { return; }
			
			if (_PowerBasedOnMass && _grid.Physics.Mass!=_mass)
			{	
				_mass = _grid.Physics.Mass;
				_power = 300f * _PowerMultiplier * (_mass/500000f) / _SmallGridDivisor;
				m_block.RefreshCustomInfo();
			}
			if (!IsWorking()) return;
			if (firstRun) firstRun=false;
			_myGunList.Clear();
			List<IMySlimBlock> blockList = new List<IMySlimBlock>();
			_grid.GetBlocks(blockList);
			foreach (var block in blockList) if (block.FatBlock is IMyUserControllableGun)
			{
					var gun = block.FatBlock as IMyFunctionalBlock;
					if (gun!=null) _myGunList.Add(gun);
			}
		}

        public override void UpdateBeforeSimulation10()
        {
			if (!_isInit) return;			
			if (IsWorking() && !_AllowAITargeting)
			{
				foreach (var gun in _gunList)
				{
					gun.Enabled=true;
					gun.ResetTargetingToDefault();
				}
				_gunList.Clear();
				Vector3D gridPos = _grid.Physics.CenterOfMassWorld;
				BoundingSphereD sphere = new BoundingSphereD(gridPos, 1000f);
				List<IMyEntity> entList = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
				foreach (var ent in entList)
				{
					var cube = ent as IMyCubeBlock;
					if (cube==null) continue;
					var obj = cube.GetObjectBuilderCubeBlock(false);
					if (obj==null) continue;
					var turret = obj as MyObjectBuilder_TurretBase;
					if (turret==null) continue;
					IMyEntity target;
					MyAPIGateway.Entities.TryGetEntityById(turret.Target, out target);
					if (target==null) continue; 
					cube = target as IMyCubeBlock;
					if (cube==null) continue;
					var grid = cube.CubeGrid;
					if (grid==null || grid!=_grid) continue;
					var fblock = ent as Sandbox.ModAPI.Ingame.IMyLargeTurretBase;
					if (fblock==null) continue;
					_gunList.Add(fblock);
					fblock.Enabled=false;
					fblock.Azimuth=0;
					fblock.Elevation=0;
					fblock.ResetTargetingToDefault();
				}
			}			

			if (_timeOut>0) _timeOut--;
			
			foreach (var slim in _slimBlocks) try {
				if (slim.CubeGrid!=_grid)
					slim.Dithering=0f;
			} catch (Exception ex) {}
			foreach (var ent in _entities) try {
				var block = ent as IMyCubeBlock;
				if (block.CubeGrid!=_grid) {
					ent.Render.Transparency = 0f;
					ent.Render.RemoveRenderObjects();
					ent.Render.AddRenderObjects();
				}
			} catch (Exception ex) {}
			
			if (_phase==0 && !IsWorking()) return;
			if ((_phase==-20 && IsWorking()) || _timeOut>0) return;			
			
			_sink.Update();
			m_block.RefreshCustomInfo();
			if (IsWorking()) _phase--; else _phase++;
			float transparency = (float)_phase/20f;
			
			foreach (var ent in _entities)
			{
				ent.Render.Transparency = 0f;
				ent.Render.RemoveRenderObjects();
				ent.Render.AddRenderObjects();
			}
			
			_entities.Clear();
			_slimBlocks.Clear();
			_grid.GetBlocks(_slimBlocks);
			
			foreach (var block in _slimBlocks) {
				block.Dithering = transparency;
				var ent = block.FatBlock as MyEntity;
				if (ent!=null) addEntities(ent);
				var motor = block.FatBlock as IMyMotorBase;
				if (motor!=null) {
					var rotor = motor.Rotor.SlimBlock;
					if (rotor!=null) rotor.Dithering = transparency;
				}
				var piston = block.FatBlock as IMyPistonBase;
				if (piston!=null) {
					var top = piston.Top.SlimBlock;
					if (top!=null) top.Dithering = transparency;
				}
			}

			foreach (var ent in _entities)
			{
				ent.Render.Transparency = transparency;
				ent.Render.RemoveRenderObjects();
				ent.Render.AddRenderObjects();
			}
		
			if (_phase==0 || _phase==-20)
			{
				_timeOut=30;
				_grid.Render.Visible=(_phase==0);
			} 
		}
		
		public void addEntities(MyEntity ent)
		{
			if (ent.Subparts==null) return;
			foreach (var part in ent.Subparts)
			{
				_entities.Add(part.Value);
				addEntities(part.Value);
			}
		}
		
    }
}