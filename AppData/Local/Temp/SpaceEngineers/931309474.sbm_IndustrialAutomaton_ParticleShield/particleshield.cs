namespace IndustrialAutomaton.ParticleShield
{
	using ProtoBuf;
	using System;
	using System.Collections.Generic;
	using System.Text;
	using Sandbox.Common.ObjectBuilders;
	using Sandbox.Game.Entities;
	using Sandbox.Game.EntityComponents;
	using Sandbox.ModAPI;
	using Sandbox.ModAPI.Interfaces.Terminal;	
	using SpaceEngineers.Game.ModAPI;
	using VRage.Game;
	using VRage.Game.ModAPI;
	using VRageMath;
	using VRage.ObjectBuilders;
	using VRage.Game.ObjectBuilders.Definitions;
	using VRage.Game.Components;
	using VRage.ModAPI;
	using VRage.Utils;

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), true, new string[] { "ParticleShield" })]
    public class ParticleShield : MyGameLogicComponent
    {

#region Initialisation & loop

		private ushort _modID = 50004;				
        private IMyTerminalBlock _tblock;
        private MyResourceSinkComponent _sink;
        private static MyDefinitionId Electricity = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
		public float _range = 0f;
		public float _maxrange { get; set; }
		private float _power = 200f;
		private bool _wasOn = true;
        protected IMyUpgradeModule _ublock;
		private int _count= 0;
        private static Random _random = new Random();
        private bool _isInit=false;
		private bool _hasControls=false;
		public static readonly Dictionary<long, ParticleShield> Shields = new Dictionary<long, ParticleShield>();
				
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
			if (_isInit) return;
			Container.Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
			_tblock = Container.Entity as IMyTerminalBlock;
			_tblock.AppendingCustomInfo += appendCustomInfo;			
			_ublock = Entity as IMyUpgradeModule;
			_maxrange=1000f;
			if (!_ublock.Components.TryGet(out _sink))
			{
				_sink = new MyResourceSinkComponent();
				MyResourceSinkInfo info = new MyResourceSinkInfo();
				info.ResourceTypeId = Electricity;
				_sink.AddType(ref info);
				_ublock.Components.Add(_sink);
			}
			Shields.Add(Entity.EntityId, this);
			_isInit=true;
		}
        
        public override void UpdateOnceBeforeFrame()
        {
			Container.Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
			Container.Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
			InitResourceSink();			
        }

        public override void UpdateBeforeSimulation()
        {
            if (IsWorking())
            {
				if (_range<_maxrange && _count==30)
				{
					_range+=(_maxrange/20f);
					_tblock.SetEmissiveParts("Emissive0", Color.Green, (_range/_maxrange));
				} else {
					_tblock.SetEmissiveParts("Emissive0", Color.Green, 1.0f);
				}
				if (!MyAPIGateway.Utilities.IsDedicated) showRange(_range);
				else sendPoke(_range);
				if (_count++==60) gridEffects();
            } else if (_wasOn) {
				_tblock.SetEmissiveParts("Emissive0", Color.DarkRed, 0.20f);
				_range=0f;
			}

			if (IsWorking()!=_wasOn && _ublock.Components.TryGet(out _sink))
			{
				_sink.Update();
				_tblock.RefreshCustomInfo();
				_wasOn=IsWorking();
			}
		}
		
		public override void UpdateBeforeSimulation100()
		{
			try {
				if (!ParticleShieldBase.ControlsLoaded) CreateControls();
			} catch (Exception ex) {}
		}
		
		public override void Close()
		{
			try {
				List<ParticleShieldBase.SafeGrid> safeGrids = new List<ParticleShieldBase.SafeGrid>();
				foreach (var safeGrid in ParticleShieldBase.SafeGrids)
					if (safeGrid.shield!=_tblock) safeGrids.Add(safeGrid);
				ParticleShieldBase.SafeGrids.Clear();
				foreach (var grid in safeGrids)
					ParticleShieldBase.SafeGrids.Add(grid);	
			} catch (Exception ex) {}
		}
		
#endregion

#region Controls

		public static bool IsShield(IMyTerminalBlock block)
		{
			return (Shields.ContainsKey(block.EntityId));
		}
		public void RangeSetter(IMyTerminalBlock block, float setting)
		{
			long ID = block.EntityId;
			if (IsShield(block)) Shields[ID]._maxrange=setting;
		}
		public float RangeGetter(IMyTerminalBlock block)
		{
			long ID = block.EntityId;
			if (IsShield(block))
				return Shields[ID]._maxrange;
			return 1000f;
		}
		public void RangeWriter(IMyTerminalBlock block, StringBuilder info)
		{
			long ID = block.EntityId;
			if (IsShield(block))
			{
				info.Clear();
				info.Append("" + Math.Round(Shields[ID]._maxrange,2) + "m");
			}
		}
		public void CreateControls()
		{
			ParticleShieldBase.ControlsLoaded = true;			
			if (!MyAPIGateway.Utilities.IsDedicated)
			{
				var rangeSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, Sandbox.ModAPI.Ingame.IMyUpgradeModule>("Range");
				rangeSlider.Enabled = block => IsShield(block);
				rangeSlider.Visible = block => IsShield(block);
				rangeSlider.SetLimits(100f, 50000f);
				rangeSlider.Setter = RangeSetter;
				rangeSlider.Getter = RangeGetter;
				rangeSlider.Writer = RangeWriter;
				rangeSlider.Title = MyStringId.GetOrCompute("Shield Radius");
				MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyUpgradeModule>(rangeSlider);
			}
		}		

#endregion

#region Power

        private bool IsWorking()
        {
            if(_ublock.Closed || _ublock.MarkedForClose)
            {
                MarkForClose();
                return false;
            }
            return (_ublock.IsFunctional && _ublock.Enabled && _sink.IsPowerAvailable(Electricity, _maxrange/5f));
        }

		public void appendCustomInfo(Sandbox.ModAPI.IMyTerminalBlock block, StringBuilder info)
		{
			info.Clear();
			info.AppendLine("Type: " + _tblock.DefinitionDisplayNameText);
			info.AppendLine("Required input: " + (_maxrange/5f).ToString("N") + " MW");
			info.AppendLine("Current input: " + _sink.CurrentInputByType(Electricity).ToString("N") + "MW");
		}

        private void InitResourceSink()
        {
			_sink.SetMaxRequiredInputByType(Electricity, 10000f);
			_sink.SetRequiredInputFuncByType(Electricity, PowerConsumptionFunc);
			_sink.SetRequiredInputByType(Electricity, PowerConsumptionFunc());
			_sink.Update();
        }

        private float PowerConsumptionFunc()
        {
			try {
				if (!IsWorking()) return 0f;
				return _maxrange/5f;
			} catch (Exception ex) { return 0f; }
        }

#endregion

#region Server-client comms

        [ProtoContract(UseProtoMembersOnly = true)]
        public class Poke
        {
          [ProtoMember(1)]
          public ushort ModID;
          [ProtoMember(2)]
          public float Size { get; set; }
        }

        public void sendPoke(float size)
        {
            bool sent;
            Poke info = new Poke();
            info.ModID =_modID;
            info.Size = size;
            sent = MyAPIGateway.Multiplayer.SendMessageToOthers(_modID, MyAPIGateway.Utilities.SerializeToBinary(info), true);
        }
        
        public void getPoke(byte[] data)
        {
            var message = MyAPIGateway.Utilities.SerializeFromBinary<Poke>(data);
            Poke info = new Poke();
            try
            {
                info = message;
                if (info.ModID==_modID)
				{
					showRange(info.Size);
				}
            }
            catch (Exception ex) {}
        }				

#endregion

#region Graphic effect

		public void showRange(float size)
		{
			Color colour;
			var relations = _tblock.GetUserRelationToOwner(MyAPIGateway.Session.Player.PlayerID);
			if (relations==MyRelationsBetweenPlayerAndBlock.Owner || relations==MyRelationsBetweenPlayerAndBlock.FactionShare)
				colour = Color.FromNonPremultiplied(16, 255, 32, 64);
			else
				colour = Color.FromNonPremultiplied(255, 96, 16, 64);
			MyStringId RangeGridResourceId = MyStringId.GetOrCompute("Build new");
			var matrix = _tblock.WorldMatrix;
			MySimpleObjectDraw.DrawTransparentSphere(ref matrix, size, ref colour, MySimpleObjectRasterizer.Solid, 20, null, RangeGridResourceId, 0.25f, -1);
		}

#endregion

#region Grid damage effects

		public void gridEffects()
		{
			_count=0;
			List<IMyCubeGrid> safeList = new List<IMyCubeGrid>();
			safeList.Add(_tblock.CubeGrid);
			var pos = _tblock.CubeGrid.GridIntegerToWorld(_tblock.Position);
			BoundingSphereD sphere = new BoundingSphereD(pos, _range*0.98f);
			List<IMyEntity> entList = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
			BoundingSphereD inside = new BoundingSphereD(pos, _range*0.90f);
			List<IMyEntity> insideList = MyAPIGateway.Entities.GetEntitiesInSphere(ref inside);			
			foreach (var ent in entList)
			try {
				if (ent==null) continue;
				if (ent is IMyCharacter && !insideList.Contains(ent))
				{
					var dude = MyAPIGateway.Players.GetPlayerControllingEntity(ent).PlayerID;
					if (dude==null) continue;
					var relationship = _tblock.GetUserRelationToOwner(dude);
					if (relationship!=MyRelationsBetweenPlayerAndBlock.Owner && relationship!=MyRelationsBetweenPlayerAndBlock.FactionShare)
						try { (ent as IMyCharacter).Kill(); } catch (Exception ex) {}
					continue;
				}
				var grid = ent as IMyCubeGrid;
				if (grid==null || grid==_tblock.CubeGrid) continue;
				List<long> owners = grid.BigOwners; if (owners.Count==0) continue;
				var relations = _tblock.GetUserRelationToOwner(owners[0]);
				if (relations==MyRelationsBetweenPlayerAndBlock.Owner || relations==MyRelationsBetweenPlayerAndBlock.FactionShare)
				{				
					if (!safeList.Contains(grid)) safeList.Add(grid);
					continue;
				}
				if (insideList.Contains(ent)) continue;
				var vel = grid.Physics.LinearVelocity;
				vel.SetDim(0, (int)((float)vel.GetDim(0)*0.6f));
				vel.SetDim(1, (int)((float)vel.GetDim(1)*0.6f));
				vel.SetDim(2, (int)((float)vel.GetDim(2)*0.6f));
				grid.Physics.LinearVelocity=vel;
				List<IMySlimBlock> victimList = new List<IMySlimBlock>();
				grid.GetBlocks(victimList);
				foreach (var block in victimList)
				{
					if (block==null) continue;
					var victim = block as IMyDestroyableObject;
					if (victim==null) continue;
					float damage = (float)(_random.Next(1,21)+30f)*(grid.Physics.Speed+5f);
					if (block is IMyFunctionalBlock) damage*=500f;
					victim.DoDamage(damage, MyDamageType.Fire, false);
				}
			} catch (Exception ex) {}
			try 
			{
				List<ParticleShieldBase.SafeGrid> safeGrids = new List<ParticleShieldBase.SafeGrid>();
				foreach (var safeGrid in ParticleShieldBase.SafeGrids)
					if (safeGrid.shield!=_tblock) safeGrids.Add(safeGrid);					
				ParticleShieldBase.SafeGrids.Clear();
				foreach (var grid in safeGrids) ParticleShieldBase.SafeGrids.Add(grid);
				foreach (var grid in safeList)
				{
					ParticleShieldBase.SafeGrid safeGrid = new ParticleShieldBase.SafeGrid();
					safeGrid.grid=grid;
					safeGrid.shield=_tblock;
					if (!ParticleShieldBase.SafeGrids.Contains(safeGrid)) ParticleShieldBase.SafeGrids.Add(safeGrid);
				}
			} catch (Exception ex) { }
		}

#endregion
		
	}
	
#region Session components - grid protection

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class ParticleShieldBase : MySessionComponentBase
    {

        public static bool _isInit;
        public class SafeGrid
		{
			public IMyCubeGrid grid;
			public IMyTerminalBlock shield;
		}
		public static List<SafeGrid> SafeGrids;
		public static bool ControlsLoaded;
		
        // Initialisation

        public override void UpdateBeforeSimulation()
        {
            if (_isInit) return;
            if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated) Init();
            else if (MyAPIGateway.Session.Player!=null) Init();
        }

        public static void Init()
        {
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, checkDamage);
			SafeGrids = new List<SafeGrid>();
            _isInit=true;
        }

        // Mod actions
    
        public static void checkDamage(object block, ref MyDamageInformation info)
        {
            if (info.Type!=MyDamageType.Bullet && info.Type!=MyDamageType.Rocket) return;
            var slimBlock = block as IMySlimBlock;				if (slimBlock==null) return;
            var grid = slimBlock.CubeGrid as IMyCubeGrid;       if (grid==null) return;
			foreach (var safeGrid in SafeGrids)	if (safeGrid.grid==grid) info.Amount=0f;
        }
    }

#endregion
	
}