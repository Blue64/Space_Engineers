namespace IndustrialAutomaton.MaintenanceAstromech
{
   using System;
   using System.Collections.Generic;
   using System.Text;

   using VRage;
   using VRage.Game.Components;
   using VRage.Game;
   using VRage.ObjectBuilders;
   using VRage.ModAPI;
   using VRage.Game.ModAPI;
   using VRage.Utils;
   using VRageMath;

   using Sandbox.ModAPI;
   using Sandbox.Common.ObjectBuilders;
   using Sandbox.Game.Entities;
   using Sandbox.Game.Lights;
   using Sandbox.ModAPI.Ingame;
   using Sandbox.Definitions;

   using SpaceEquipmentLtd.Utils;

   using IMyShipWelder = Sandbox.ModAPI.IMyShipWelder;
   using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

   [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ShipWelder), false, "Big_Astromech_Maintenance","Big_Astromech_Maintenance_r5","Big_Astromech_Maintenance_imperial","Big_Astromech_Maintenance_r5_imperial", "Small_Astromech_Maintenance","Small_Astromech_Maintenance_r5","Small_Astromech_Maintenance_imperial","Small_Astromech_Maintenance_r5_imperial")]
   public class NanobotBuildAndRepairSystemBlock : MyGameLogicComponent
   {
      private enum WorkingState
      {
         NotReady = 0, Idle = 1, Welding = 2, NeedWelding = 3, MissingComponents = 4, Grinding = 5
      }

      public static readonly int WELDER_RANGE_DEFAULT_IN_M = 25;
      public static readonly int WELDER_RANGE_MAX_IN_M = 1000;
      public static readonly int WELDER_RANGE_MIN_IN_M = 1;
      public static readonly float WELDING_GRINDING_MULTIPLIER_MIN = 0.001f;
      public static readonly float WELDING_GRINDING_MULTIPLIER_MAX = 1000f;

      public static readonly float WELDER_REQUIRED_ELECTRIC_POWER_STANDBY = 0.02f / 1000; //20W
      public static readonly float WELDER_REQUIRED_ELECTRIC_POWER_WELDING_DEFAULT = 2.0f / 1000; //2kW
      public static readonly float WELDER_REQUIRED_ELECTRIC_POWER_GRINDING_DEFAULT = 1.5f / 1000; //1.5kW
      public static readonly float WELDER_REQUIRED_ELECTRIC_POWER_TRANSPORT_DEFAULT = 10.0f / 1000; //10kW
      public static readonly float WELDER_TRANSPORTSPEED_METER_PER_SECOND_DEFAULT = 20f;
      public static readonly float WELDER_TRANSPORTVOLUME_DIVISOR = 10f;
      public static readonly float WELDER_AMOUNT_PER_SECOND = 2f;
      public static readonly float WELDER_MAX_REPAIR_BONE_MOVEMENT_SPEED = 0.2f;
      public static readonly float GRINDER_AMOUNT_PER_SECOND = 4f;
      public static readonly float WELDER_SOUND_VOLUME = 2f;
      public static readonly MyDefinitionId ElectricityId = new MyDefinitionId(typeof(VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GasProperties), "Electricity");
      private static readonly MyStringId RangeGridResourceId = MyStringId.GetOrCompute("Build new");

      private static MySoundPair[] _Sounds = new [] { null, new MySoundPair("ToolPlayWeldIdle"), new MySoundPair("ToolLrgWeldMetal"), new MySoundPair("BlockModuleProductivity"), new MySoundPair("BlockModuleProductivity"), new MySoundPair("ToolLrgGrindMetal")};
      private const MyParticleEffectsIDEnum PARTICLE_EFFECT_WELDING1 = MyParticleEffectsIDEnum.Welder;
      private const MyParticleEffectsIDEnum PARTICLE_EFFECT_GRINDING1 = MyParticleEffectsIDEnum.AngleGrinder;
      private const MyParticleEffectsIDEnum PARTICLE_EFFECT_TRANSPORT1 = MyParticleEffectsIDEnum.Prefab_LeakingBiohazard;

      private IMyShipWelder _Welder;
      private IMyInventory _TransportInventory;
      private bool _IsInit;
      private List<IMyInventory> _PossibleSources = new List<IMyInventory>();
      private Dictionary<string, int> _TempMissingComponents  = new Dictionary<string, int>();
      private TimeSpan _LastFriendlyDamageCleanup;
      private TimeSpan _TransportTime = new TimeSpan(0);
      private TimeSpan _LastTransportStartTime = new TimeSpan(0);

      private MyEntity3DSoundEmitter _SoundEmitter;
      private MyEntity3DSoundEmitter _SoundEmitterWorking;
      private MyParticleEffect _ParticleEffectWorking1;
      private MyParticleEffect _ParticleEffectTransport1;
      private MyLight _LightEffect;
      private MyFlareDefinition _LightEffectFlareWelding;
      private MyFlareDefinition _LightEffectFlareGrinding;
      private Vector3 _EmitterPosition;

      private WorkingState _WorkingStateSet;
      private bool _TransportStateSet;
      private float _MaxTransportVolume;
      private WorkingState _WorkingState;
      private long _LastPossibleWeldTargetsHash;
      private long _LastPossibleGrindTargetsHash;
      private IMySlimBlock _CurrentTransportDestination;
      private IMySlimBlock _CurrentTransportSource;
      private int _ContinuouslyError;

      private SyncBlockSettings _Settings;
      internal SyncBlockSettings Settings {
         get
         {
            return _Settings != null ? _Settings : _Settings = SyncBlockSettings.Load(Entity, NanobotBuildAndRepairSystemMod.ModGuid, BuildPriority);
         }
      }

      private NanobotBuildAndRepairSystemPriorityHandling _BuildPriority = new NanobotBuildAndRepairSystemPriorityHandling();
      internal NanobotBuildAndRepairSystemPriorityHandling BuildPriority {
         get
         {
            return _BuildPriority;
         }
      }
      public IMyShipWelder Welder { get { return _Welder; } }

      private SyncBlockState _State = new SyncBlockState();
      public SyncBlockState State { get { return _State; } }

      public int WelderMaximumRange { get; private set; }
      public float WelderTransportSpeed { get; private set; }
      public float WelderMaximumRequiredElectricPowerWelding { get; private set; }
      public float WelderMaximumRequiredElectricPowerGrinding { get; private set; }
      public float WelderMaximumRequiredElectricPowerTransport { get; private set; }

      /// <summary>
      /// Currently friendly damaged blocks
      /// </summary>
      private Dictionary<IMySlimBlock, TimeSpan> _FriendlyDamage;
      public Dictionary<IMySlimBlock, TimeSpan> FriendlyDamage
      {
         get
         {
            return _FriendlyDamage != null ? _FriendlyDamage : _FriendlyDamage = new Dictionary<IMySlimBlock, TimeSpan>();
         }
      }

      internal int SourcesAndTargetsUpdateRun { get; set; }

      /// <summary>
      /// Initialize locical component
      /// </summary>
      /// <param name="objectBuilder"></param>
      public override void Init(MyObjectBuilder_EntityBase objectBuilder)
      {
         if (Mod.Log.ShouldLog(Logging.Level.Event)) Mod.Log.Write(Logging.Level.Event, "BuildAndRepairSystemBlock: Initializing");
         base.Init(objectBuilder);
         NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;

         if (Entity.GameLogic is MyCompositeGameLogicComponent)
         {
            if (Mod.Log.ShouldLog(Logging.Level.Event)) Mod.Log.Write(Logging.Level.Event, "BuildAndRepairSystemBlock: Init Entiy.Logic remove other mods from this entity");
            Entity.GameLogic = this;
         }

         _Welder = Entity as IMyShipWelder;
         _Welder.AppendingCustomInfo += AppendingCustomInfo;

         _SoundEmitter = new MyEntity3DSoundEmitter((VRage.Game.Entity.MyEntity)_Welder);
         _SoundEmitter.CustomMaxDistance = 30f;
         _SoundEmitter.CustomVolume = 2f;
         _SoundEmitterWorking = new MyEntity3DSoundEmitter((VRage.Game.Entity.MyEntity)_Welder);
         _SoundEmitterWorking.CustomMaxDistance = 30f;
         _SoundEmitterWorking.CustomVolume = 2f;

         _WorkingState = WorkingState.NotReady;

         if (Mod.Log.ShouldLog(Logging.Level.Event)) Mod.Log.Write(Logging.Level.Event, "BuildAndRepairSystemBlock {0}: Initialized", Logging.BlockName(_Welder, Logging.BlockNameOptions.None));
      }

      /// <summary>
      /// 
      /// </summary>
      public void SettingsChanged()
      {
         WelderMaximumRange = (int)Math.Ceiling(NanobotBuildAndRepairSystemMod.Settings.Range / (_Welder.BlockDefinition.SubtypeName.Contains("Big") ? 1f : 3f));
         WelderMaximumRequiredElectricPowerTransport = NanobotBuildAndRepairSystemMod.Settings.MaximumRequiredElectricPowerTransport / (_Welder.BlockDefinition.SubtypeName.Contains("Big") ? 1f : 3f);
         WelderMaximumRequiredElectricPowerWelding = NanobotBuildAndRepairSystemMod.Settings.Welder.MaximumRequiredElectricPowerWelding / (_Welder.BlockDefinition.SubtypeName.Contains("Big") ? 1f : 3f);
         WelderMaximumRequiredElectricPowerGrinding = NanobotBuildAndRepairSystemMod.Settings.Welder.MaximumRequiredElectricPowerGrinding / (_Welder.BlockDefinition.SubtypeName.Contains("Big") ? 1f : 3f);

         if (_Settings != null) _Settings.CheckLimits(this);

         var resourceSink = _Welder.Components.Get<Sandbox.Game.EntityComponents.MyResourceSinkComponent>();
         if (resourceSink != null)
         {
            var electricPowerTransport = WelderMaximumRequiredElectricPowerTransport;
            if ((NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedSearchModes & SearchModes.BoundingBox) == 0) electricPowerTransport /= 10;

            resourceSink.SetMaxRequiredInputByType(ElectricityId, WelderMaximumRequiredElectricPowerWelding + electricPowerTransport);
            resourceSink.SetRequiredInputFuncByType(ElectricityId, ComputeRequiredElectricPower);
         }

         var maxMultiplier = Math.Max(NanobotBuildAndRepairSystemMod.Settings.Welder.WeldingMultiplier, NanobotBuildAndRepairSystemMod.Settings.Welder.GrindingMultiplier);

         if (maxMultiplier>10) NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_10TH_FRAME;
         else NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;

         WelderTransportSpeed = maxMultiplier * WELDER_TRANSPORTSPEED_METER_PER_SECOND_DEFAULT * Math.Min(NanobotBuildAndRepairSystemMod.Settings.Range / WELDER_RANGE_DEFAULT_IN_M, 4.0f);

         var multiplier = Math.Max(WELDER_TRANSPORTVOLUME_DIVISOR, maxMultiplier);
         multiplier = Math.Min(1, multiplier);
         _MaxTransportVolume = (_TransportInventory.MaxVolume.RawValue * multiplier) / (1000000 * WELDER_TRANSPORTVOLUME_DIVISOR);
         if (Mod.Log.ShouldLog(Logging.Level.Event)) Mod.Log.Write(Logging.Level.Event, "BuildAndRepairSystemBlock {0}: Init Inventory Volume {1} MaxTransportVolume {2} Mode {3}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), (float)_TransportInventory.MaxVolume, _MaxTransportVolume, Settings.SearchMode);

         if (NanobotBuildAndRepairSystemMod.SettingsValid && !NanobotBuildAndRepairSystemTerminal.CustomControlsInit) NanobotBuildAndRepairSystemTerminal.InitializeControls();
      }

      /// <summary>
      /// 
      /// </summary>
      private void Init()
      {
         if (_IsInit) return;

         if (_Welder.SlimBlock.IsProjected())
         {
            if (Mod.Log.ShouldLog(Logging.Level.Event)) Mod.Log.Write(Logging.Level.Event, "BuildAndRepairSystemBlock {0}: Init Block is only projected -> exit", Logging.BlockName(_Welder, Logging.BlockNameOptions.None));
            NeedsUpdate = MyEntityUpdateEnum.NONE;
            return;
         }

         lock (NanobotBuildAndRepairSystemMod.BuildAndRepairSystems)
         {
            if (!NanobotBuildAndRepairSystemMod.BuildAndRepairSystems.ContainsKey(Entity.EntityId))
            {
               NanobotBuildAndRepairSystemMod.BuildAndRepairSystems.Add(Entity.EntityId, this);
            }
         }

         var welderInventory = _Welder.GetInventory(0);
         if (welderInventory == null) return;

         _TransportInventory = new Sandbox.Game.MyInventory((float)welderInventory.MaxVolume, welderInventory.Size, MyInventoryFlags.CanSend); //MaxVolume handled in Pick functions

         SettingsChanged();

         var dummies = new Dictionary<string, IMyModelDummy>();
         _Welder.Model.GetDummies(dummies);
         foreach (var dummy in dummies)
         {
            if (dummy.Key.ToLower().Contains("detector_emitter"))
            {
               var matrix = dummy.Value.Matrix;
               Matrix blockMatrix = _Welder.PositionComp.LocalMatrix;
               _EmitterPosition = Vector3.Transform(matrix.Translation, blockMatrix);
               break;
            }
         }

         NanobotBuildAndRepairSystemMod.SyncBlockDataRequestSend(this);
         UpdateCustomInfo();
         _IsInit = true;
         if (Mod.Log.ShouldLog(Logging.Level.Event)) Mod.Log.Write(Logging.Level.Event, "BuildAndRepairSystemBlock {0}: Init -> done", Logging.BlockName(_Welder, Logging.BlockNameOptions.None));
      }

      /// <summary>
      /// 
      /// </summary>
      /// <returns></returns>
      private float ComputeRequiredElectricPower()
      {
         if (_Welder == null) return 0f;
         var required = 0f;
         if (_Welder.Enabled)
         {
            if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: ComputeRequiredElectricPower Enabled", Logging.BlockName(_Welder, Logging.BlockNameOptions.None));
            required += WELDER_REQUIRED_ELECTRIC_POWER_STANDBY;
            required += State.CurrentWeldingBlock != null ? WelderMaximumRequiredElectricPowerWelding : 0f;
            required += State.CurrentGrindingBlock != null ? WelderMaximumRequiredElectricPowerGrinding : 0f;
            required += _CurrentTransportDestination != null || _CurrentTransportSource != null ? (Settings.SearchMode == SearchModes.Grids ? WelderMaximumRequiredElectricPowerTransport / 10 : WelderMaximumRequiredElectricPowerTransport) : 0f;
         }
         if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: ComputeRequiredElectricPower {1}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), required);
         return required;
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="required"></param>
      /// <returns></returns>
      private bool HasRequiredElectricPower(bool weld, bool transport)
      {
         if (_Welder == null) return false;
         var enought = true;
         var resourceSink = _Welder.Components.Get<Sandbox.Game.EntityComponents.MyResourceSinkComponent>();
         if (resourceSink != null)
         {
            var required = weld ? WelderMaximumRequiredElectricPowerWelding : 0f;
            required += transport ? (Settings.SearchMode == SearchModes.Grids ? WelderMaximumRequiredElectricPowerTransport / 10 : WelderMaximumRequiredElectricPowerTransport) : 0f;
            enought = resourceSink.IsPowerAvailable(ElectricityId, required);
         }
         if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: HasRequiredElectricPower {1}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), enought);
         return enought || MyAPIGateway.Session.CreativeMode;
      }

      /// <summary>
      /// 
      /// </summary>
      public override void Close()
      {
         if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: Close", Logging.BlockName(_Welder, Logging.BlockNameOptions.None));
         if (_IsInit)
         {
            Settings.Save(Entity, NanobotBuildAndRepairSystemMod.ModGuid);
            lock (NanobotBuildAndRepairSystemMod.BuildAndRepairSystems)
            {
               NanobotBuildAndRepairSystemMod.BuildAndRepairSystems.Remove(Entity.EntityId);
            }

            //Stop effects
            _CurrentTransportDestination = null;
            _CurrentTransportSource = null;
            State.Ready = false;
            UpdateEffects();
         }
         base.Close();
      }

      /// <summary>
      /// 
      /// </summary>
      public override void UpdateBeforeSimulation()
      {
         try
         {
            base.UpdateBeforeSimulation();
            if (_Welder == null || !_IsInit) return;
            
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
               if (Settings.ShowArea)
               {
                  var colorWelder = _Welder.SlimBlock.GetColorMask().HSVtoColor();
                  var color = Color.FromNonPremultiplied(colorWelder.R, colorWelder.G, colorWelder.B, 255);
                  var areaBoundingBox = Settings.AreaBoundingBox;
                  var matrix = _Welder.WorldMatrix;
                  MySimpleObjectDraw.DrawTransparentBox(ref matrix, ref areaBoundingBox, ref color, MySimpleObjectRasterizer.Solid, 1, 0.04f, RangeGridResourceId, null, false);
               }

               //Debug draw target boxes
               //lock (_PossibleWeldTargets)
               //{
               //   var colorWelder = _Welder.SlimBlock.GetColorMask().HSVtoColor();
               //   var color = Color.FromNonPremultiplied(colorWelder.R, colorWelder.G, colorWelder.B, 255);

               //   foreach (var targetData in _PossibleWeldTargets)
               //   {
               //      BoundingBoxD box;
               //      Vector3 halfExtents;
               //      targetData.Block.ComputeScaledHalfExtents(out halfExtents);
               //      halfExtents *= 1.2f;
               //      var matrix = targetData.Block.CubeGrid.WorldMatrix;
               //      matrix.Translation = targetData.Block.CubeGrid.GridIntegerToWorld(targetData.Block.Position);

               //      box = new BoundingBoxD(-(halfExtents), (halfExtents));
               //      MySimpleObjectDraw.DrawTransparentBox(ref matrix, ref box, ref color, MySimpleObjectRasterizer.Solid, 1, 0.04f, "HoneyComb", null, false);
               //   }
               //}

               UpdateEffects();
            }
         }
         catch (Exception ex)
         {
            if (Mod.Log.ShouldLog(Logging.Level.Error)) Mod.Log.Write(Logging.Level.Error, "BuildAndRepairSystemBlock {0}: UpdateBeforeSimulation Exception:{1}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), ex);
         }
      }

      /// <summary>
      /// 
      /// </summary>
      public override void UpdateBeforeSimulation10()
      {
         base.UpdateBeforeSimulation10();
         UpdateBeforeSimulation10_100();
      }

      /// <summary>
      /// 
      /// </summary>
      public override void UpdateBeforeSimulation100()
      {
         base.UpdateBeforeSimulation100();
         UpdateBeforeSimulation10_100();
      }

      private void UpdateBeforeSimulation10_100()
      {
         try
         {
            if (_Welder == null) return;
            if (!_IsInit) Init();
            if (!_IsInit) return;

            if (MyAPIGateway.Session.IsServer)
            {
               ServerTryWeldingGrinding();
               Settings.TrySave(Entity, NanobotBuildAndRepairSystemMod.ModGuid);
               if (State.IsTransmitNeeded())
               {
                  NanobotBuildAndRepairSystemMod.SyncBlockStateSend(0, this);
               }
            }
            else
            {
               if (State.Changed)
               {
                  UpdateCustomInfo();
                  State.ResetChanged();
               }
            }
            if (Settings.IsTransmitNeeded())
            {
               NanobotBuildAndRepairSystemMod.SyncBlockSettingsSend(0, this);
            }
         }
         catch (Exception ex)
         {
            if (Mod.Log.ShouldLog(Logging.Level.Error)) Mod.Log.Write(Logging.Level.Error, "BuildAndRepairSystemBlock {0}: UpdateBeforeSimulation10/100 Exception:{1}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), ex);
         }
      }

      /// <summary>
      /// Try to weld the possible targets
      /// </summary>
      private void ServerTryWeldingGrinding()
      {
         var hashPossibleWeldTargets = State.PossibleWeldTargets.GetHash();
         var hashPossibleGrindTargets = State.PossibleGrindTargets.GetHash();
         var hashMissingComponents = State.MissingComponents.GetHash();
         State.MissingComponents.Clear();

         var welding = false;
         var needwelding = false;
         var grinding = false;
         var transporting = false;
         var ready = _Welder.Enabled && _Welder.IsWorking && _Welder.IsFunctional;
         IMySlimBlock currentWeldingBlock = null;
         IMySlimBlock currentGrindingBlock = null;
         if (ready)
         {
            if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: TryWelding Welder ready: Enabled={1}, IsWorking={2}, IsFunctional={3}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), _Welder.Enabled, _Welder.IsWorking, _Welder.IsFunctional);

            switch (Settings.WorkMode)
            {
               case WorkModes.WeldBeforeGrind:
                  ServerTryWelding(out welding, out needwelding, out transporting, out currentWeldingBlock);
                  if (State.PossibleWeldTargets.Count == 0 || (Settings.ScriptControlled && Settings.CurrentPickedGrindingBlock != null))
                  {
                     ServerTryGrinding(out grinding, out transporting, out currentGrindingBlock);
                  }
                  break;
               case WorkModes.GrindBeforeWeld:
                  ServerTryGrinding(out grinding, out transporting, out currentGrindingBlock);
                  if (State.PossibleGrindTargets.Count == 0 || (Settings.ScriptControlled && Settings.CurrentPickedWeldingBlock != null))
                  {
                     ServerTryWelding(out welding, out needwelding, out transporting, out currentWeldingBlock);
                  }
                  break;
               case WorkModes.GrindIfWeldGetStuck:
                  ServerTryWelding(out welding, out needwelding, out transporting, out currentWeldingBlock);
                  if (!(welding || transporting) || (Settings.ScriptControlled && Settings.CurrentPickedGrindingBlock != null))
                  {
                     ServerTryGrinding(out grinding, out transporting, out currentGrindingBlock);
                  }
                  break;
            }
         }
         else
         {
            SourcesAndTargetsUpdateRun = 0;
            if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: TryWelding Welder not ready: Enabled={1}, IsWorking={2}, IsFunctional={3}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), _Welder.Enabled || MyAPIGateway.Session.CreativeMode, _Welder.IsWorking, _Welder.IsFunctional);
         }
         if (!welding)
         {
            _CurrentTransportDestination = null;
            if (State.Welding) AsyncUpdateSourcesAndTargets(false); //Scan immediately
         }
         if (!grinding)
         {
            _CurrentTransportSource = null;
            if (State.Grinding) AsyncUpdateSourcesAndTargets(false); //Scan immediately
         }

         var readyChanged = State.Ready != ready;
         State.Ready = ready;
         State.Welding = welding;
         State.NeedWelding = needwelding;
         State.CurrentWeldingBlock = currentWeldingBlock;

         State.Grinding = grinding;
         State.CurrentGrindingBlock = currentGrindingBlock;

         var missingComponentsChanged = hashMissingComponents != State.MissingComponents.GetHash();
         var possibleWeldTargetsChanged = hashPossibleWeldTargets != _LastPossibleWeldTargetsHash;
         _LastPossibleWeldTargetsHash = hashPossibleWeldTargets;

         var possibleGrindTargetsChanged = hashPossibleGrindTargets != _LastPossibleGrindTargetsHash;
         _LastPossibleGrindTargetsHash = hashPossibleGrindTargets;

         if (missingComponentsChanged || possibleWeldTargetsChanged || possibleGrindTargetsChanged) State.HasChanged();

         if (missingComponentsChanged && Mod.Log.ShouldLog(Logging.Level.Verbose))
         {
            lock (Mod.Log)
            {
               Mod.Log.Write(Logging.Level.Verbose, "BuildAndRepairSystemBlock {0}: TryWelding: MissingComponents --->", Logging.BlockName(_Welder, Logging.BlockNameOptions.None));
               Mod.Log.IncreaseIndent(Logging.Level.Verbose);
               foreach (var missing in State.MissingComponents)
               {
                  Mod.Log.Write(Logging.Level.Verbose, "{0}:{1}", missing.Key.SubtypeName, missing.Value);
               }
               Mod.Log.DecreaseIndent(Logging.Level.Verbose);
               Mod.Log.Write(Logging.Level.Verbose, "<--- MissingComponents");
            }
         }

         if (missingComponentsChanged || possibleWeldTargetsChanged || possibleGrindTargetsChanged || readyChanged)
         {
            UpdateCustomInfo();
         }
      }

      private void ServerTryGrinding(out bool grinding, out bool transporting, out IMySlimBlock currentGrindingBlock)
      {
         grinding = false;
         transporting = false;
         currentGrindingBlock = null;
         lock (State.PossibleGrindTargets)
         {
            foreach (var targetData in State.PossibleGrindTargets)
            {
               if (Settings.ScriptControlled && targetData.Block != Settings.CurrentPickedGrindingBlock) continue;

               if (!targetData.Block.IsDestroyed)
               {
                  if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: TryGrinding: {1}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(targetData.Block));

                  grinding = ServerDoGrind(targetData, out transporting);
                  if (grinding)
                  {
                     currentGrindingBlock = targetData.Block;
                     break; //Only grind one block at once
                  }
               }
            }
         }
      }

      private void ServerTryWelding(out bool welding, out bool needwelding, out bool transporting, out IMySlimBlock currentWeldingBlock)
      {
         welding = false;
         needwelding = false;
         transporting = false;
         currentWeldingBlock = null;
         lock (State.PossibleWeldTargets)
         {
            foreach (var targetData in State.PossibleWeldTargets)
            {
               if (Settings.ScriptControlled && targetData.Block != Settings.CurrentPickedWeldingBlock) continue;

               var createBlock = targetData.Block.IsProjected();
               if ((Settings.ScriptControlled || !IsFriendlyDamage(targetData.Block)) && ((!createBlock && targetData.Block.NeedRepair()) || (createBlock && targetData.Block.CouldBuild())))
               {
                  needwelding = true;
                  if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: TryWelding: {1} HasDeformation={2} (MaxDeformation={3}), IsFullIntegrity={4}, HasFatBlock={5}, IsProjected={6}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(targetData.Block), targetData.Block.HasDeformation, targetData.Block.MaxDeformation, targetData.Block.IsFullIntegrity, targetData.Block.FatBlock != null, targetData.Block.IsProjected());

                  transporting = ServerFindMissingComponents(targetData, createBlock);
                  welding = ServerDoWeld(targetData, createBlock);
                  if (welding)
                  {
                     currentWeldingBlock = targetData.Block;
                     break; //Only weld one block at once (do not split over all blocks as the base shipwelder does)
                  }
               }
            }
         }
      }

      /// <summary>
      /// 
      /// </summary>
      private void UpdateCustomInfo()
      {
         _Welder.RefreshCustomInfo();
         TriggerTerminalRefresh();
      }

      /// <summary>
      /// 
      /// </summary>
      public void TriggerTerminalRefresh()
      {
         //Workaround as long as RaisePropertiesChanged is not public
         if (_Welder != null)
         {
            var action = _Welder.GetActionWithName("helpOthers");
            if (action != null)
            {
               action.Apply(_Welder);
               action.Apply(_Welder);
            }
         }
      }

      /// <summary>
      /// 
      /// </summary>
      private bool ServerDoWeld(TargetBlockData targetData, bool createBlock)
      {
         var welderInventory = _Welder.GetInventory(0);
         var welding = false;
         var created = false;
         var target = targetData.Block;
         var hasIgnoreColor = Settings.UseIgnoreColor && Settings.IgnoreColor == target.GetColorMask();

         if (!HasRequiredElectricPower(true, _CurrentTransportDestination != null)) return false; //-> Not enought power

         if (createBlock)
         {
            //New Block (Projected)
            var cubeGridProjected = target.CubeGrid as MyCubeGrid;
            var blockDefinition = target.BlockDefinition as MyCubeBlockDefinition;
            var item = _TransportInventory.FindItem(blockDefinition.Components[0].Definition.Id);
            if ((MyAPIGateway.Session.CreativeMode || (item != null && item.Amount >= 1)) && cubeGridProjected != null && cubeGridProjected.Projector != null)
            {
               ((Sandbox.ModAPI.IMyProjector)cubeGridProjected.Projector).Build(target, _Welder.OwnerId, _Welder.EntityId, true);
               if (!MyAPIGateway.Session.CreativeMode) _TransportInventory.RemoveItems(item.ItemId, 1);
               created = true;
               if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: ServerDoWeld (new): {1}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(target));

               //After creation we can't welding this projected block, we have to find the 'physical' block instead.
               var cubeGrid = cubeGridProjected.Projector.CubeGrid;
               Vector3I blockPos = cubeGrid.WorldToGridInteger(cubeGridProjected.GridIntegerToWorld(target.Position));
               target = cubeGrid.GetCubeBlock(blockPos);
               if (target != null) targetData.Block = target;
            }
         }

         if (!hasIgnoreColor && target != null && (!createBlock || created)) {
            //No ignore color and allready created
            if (!target.IsFullIntegrity || created)
            {
               //Incomplete
               welding = target.CanContinueBuild(_TransportInventory) || MyAPIGateway.Session.CreativeMode;
               //If we could weld or welder is getting full move collected items to stockpile.
               //Otherwise keep them in welder, maybe we could use them for a block that could be immediately welded
               target.MoveItemsToConstructionStockpile(_TransportInventory);
               if (welding)
               {
                  if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: ServerDoWeld (incomplete): {1}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(target));
                  //target.MoveUnneededItemsFromConstructionStockpile(welderInventory); not available in modding api
                  target.IncreaseMountLevel(MyAPIGateway.Session.WelderSpeedMultiplier * NanobotBuildAndRepairSystemMod.Settings.Welder.WeldingMultiplier * WELDER_AMOUNT_PER_SECOND, _Welder.OwnerId, welderInventory, MyAPIGateway.Session.WelderSpeedMultiplier * NanobotBuildAndRepairSystemMod.Settings.Welder.WeldingMultiplier * WELDER_MAX_REPAIR_BONE_MOVEMENT_SPEED, _Welder.HelpOthers);
               }
               ServerEmptyTranportInventory(false);
            }
            else
            {
               //Deformation
               if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: ServerDoWeld (deformed): {1}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(target));
               welding = true;
               target.IncreaseMountLevel(MyAPIGateway.Session.WelderSpeedMultiplier * NanobotBuildAndRepairSystemMod.Settings.Welder.WeldingMultiplier * WELDER_AMOUNT_PER_SECOND, _Welder.OwnerId, welderInventory, MyAPIGateway.Session.WelderSpeedMultiplier * NanobotBuildAndRepairSystemMod.Settings.Welder.WeldingMultiplier * WELDER_MAX_REPAIR_BONE_MOVEMENT_SPEED, _Welder.HelpOthers);
            }
         }

         return welding || created;
      }

      /// <summary>
      /// 
      /// </summary>
      private bool ServerDoGrind(TargetBlockData targetData, out bool transporting)
      {
         transporting = false;

         var playTime = MyAPIGateway.Session.ElapsedPlayTime;
         if (playTime.Subtract(_LastTransportStartTime) < _TransportTime)
         {
            //Last transport still running -> wait
            if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: ServerDoGrind: Target {1} transport still running remaining transporttime={2}",
               Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(targetData.Block), _TransportTime.Subtract(MyAPIGateway.Session.ElapsedPlayTime.Subtract(_LastTransportStartTime)));
            transporting = true;
            return false;
         }

         _CurrentTransportSource = null;
         var welderInventory = _Welder.GetInventory(0);
         var grinding = false;
         var target = targetData.Block;
         var targetGrid = target.CubeGrid;

         if (targetGrid.Physics == null || !targetGrid.Physics.Enabled) return false;
         if (!HasRequiredElectricPower(true, true)) return false; //-> Not enought power

         grinding = true;

         var integrityRatio = target.Integrity / target.MaxIntegrity;
         var emptying = false;
         if (integrityRatio <= 0.2)
         {
            //Try to emtpy inventory (if any)
            if (target.FatBlock != null && target.FatBlock.HasInventory)
            {
               emptying = EmptyBlockInventories(target.FatBlock, _TransportInventory);
               if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: ServerDoGrind {1} Try empty Inventory running={2}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(target), emptying);
            }
         }

         if (!emptying)
         {
            float damage = MyAPIGateway.Session.GrinderSpeedMultiplier * NanobotBuildAndRepairSystemMod.Settings.Welder.GrindingMultiplier * GRINDER_AMOUNT_PER_SECOND;
            MyDamageInformation damageInfo = new MyDamageInformation(false, damage, MyDamageType.Grind, _Welder.EntityId);

            if (target.UseDamageSystem)
            {
               //Not available in modding
               //MyAPIGateway.Session.DamageSystem.RaiseBeforeDamageApplied(target, ref damageInfo);

               foreach (var entry in NanobotBuildAndRepairSystemMod.BuildAndRepairSystems)
               {
                  var relation = entry.Value.Welder.GetUserRelationToOwner(_Welder.OwnerId);
                  if (MyRelationsBetweenPlayerAndBlockExtensions.IsFriendly(relation))
                  {
                     //A 'friendly' damage from grinder -> do not repair (for a while)
                     //I don't check block relation here, because if it is enemy we won't repair it in any case and it just times out
                     entry.Value.FriendlyDamage[target] = MyAPIGateway.Session.ElapsedPlayTime + NanobotBuildAndRepairSystemMod.Settings.FriendlyDamageTimeout;
                     if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock: Damaged Add FriendlyDamage {0} Timeout {1}", Logging.BlockName(target), entry.Value.FriendlyDamage[target]);
                  }
               }
            }

            target.DecreaseMountLevel(damageInfo.Amount, _TransportInventory);
            target.MoveItemsFromConstructionStockpile(_TransportInventory);

            if (target.UseDamageSystem)
            {
               //Not available in modding
               //MyAPIGateway.Session.DamageSystem.RaiseAfterDamageApplied(target, ref damageInfo);
            }
            if (target.IsFullyDismounted)
            {
               if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: ServerDoGrind {1} FullyDismounted", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(target));
               if (target.UseDamageSystem)
               {
                  //Not available in modding
                  //MyAPIGateway.Session.DamageSystem.RaiseDestroyed(target, damageInfo);
               }

               target.SpawnConstructionStockpile();
               target.CubeGrid.RazeBlock(target.Position);
            }
         }

         if ((float)_TransportInventory.CurrentVolume >= _MaxTransportVolume || target.IsFullyDismounted)
         {
            //Transport startet
            _CurrentTransportSource = target;
            _LastTransportStartTime = playTime;
            _TransportTime = TimeSpan.FromSeconds(2d * targetData.Distance / WelderTransportSpeed);
            if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: ServerDoGrind: Target {1} transport started transporttime={2}",
               Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(targetData.Block), _TransportTime);
            ServerEmptyTranportInventory(true);
            transporting = true;
         }

         return grinding;
      }

      /// <summary>
      /// Try to find an the missing components and moves them into welder inventory
      /// </summary>
      private bool ServerFindMissingComponents(TargetBlockData targetData, bool createNew)
      {
         try
         {
            var playTime = MyAPIGateway.Session.ElapsedPlayTime;
            if (playTime.Subtract(_LastTransportStartTime) < _TransportTime)
            {
               //Last transport still running -> wait
               if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: FindMissingComponents: Target {1} transport still running remaining transporttime={2}",
                  Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(targetData.Block), _TransportTime.Subtract(MyAPIGateway.Session.ElapsedPlayTime.Subtract(_LastTransportStartTime)));
               return true;
            }

            _CurrentTransportDestination = null;
            if (!HasRequiredElectricPower(false, true))
            {
               //-> Not enought power for transport
               if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: FindMissingComponents: Target {1} not enought electricPower Available", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(targetData.Block));
               return false;
            }

            var remainingVolume = _MaxTransportVolume;
            _TempMissingComponents.Clear();
            var cubeGrid = targetData.Block.CubeGrid as MyCubeGrid;
            if (createNew)
            {
               var blockDefinition = targetData.Block.BlockDefinition as MyCubeBlockDefinition;
               if (blockDefinition.Components == null || blockDefinition.Components.Length == 0) return false;
               var component = blockDefinition.Components[0];
               _TempMissingComponents.Add(component.Definition.Id.SubtypeName, 1);

               var picked = ServerFindMissingComponents(targetData, ref remainingVolume);
               if (picked)
               {
                  if (!Settings.UseIgnoreColor || Settings.IgnoreColor != targetData.Block.GetColorMask())
                  {
                     //Block could be created and should be weldet -> so retrieve the remaining material also
                     if (component.Count > 1) _TempMissingComponents[component.Definition.Id.SubtypeName] = component.Count - 1;
                     for (var idx = 1; idx < blockDefinition.Components.Length; idx++)
                     {
                        component = blockDefinition.Components[idx];
                        if (_TempMissingComponents.ContainsKey(component.Definition.Id.SubtypeName)) _TempMissingComponents[component.Definition.Id.SubtypeName] += component.Count;
                        else _TempMissingComponents.Add(component.Definition.Id.SubtypeName, component.Count);
                     }
                  }
               }
            }
            else
            {
               targetData.Block.GetMissingComponents(_TempMissingComponents);
            }

            if (_TempMissingComponents.Count > 0)
            {
               ServerFindMissingComponents(targetData, ref remainingVolume);
            }
            if (remainingVolume < _MaxTransportVolume || (MyAPIGateway.Session.CreativeMode && _TempMissingComponents.Count > 0))
            {
               //Transport startet
               _CurrentTransportDestination = targetData.Block;
               _LastTransportStartTime = playTime;
               _TransportTime = TimeSpan.FromSeconds(2d * targetData.Distance / WelderTransportSpeed);
               if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: FindMissingComponents: Target {1} transport started volume={2} (max {3}) transporttime={4}",
                  Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(targetData.Block), _MaxTransportVolume - remainingVolume, _MaxTransportVolume, _TransportTime);
               return true;
            }
            return false;
         }
         finally
         {
            _TempMissingComponents.Clear();
         }
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="targetData"></param>
      /// <returns></returns>
      private bool ServerFindMissingComponents(TargetBlockData targetData, ref float remainingVolume)
      {
         var picked = false;
         foreach (var component in _TempMissingComponents)
         {
            var componentId = new MyDefinitionId(typeof(MyObjectBuilder_Component), component.Key);
            if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: FindMissingComponents: Target {1} missing {2}={3} remainingVolume={4}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(targetData.Block), componentId, component.Value, remainingVolume);
            int neededAmount = 0;
            int amount;

            var group = MyDefinitionManager.Static.GetGroupForComponent(componentId, out amount);
            if (group == null)
            {
               MyComponentSubstitutionDefinition substitutions;
               if (MyDefinitionManager.Static.TryGetComponentSubstitutionDefinition(componentId, out substitutions))
               {
                  foreach (var providingComponent in substitutions.ProvidingComponents)
                  {
                     var definition = MyDefinitionManager.Static.GetPhysicalItemDefinition(providingComponent.Key);
                     neededAmount = component.Value / providingComponent.Value;
                     picked = ServerPickFromWelder(providingComponent.Key, definition.Volume, ref neededAmount, ref remainingVolume) || picked;
                     if (neededAmount > 0 && remainingVolume > 0) picked = PullComponents(providingComponent.Key, definition.Volume, ref neededAmount, ref remainingVolume) || picked;
                  }
               }
               else
               {
                  var definition = MyDefinitionManager.Static.GetPhysicalItemDefinition(componentId);
                  neededAmount = component.Value;
                  picked = ServerPickFromWelder(componentId, definition.Volume, ref neededAmount, ref remainingVolume) || picked;
                  if (neededAmount > 0 && remainingVolume > 0) picked = PullComponents(componentId, definition.Volume, ref neededAmount, ref remainingVolume) || picked; 
               }
            }
            else
            {
               var definition = MyDefinitionManager.Static.GetPhysicalItemDefinition(componentId);
               neededAmount = component.Value;
               picked = ServerPickFromWelder(componentId, definition.Volume, ref neededAmount, ref remainingVolume) || picked;
               if (neededAmount > 0 && remainingVolume > 0) picked = PullComponents(componentId, definition.Volume, ref neededAmount, ref remainingVolume) || picked;
            }

            if (neededAmount > 0 && remainingVolume > 0) AddToMissingComponents(componentId, neededAmount);
            if (remainingVolume <= 0) break;
         }
         return picked;
      }

      /// <summary>
      /// Try to pick needed material from own inventory, if successfull material is moved into transport inventory
      /// </summary>
      private bool ServerPickFromWelder(MyDefinitionId componentId, float volume, ref int neededAmount, ref float remainingVolume)
      {
         if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: PickFromWelder Try: {1}={2}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), componentId, neededAmount);

         var welderInventory = _Welder.GetInventory(0);
         if (welderInventory == null || welderInventory.Empty())
         {
            if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: PickFromWelder welder empty: {1}={2}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), componentId, neededAmount);
            return false;
         }

         var picked = false;
         var srcItems = welderInventory.GetItems();
         for (int i1 = srcItems.Count-1; i1 >= 0; i1--)
         {
            var srcItem = srcItems[i1];
            if (srcItem != null && srcItem.Content.GetId() == componentId && srcItem.Amount > 0)
            {
               var maxpossibleAmount = Math.Min(neededAmount, (int)Math.Ceiling(remainingVolume / volume));
               var pickedAmount = MyFixedPoint.Min(maxpossibleAmount, srcItem.Amount);

               welderInventory.RemoveItems(srcItem.ItemId, pickedAmount);
               _TransportInventory.AddItems(pickedAmount, (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(componentId));

               neededAmount -= (int)pickedAmount;
               remainingVolume -= (float)pickedAmount * volume;
               picked = picked || pickedAmount > 0;
               if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: PickFromWelder: {1}: missingAmount={2} pickedAmount={3} maxpossibleAmount={4} remainingVolume={5} transportVolumeTotal={6}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), componentId, neededAmount, pickedAmount, maxpossibleAmount, remainingVolume, _TransportInventory.CurrentVolume);
            }
            if (neededAmount <= 0 || remainingVolume <= 0) break;
         }
         return picked;
      }

      /// <summary>
      /// Check if the transport inventory is empty after delivering, if not move items back to welder inventory
      /// </summary>
      private void ServerEmptyTranportInventory(bool push)
      {
         if (!_TransportInventory.Empty())
         {
            if (!MyAPIGateway.Session.CreativeMode)
            {
               var welderInventory = _Welder.GetInventory(0);
               if (welderInventory != null)
               {
                  if (push) {
                     if (welderInventory.MaxVolume - welderInventory.CurrentVolume < _TransportInventory.CurrentVolume)
                     {
                        welderInventory.PushComponents(_PossibleSources);
                     }
                  }

                  var items = _TransportInventory.GetItems();
                  foreach (var item in items)
                  {
                     if (welderInventory.CanItemsBeAdded(item.Amount, item.Content.GetId())){
                        //If there is not enought space, items will be automaticly spawned as floating objects by AddItems!
                        welderInventory.AddItems(item.Amount, (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(item.Content.GetId()));
                        if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: CheckTranportInventory move to welder Item {1} amount={2}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), item.Content.GetId(), item.Amount);
                     } else
                     {
                        _Welder.Components.Add((Sandbox.Game.MyInventory)_TransportInventory); //Add to spawn
                        _TransportInventory.RemoveItems(item.ItemId, spawn: true);
                        _Welder.Components.Remove(typeof(Sandbox.Game.MyInventory), (Sandbox.Game.MyInventory)_TransportInventory);
                        if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: CheckTranportInventory (no more room in welder) spawn Item {1} amount={2}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), item.Content.GetId(), item.Amount);
                     }
                     
                  }
               }
            }
            _TransportInventory.Clear();
         }
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="block"></param>
      /// <returns></returns>
      private bool EmptyBlockInventories(IMyCubeBlock block, IMyInventory dstInventory)
      {
         var running = false;
         var remainingVolume = _MaxTransportVolume;

         for (int i1 = 0; i1 < block.InventoryCount; ++i1)
         {
            var srcInventory = block.GetInventory(i1) as IMyInventory;
            if (srcInventory.Empty()) continue;
            lock (srcInventory)//Protect 'IsConnectedTo' from use in the other threads, could be removed after MyInventory.IsConnected is Thread safe
            {
               var srcItems = srcInventory.GetItems();
               for (int i2 = 0; i2 < srcItems.Count; i2++)
               {
                  var srcItem = srcItems[i1];
                  if (srcItem == null) continue;
                  var definition = MyDefinitionManager.Static.GetPhysicalItemDefinition(srcItem.Content.GetId());
                  var startAmount = srcItem.Amount;

                  var maxpossibleAmount = MyFixedPoint.Min(srcItem.Amount, MyFixedPoint.Ceiling((MyFixedPoint)(remainingVolume / definition.Volume)));
                  var mass = srcInventory.CurrentMass;
                  srcInventory.RemoveItems(srcItem.ItemId, maxpossibleAmount);
                  if (mass > srcInventory.CurrentMass)
                  {
                     _TransportInventory.AddItems(maxpossibleAmount, (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(srcItem.Content.GetId()));
                     remainingVolume -= (float)maxpossibleAmount * definition.Volume;
                     running = true;
                     if (remainingVolume <= 0) return true; //No more transport volume
                  }
                  else return running; //No more space
               }
            }
         }

         return running;
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="componentId"></param>
      /// <param name="neededAmount"></param>
      private void AddToMissingComponents(MyDefinitionId componentId, int neededAmount)
      {
         int missingAmount;
         if (State.MissingComponents.TryGetValue(componentId, out missingAmount))
         {
            State.MissingComponents[componentId] = missingAmount + neededAmount;
         }
         else
         {
            State.MissingComponents.Add(componentId, neededAmount);
         }
      }

      /// <summary>
      /// Pull components into welder
      /// </summary>
      private bool PullComponents(MyDefinitionId componentId, float volume, ref int neededAmount, ref float remainingVolume)
      {
         int availAmount = 0;
         var welderInventory = _Welder.GetInventory(0);
         var maxpossibleAmount = Math.Min(neededAmount, (int)Math.Ceiling(remainingVolume / volume));
         if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: PullComponents start: {1}={2} maxpossibleAmount={3} volume={4}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), componentId, neededAmount, maxpossibleAmount, volume);
         if (maxpossibleAmount <= 0) return false;
         var picked = false;
         lock (_PossibleSources)
         {
            foreach (var srcInventory in _PossibleSources)
            {
               var srcItems = srcInventory.GetItems();
               for (int i1 = 0; i1 < srcItems.Count; i1++)
               {
                  var srcItem = srcItems[i1];
                  if (srcItem != null && srcItem.Content.GetId() == componentId && srcItem.Amount > 0)
                  {
                     if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: PullComponents Found: {1}={2} in {3}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), componentId, srcItem.Amount, Logging.BlockName(srcInventory));
                     var amountPossible = Math.Min(maxpossibleAmount, (int)srcItem.Amount);
                     if (amountPossible > 0)
                     {
                        var amountMoveable = amountPossible;
                        while (!welderInventory.CanItemsBeAdded(amountMoveable, componentId) && amountMoveable > 0)
                        {
                           amountMoveable -= 1;
                        }
                        if (amountMoveable > 0)
                        {
                           if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: PullComponents Try to move: {1}={2} from {3}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), componentId, amountMoveable, Logging.BlockName(srcInventory));
                           var moved = false;
                           lock (srcInventory)//Protect 'IsConnectedTo' from use in the other threads, could be removed after MyInventory.IsConnected is Thread safe
                           {
                              moved = srcInventory.TransferItemTo(welderInventory, i1, null, true, amountMoveable);
                           }
                           if (moved)
                           {
                              maxpossibleAmount -= amountMoveable;
                              availAmount += amountMoveable;
                              if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: PullComponents Moved: {1}={2} from {3}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), componentId, amountMoveable, Logging.BlockName(srcInventory));
                              picked = ServerPickFromWelder(componentId, volume, ref neededAmount, ref remainingVolume) || picked;
                           }
                        }
                        else
                        {
                           //No (more) space in welder
                           if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: PullComponents no more space in welder: {1}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), componentId);
                           neededAmount -= availAmount;
                           remainingVolume -= availAmount * volume;
                           return picked;
                        }
                     }
                  }
                  if (maxpossibleAmount <= 0) break;
               }
               if (maxpossibleAmount <= 0) break;
            }
         }

         return picked;
      }

      /// <summary>
      /// Parse all the connected blocks and find the possible targets and sources of components
      /// </summary>
      public void AsyncUpdateSourcesAndTargets(bool updateSource)
      {
         if (!_Welder.Enabled || !_Welder.IsFunctional) {
            if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: AsyncUpdateSourcesAndTargets Enabled={1} IsFunctional={2} ---> not ready don't search for targets", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), _Welder.Enabled, _Welder.IsFunctional);
            lock (State.PossibleWeldTargets)
            {
               State.PossibleWeldTargets.Clear();
            }
            return;
         };

         MyAPIGateway.Parallel.StartBackground(() =>
         {
            int pos = 0;
            try
            {
               pos = 1;

               var grids = new List<IMyCubeGrid>();
               var possibleWeldTargets = new List<TargetBlockData>();
               var possibleGrindTargets = new List<TargetBlockData>();
               var possibleSources = updateSource ? new List<IMyInventory>() : null;

               var ignoreColor = Settings.IgnoreColor;
               var grindColor = Settings.GrindColor;
               var worldMatrix = _Welder.WorldMatrix;
               var areaBox = new MyOrientedBoundingBoxD(Settings.AreaBoundingBox, worldMatrix);

               AsyncAddBlocksOfGrid(ref areaBox, Settings.UseIgnoreColor, ref ignoreColor, Settings.UseGrindColor, ref grindColor, _Welder.CubeGrid, grids, possibleSources, possibleWeldTargets, possibleGrindTargets);
               switch (Settings.SearchMode)
               {
                  case SearchModes.Grids:
                     break;
                  case SearchModes.BoundingBox:
                     AsyncAddBlocksOfBox(ref areaBox, Settings.UseIgnoreColor, ref ignoreColor, Settings.UseGrindColor, ref grindColor, grids, possibleWeldTargets, possibleGrindTargets);
                     break;
               }

               pos = 2;
               if (possibleSources != null)
               {
                  Vector3D posWelder;
                  _Welder.SlimBlock.ComputeWorldCenter(out posWelder);
                  possibleSources.Sort((a, b) =>
                  {
                     var blockA = a.Owner as IMySlimBlock;
                     var blockB = b.Owner as IMySlimBlock;
                     if (blockA != null && blockB != null)
                     {
                        Vector3D posA;
                        blockA.ComputeWorldCenter(out posA);
                        var distanceA = (int)Math.Abs((posWelder - posA).Length());
                        var distanceB = (int)Math.Abs((posWelder - posA).Length());
                        return distanceA - distanceB;
                     }
                     else if (blockA == null && blockB == null) return 0;
                     else if (blockA != null) return -1;
                     else return 1;
                  });
               }

               pos = 3;
               possibleWeldTargets.Sort((a, b) =>
               {
                  var blockA = a.Block;
                  var blockB = b.Block;
                  var priorityA = _BuildPriority.GetBuildPriority(blockA);
                  var priorityB = _BuildPriority.GetBuildPriority(blockB);
                  if (priorityA == priorityB)
                  {
                     return (int)(a.Distance - b.Distance);
                  }
                  else return priorityA - priorityB;
               });

               pos = 4;
               possibleGrindTargets.Sort((a, b) =>
               {
                  var blockA = a.Block;
                  var blockB = b.Block;
                  return (int)(b.Distance - a.Distance); //from far to near
               });

               pos = 5;
               if (Mod.Log.ShouldLog(Logging.Level.Verbose))
               {
                  lock (Mod.Log)
                  {
                     Mod.Log.Write(Logging.Level.Verbose, "BuildAndRepairSystemBlock {0}: AsyncUpdateSourcesAndTargets Possible Build Target Blocks --->", Logging.BlockName(_Welder, Logging.BlockNameOptions.None));
                     Mod.Log.IncreaseIndent(Logging.Level.Verbose);
                     foreach (var blockData in possibleWeldTargets)
                     {
                        Mod.Log.Write(Logging.Level.Verbose, "Block: {0} ({1})", Logging.BlockName(blockData.Block), blockData.Distance);
                     }
                     Mod.Log.DecreaseIndent(Logging.Level.Verbose);
                     Mod.Log.Write(Logging.Level.Verbose, "<---");

                     Mod.Log.Write(Logging.Level.Verbose, "BuildAndRepairSystemBlock {0}: AsyncUpdateSourcesAndTargets Possible Grind Target Blocks --->", Logging.BlockName(_Welder, Logging.BlockNameOptions.None));
                     Mod.Log.IncreaseIndent(Logging.Level.Verbose);
                     foreach (var blockData in possibleGrindTargets)
                     {
                        Mod.Log.Write(Logging.Level.Verbose, "Block: {0} ({1})", Logging.BlockName(blockData.Block), blockData.Distance);
                     }
                     Mod.Log.DecreaseIndent(Logging.Level.Verbose);
                     Mod.Log.Write(Logging.Level.Verbose, "<---");

                     if (updateSource)
                     {
                        Mod.Log.Write(Logging.Level.Verbose, "BuildAndRepairSystemBlock {0}: AsyncUpdateSourcesAndTargets Possible Source Blocks --->", Logging.BlockName(_Welder, Logging.BlockNameOptions.None));
                        Mod.Log.IncreaseIndent(Logging.Level.Verbose);
                        foreach (var inventory in possibleSources)
                        {
                           Mod.Log.Write(Logging.Level.Verbose, "Inventory: {0}", Logging.BlockName(inventory));
                        }
                        Mod.Log.DecreaseIndent(Logging.Level.Verbose);
                        Mod.Log.Write(Logging.Level.Verbose, "<---");
                     }
                  }
               }

               pos = 6;
               lock (State.PossibleWeldTargets)
               {
                  State.PossibleWeldTargets.Clear();
                  State.PossibleWeldTargets.AddRange(possibleWeldTargets);
               }
               pos = 7;
               lock (State.PossibleGrindTargets)
               {
                  State.PossibleGrindTargets.Clear();
                  State.PossibleGrindTargets.AddRange(possibleGrindTargets);
               }
               pos = 8;
               if (updateSource)
               {
                  lock (_PossibleSources)
                  {
                     _PossibleSources.Clear();
                     _PossibleSources.AddRange(possibleSources);
                  }
               }

               _ContinuouslyError = 0;
            }
            catch (Exception ex)
            {
               _ContinuouslyError++;
               if (_ContinuouslyError > 10 || Mod.Log.ShouldLog(Logging.Level.Info) || Mod.Log.ShouldLog(Logging.Level.Verbose))
               {
                  Mod.Log.Error("BuildAndRepairSystemBlock {0}: AsyncUpdateSourcesAndTargets exception at {1}: {2}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), pos, ex);
                  _ContinuouslyError = 0;
               }
            }
         });
      }

      /// <summary>
      /// Search for grids inside bounding box and add their damaged block also
      /// </summary>
      private void AsyncAddBlocksOfBox(ref MyOrientedBoundingBoxD areaBox, bool useIgnoreColor, ref Vector3 ignoreColor, bool useGrindColor, ref Vector3 grindColor, List<IMyCubeGrid> grids, List<TargetBlockData> possibleWeldTargets, List<TargetBlockData> possibleGrindTargets)
      {
         if (Mod.Log.ShouldLog(Logging.Level.Verbose)) Mod.Log.Write(Logging.Level.Verbose, "BuildAndRepairSystemBlock {0}: AsyncAddBlockOfBox", Logging.BlockName(_Welder, Logging.BlockNameOptions.None));
         var areaBoundingBox = Settings.AreaBoundingBox.TransformSlow(_Welder.WorldMatrix);
         List<IMyEntity> entityInRange = null;
         lock (MyAPIGateway.Entities)
         {
            //API not thread save !!!
            entityInRange = MyAPIGateway.Entities.GetElementsInBox(ref areaBoundingBox);
            //The list contains grid, Fatblocks and Damaged blocks in range. But as I would like to use the searchfunction also for grinding,
            //I only could use the grids and have to traverse through the grids to get all slimblocks.
         }
         if (entityInRange != null)
         {
            foreach (var entity in entityInRange)
            {
               var grid = entity as IMyCubeGrid;
               if (grid != null)
               {
                  AsyncAddBlocksOfGrid(ref areaBox, Settings.UseIgnoreColor, ref ignoreColor, useGrindColor, ref grindColor, grid, grids, null, possibleWeldTargets, possibleGrindTargets);
                  continue;
               }
            }
         }
      }

      /// <summary>
      /// 
      /// </summary>
      private void AsyncAddBlocksOfGrid(ref MyOrientedBoundingBoxD areaBox, bool useIgnoreColor, ref Vector3 ignoreColor, bool useGrindColor, ref Vector3 grindColor, IMyCubeGrid cubeGrid, List<IMyCubeGrid> grids, List<IMyInventory> possibleSources, List<TargetBlockData> possibleWeldTargets, List<TargetBlockData> possibleGrindTargets)
      {
         if (grids.Contains(cubeGrid)) return; //Allready parsed

         if (Mod.Log.ShouldLog(Logging.Level.Verbose)) Mod.Log.Write(Logging.Level.Verbose, "BuildAndRepairSystemBlock {0}: AsyncAddBlocksOfGrid AddGrid {1}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), cubeGrid.DisplayName);
         grids.Add(cubeGrid);

         var newBlocks = new List<IMySlimBlock>();
         cubeGrid.GetBlocks(newBlocks);

         foreach (var slimBlock in newBlocks)
         {
            AsyncAddBlockIfTargetOrSource(ref areaBox, useIgnoreColor, ref ignoreColor, useGrindColor, ref grindColor, slimBlock, possibleSources, possibleWeldTargets, possibleGrindTargets);

            var fatBlock = slimBlock.FatBlock;
            if (fatBlock == null) continue;

            var mechanicalConnectionBlock = fatBlock as Sandbox.ModAPI.IMyMechanicalConnectionBlock;
            if (mechanicalConnectionBlock != null)
            {
               if (mechanicalConnectionBlock.TopGrid != null)
                  AsyncAddBlocksOfGrid(ref areaBox, useIgnoreColor, ref ignoreColor, useGrindColor, ref grindColor, mechanicalConnectionBlock.TopGrid, grids, possibleSources, possibleWeldTargets, possibleGrindTargets);
               continue;
            }

            var attachableTopBlock = fatBlock as Sandbox.ModAPI.IMyAttachableTopBlock;
            if (attachableTopBlock != null)
            {
               if (attachableTopBlock.Base != null && attachableTopBlock.Base.CubeGrid != null)
                  AsyncAddBlocksOfGrid(ref areaBox, useIgnoreColor, ref ignoreColor, useGrindColor, ref grindColor, attachableTopBlock.Base.CubeGrid, grids, possibleSources, possibleWeldTargets, possibleGrindTargets);
               continue;
            }

            var connector = fatBlock as Sandbox.ModAPI.IMyShipConnector;
            if (connector != null)
            {
               if (connector.Status == MyShipConnectorStatus.Connected && connector.OtherConnector != null)
               {
                  AsyncAddBlocksOfGrid(ref areaBox, useIgnoreColor, ref ignoreColor, useGrindColor, ref grindColor, connector.OtherConnector.CubeGrid, grids, possibleSources, possibleWeldTargets, possibleGrindTargets);
               }
               continue;
            }

            if (possibleWeldTargets != null && Settings.AllowBuild) //If projected blocks should be build
            {
               var projector = fatBlock as Sandbox.ModAPI.IMyProjector;
               if (projector != null)
               {
                  if (Mod.Log.ShouldLog(Logging.Level.Verbose)) Mod.Log.Write(Logging.Level.Verbose, "BuildAndRepairSystemBlock {0}: Projector Block {1} IsProjecting={2} BuildableBlockCount={3}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(projector), projector.IsProjecting, projector.BuildableBlocksCount);
                  if (IsRelationAllowed(slimBlock) && projector.IsProjecting && projector.BuildableBlocksCount > 0)
                  {
                     //Add buildable blocks
                     var projectedCubeGrid = projector.ProjectedGrid;
                     if (projectedCubeGrid != null && !grids.Contains(projectedCubeGrid))
                     {
                        var projectedBlocks = new List<IMySlimBlock>();
                        projectedCubeGrid.GetBlocks(projectedBlocks);

                        foreach (IMySlimBlock block in projectedBlocks)
                        {
                           double distance;
                           if (_BuildPriority.GetEnabled(block) && projector.CanBuild(block, false) == BuildCheckResult.OK && block.IsInRange(ref areaBox, out distance))
                           {
                              if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: Add projected Block {1}:{2}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(projector), Logging.BlockName(block));
                              possibleWeldTargets.Add(new TargetBlockData(block, distance));
                           }
                        }
                     }
                  }
                  continue;
               }
            }
         }
      }

      /// <summary>
      /// 
      /// </summary>
      private void AsyncAddBlockIfTargetOrSource(ref MyOrientedBoundingBoxD areaBox, bool useIgnoreColor, ref Vector3 ignoreColor, bool useGrindColor, ref Vector3 grindColor, IMySlimBlock block, List<IMyInventory> possibleSources, List<TargetBlockData> possibleWeldTargets, List<TargetBlockData> possibleGrindTargets)
      {
         try
         {
            if (_Welder.UseConveyorSystem && possibleSources != null)
            {
               //Search for sources of components (Container, Assembler, Welder, Grinder, ?)
               var terminalBlock = block.FatBlock as IMyTerminalBlock;
               if (terminalBlock != null && terminalBlock.EntityId != _Welder.EntityId && terminalBlock.IsFunctional) //Own inventor is no external source (handled internaly)
               {
                  var relation = terminalBlock.GetUserRelationToOwner(_Welder.OwnerId);
                  if (relation != MyRelationsBetweenPlayerAndBlock.Enemies)
                  {
                     try
                     {
                        var welderInventory = _Welder.GetInventory(0);
                        var maxInv = terminalBlock.InventoryCount;
                        for (var idx = 0; idx < maxInv; idx++)
                        {
                           var inventory = terminalBlock.GetInventory(idx);
                           lock (inventory) //Protect 'IsConnectedTo' from use in the other threads, could be removed after MyInventory.IsConnected is Thread safe
                           {
                              if (inventory.IsConnectedTo(welderInventory))
                              {
                                 if (!possibleSources.Contains(inventory)) possibleSources.Add(inventory);
                              }
                           }
                        }
                     }
                     catch (Exception ex)
                     {
                        Mod.Log.Write(Logging.Level.Event, "BuildAndRepairSystemBlock {0}: AsyncIsTargetOrSource exception: {1}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), ex);
                     }
                  }
               };
            }

            if (possibleWeldTargets != null)
            {
               AsyncAddBlockIfWeldTarget(ref areaBox, useIgnoreColor, ref ignoreColor, useGrindColor, ref grindColor, block, possibleWeldTargets);
            }

            if (possibleGrindTargets != null && useGrindColor)
            {
               AsyncAddBlockIfGrindTarget(ref areaBox, ref grindColor, block, possibleGrindTargets);
            }
         }
         catch (Exception ex)
         {
            Mod.Log.Error("BuildAndRepairSystemBlock {0}: AsyncUpdateSourcesAndTargets1 exception: {1}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), ex);
            throw;
         }
      }

      /// <summary>
      /// Check if the given slim block is a weld target (in range, owned, damaged, new, ..)
      /// </summary>
      void AsyncAddBlockIfWeldTarget(ref MyOrientedBoundingBoxD areaBox, bool useIgnoreColor, ref Vector3 ignoreColor, bool useGrindColor, ref Vector3 grindColor, IMySlimBlock block, List<TargetBlockData> possibleWeldTargets)
      {
         if (Mod.Log.ShouldLog(Logging.Level.Verbose))
            Mod.Log.Write(Logging.Level.Verbose, "BuildAndRepairSystemBlock {0}: Check Block {1} IsDestroyed={2}, IsFullyDismounted={3}, HasFatBlock={4}, FatBlockClosed={5}, MaxDeformation={6}, (HasDeformation={7}), IsFullIntegrity={8} NeedRepair={9}",
            Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(block),
            block.IsDestroyed, block.IsFullyDismounted, block.FatBlock != null, block.FatBlock != null ? block.FatBlock.Closed.ToString() : "-",
            block.MaxDeformation, block.HasDeformation, block.IsFullIntegrity, block.NeedRepair());

         if (block.NeedRepair() && (!useIgnoreColor || ignoreColor != block.GetColorMask()) && (!useGrindColor || grindColor != block.GetColorMask()) && _BuildPriority.GetEnabled(block))
         {
            double distance;
            if (block.IsInRange(ref areaBox, out distance))
            {
               if (!IsRelationAllowed(block)) return;
               if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: Add damaged Block {1} MaxDeformation={2}, (HasDeformation={3}), IsFullIntegrity={4}, HasFatBlock={5}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(block), block.MaxDeformation, block.HasDeformation, block.IsFullIntegrity, block.FatBlock != null);
               possibleWeldTargets.Add(new TargetBlockData(block, distance));
            }
         }
      }

      /// <summary>
      /// Check if the given slim block is a grind target (in range, color )
      /// </summary>
      void AsyncAddBlockIfGrindTarget(ref MyOrientedBoundingBoxD areaBox, ref Vector3 grindColor, IMySlimBlock block, List<TargetBlockData> possibleGrindTargets)
      {
         //block.CubeGrid.BlocksDestructionEnabled is not available for modding, so at least check if general destruction is enabled
         if ((MyAPIGateway.Session.SessionSettings.Scenario || MyAPIGateway.Session.SessionSettings.ScenarioEditMode) && !MyAPIGateway.Session.SessionSettings.DestructibleBlocks) return;

         //block.CubeGrid.Editable is not available for modding -> wait until it might be availabel
         //if (!block.CubeGrid.Editable) return;

         if (Mod.Log.ShouldLog(Logging.Level.Verbose))
         Mod.Log.Write(Logging.Level.Verbose, "BuildAndRepairSystemBlock {0}: Check Block {1} Color={2} Projected={3}",
         Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(block), block.GetColorMask(), block.IsProjected());

         if (grindColor == block.GetColorMask() && !block.IsProjected())
         {
            double distance;
            if (block.IsInRange(ref areaBox, out distance))
            {
               if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemBlock {0}: Add grind Block {1}", Logging.BlockName(_Welder, Logging.BlockNameOptions.None), Logging.BlockName(block));
               possibleGrindTargets.Add(new TargetBlockData(block, distance));
            }
         }
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="block"></param>
      /// <returns></returns>
      private bool IsRelationAllowed(IMySlimBlock block) {
         var cubeBlock = block.FatBlock;
         if (cubeBlock == null) return true;

         var relation = cubeBlock.GetUserRelationToOwner(_Welder.OwnerId);
         if (relation == MyRelationsBetweenPlayerAndBlock.Enemies) return false;
         if (!_Welder.HelpOthers && relation != MyRelationsBetweenPlayerAndBlock.Owner && relation != MyRelationsBetweenPlayerAndBlock.NoOwnership) return false;
         return true;
      }

      /// <summary>
      /// Update custom info of the block
      /// </summary>
      /// <param name="block"></param>
      /// <param name="details"></param>
      private void AppendingCustomInfo(IMyTerminalBlock terminalBlock, StringBuilder details)
      {
         details.Clear();

         if ((_Welder.Enabled || MyAPIGateway.Session.CreativeMode) && _Welder.IsWorking && _Welder.IsFunctional)
         {
            if (Settings.ScriptControlled)
            {
               details.Append("Picked Welding Block:" + Environment.NewLine);
               details.Append(string.Format(" -{0}" + Environment.NewLine, Settings.CurrentPickedWeldingBlock.BlockName()));
               details.Append("Picked Grinding Block:" + Environment.NewLine);
               details.Append(string.Format(" -{0}" + Environment.NewLine, Settings.CurrentPickedGrindingBlock.BlockName()));
            }

            var cnt = 0;
            details.Append("Missing Items:" + Environment.NewLine);
            foreach (var component in State.MissingComponents)
            {
               details.Append(string.Format(" -{0}: {1}" + Environment.NewLine, component.Key.SubtypeName, component.Value));
               cnt++;
               if (cnt >= SyncBlockState.MaxSyncItems)
               {
                  details.Append(" -.." + Environment.NewLine);
                  break;
               }
            }
            details.Append(Environment.NewLine);

            cnt = 0;
            details.Append("Blocks to build:" + Environment.NewLine);
            lock (State.PossibleWeldTargets)
            {
               foreach (var blockData in State.PossibleWeldTargets)
               {
                  details.Append(string.Format(" -{0}" + Environment.NewLine,  blockData.Block.BlockName()));
                  cnt++;
                  if (cnt >= SyncBlockState.MaxSyncItems)
                  {
                     details.Append(" -.." + Environment.NewLine);
                     break;
                  }
               }
            }
            details.Append(Environment.NewLine);

            cnt = 0;
            details.Append("Blocks to dismantle:" + Environment.NewLine);
            lock (State.PossibleWeldTargets)
            {
               foreach (var blockData in State.PossibleGrindTargets)
               {
                  details.Append(string.Format(" -{0}" + Environment.NewLine, blockData.Block.BlockName()));
                  cnt++;
                  if (cnt >= SyncBlockState.MaxSyncItems)
                  {
                     details.Append(" -.." + Environment.NewLine);
                     break;
                  }
               }
            }
         }
         else
         {
            details.Append("Block is not ready" + Environment.NewLine);
         }
      }

      /// <summary>
      /// Check if block currently has been damaged by friendly(grinder)
      /// </summary>
      public bool IsFriendlyDamage(IMySlimBlock slimBlock)
      {
         var playTime = MyAPIGateway.Session.ElapsedPlayTime;
         if (playTime.Subtract(_LastFriendlyDamageCleanup) > NanobotBuildAndRepairSystemMod.Settings.FriendlyDamageCleanup)
         {
            //Cleanup
            var timedout = new List<IMySlimBlock>();
            foreach (var entry in FriendlyDamage)
            {
               if (entry.Value < playTime) timedout.Add(slimBlock);
            }
            for (var idx = timedout.Count - 1; idx >= 0; idx--)
            {
               FriendlyDamage.Remove(timedout[idx]);
            }
            _LastFriendlyDamageCleanup = playTime;
         }
         return FriendlyDamage.ContainsKey(slimBlock);
      }

      /// <summary>
      /// 
      /// </summary>
      /// <returns></returns>
      private WorkingState GetWorkingState()
      {
         if (!State.Ready) return WorkingState.NotReady;
         else if (State.Welding) return WorkingState.Welding;
         else if (State.NeedWelding)
         {
            if (State.MissingComponents.Count > 0) return WorkingState.MissingComponents;
            else return WorkingState.NeedWelding;
         }
         else if (State.Grinding) return WorkingState.Grinding;
         return WorkingState.Idle;
      }

      /// <summary>
      /// Set actual state and position of visual effects
      /// </summary>
      private void UpdateEffects()
      {
         var active = Settings.SearchMode == SearchModes.BoundingBox && (_CurrentTransportDestination != null || _CurrentTransportSource != null);
         if (active != _TransportStateSet)
         {
            SetTransportEffects(active);
            _TransportStateSet = active;
         }
         else
         {
            UpdateTransportEffectPosition();
         }

         //Welding/Grinding state
         var workingState = GetWorkingState();
         if (workingState != _WorkingStateSet)
         {
            SetWorkingEffects(workingState);
            _WorkingStateSet = workingState;
         }
         else
         {
            UpdateWorkingEffectPosition();
         }
      }

      /// <summary>
      /// Start visual effects for welding/grinding
      /// </summary>
      private void SetWorkingEffects(WorkingState workingState)
      {
         bool stopEffects = true;

         var sound = _Sounds[(int)workingState];
         if (sound != null)
         {
            if (_SoundEmitter != null)
            {
               _SoundEmitter.CustomVolume = Settings.SoundVolume;
               _SoundEmitter.PlaySingleSound(sound, true);
            }

            if (_SoundEmitterWorking != null)
            {
               _SoundEmitterWorking.CustomVolume = Settings.SoundVolume;
               _SoundEmitterWorking.PlaySingleSound(sound, true);
            }
         }

         switch (workingState) {
            case WorkingState.Welding:
            case WorkingState.Grinding:
               stopEffects = false;
               if (_ParticleEffectWorking1 != null) _ParticleEffectWorking1.Stop();
               MyParticlesManager.TryCreateParticleEffect(workingState == WorkingState.Welding ? (int)PARTICLE_EFFECT_WELDING1 : (int)PARTICLE_EFFECT_GRINDING1, out _ParticleEffectWorking1, false);

               if (_LightEffect != null) MyLights.RemoveLight(_LightEffect);
               if (workingState == WorkingState.Welding && _LightEffectFlareWelding == null)
               {
                  MyDefinitionId myDefinitionId = new MyDefinitionId(typeof(MyObjectBuilder_FlareDefinition), "ShipWelder");
                  _LightEffectFlareWelding = MyDefinitionManager.Static.GetDefinition(myDefinitionId) as MyFlareDefinition;
               }
               else if (workingState == WorkingState.Grinding && _LightEffectFlareGrinding == null)
               {
                  MyDefinitionId myDefinitionId = new MyDefinitionId(typeof(MyObjectBuilder_FlareDefinition), "ShipGrinder");
                  _LightEffectFlareGrinding = MyDefinitionManager.Static.GetDefinition(myDefinitionId) as MyFlareDefinition;
               }

               var flare = workingState == WorkingState.Welding ? _LightEffectFlareWelding : _LightEffectFlareGrinding;

               if (flare != null)
               {
                  _LightEffect = MyLights.AddLight();
                  _LightEffect.Start(Vector3.Zero, new Vector4(0.7f, 0.85f, 1f, 1f), 10f, string.Concat(_Welder.DisplayNameText, " EffectLight"));
                  _LightEffect.Falloff = 2f;
                  _LightEffect.LightOn = true;
                  _LightEffect.GlareOn = true;
                  _LightEffect.GlareQuerySize = 0.8f;
                  _LightEffect.Range = 10.0f;
                  _LightEffect.PointLightOffset = 0.1f;
                  _LightEffect.GlareType = VRageRender.Lights.MyGlareTypeEnum.Normal;
                  _LightEffect.SubGlares = flare.SubGlares;
                  _LightEffect.Intensity = flare.Intensity;
                  _LightEffect.GlareSize = flare.Size;
               }

               _Welder.SetEmissiveParts("EmissiveReady", Color.Green, 1.0f);
               _Welder.SetEmissiveParts("EmissiveWorking", workingState == WorkingState.Welding ? Color.Yellow : Color.Blue, 1.0f);

               UpdateWorkingEffectPosition();
               break;
            case WorkingState.MissingComponents:
               _Welder.SetEmissiveParts("EmissiveReady", Color.Red, 1.0f);
               _Welder.SetEmissiveParts("EmissiveWorking", Color.Yellow, 1.0f);
               break;
            case WorkingState.NeedWelding:
               _Welder.SetEmissiveParts("EmissiveReady", Color.Green, 1.0f);
               _Welder.SetEmissiveParts("EmissiveWorking", Color.Yellow, 1.0f);
               break;
            case WorkingState.Idle:
               _Welder.SetEmissiveParts("EmissiveReady", Color.Green, 1.0f);
               _Welder.SetEmissiveParts("EmissiveWorking", Color.Black, 1.0f);
               break;
            case WorkingState.NotReady:
               _Welder.SetEmissiveParts("EmissiveReady", Color.Black, 1.0f);
               _Welder.SetEmissiveParts("EmissiveWorking", Color.Black, 1.0f);
               break;
         }

         if (stopEffects) {
            if (_ParticleEffectWorking1 != null)
            {
               _ParticleEffectWorking1.Stop();
               _ParticleEffectWorking1 = null;
            }

            if (_LightEffect != null)
            {
               MyLights.RemoveLight(_LightEffect);
               _LightEffect = null;
            }
         }

         if (stopEffects || sound == null) {
            if (_SoundEmitter != null)
            {
               _SoundEmitter.StopSound(false);
            }

            if (_SoundEmitterWorking != null)
            {
               _SoundEmitterWorking.StopSound(false);
               _SoundEmitterWorking.SetPosition(null); //Reset
            }
         }
      }

      /// <summary>
      /// Set the position of the visual effects
      /// </summary>
      private void UpdateWorkingEffectPosition()
      {
         Vector3D position;
         MatrixD matrix;
         if (State.CurrentWeldingBlock != null)
         {
            BoundingBoxD box;
            State.CurrentWeldingBlock.GetWorldBoundingBox(out box, false);
            matrix = box.Matrix;
            position = matrix.Translation;
         }
         else if (State.CurrentGrindingBlock != null)
         {
            BoundingBoxD box;
            State.CurrentGrindingBlock.GetWorldBoundingBox(out box, false);
            matrix = box.Matrix;
            position = matrix.Translation;
         }
         else
         {
            matrix = _Welder.WorldMatrix;
            position = matrix.Translation;
         }

         if (_LightEffect != null)
         {
            _LightEffect.Position = position;
            _LightEffect.Intensity = MyUtils.GetRandomFloat(0.2f, 1f);
            _LightEffect.UpdateLight();
         }

         if (_ParticleEffectWorking1 != null) {
            _ParticleEffectWorking1.WorldMatrix = matrix;
         }

         if (_SoundEmitterWorking != null)
         {
            _SoundEmitterWorking.SetPosition(position);
         }
      }

      /// <summary>
      /// Start visual effects for transport
      /// </summary>
      private void SetTransportEffects(bool active)
      {
         if (active)
         {
            if (_ParticleEffectTransport1 != null) _ParticleEffectTransport1.Stop();
            MyParticlesManager.TryCreateParticleEffect((int)PARTICLE_EFFECT_TRANSPORT1, out _ParticleEffectTransport1);
            _ParticleEffectTransport1.UserScale = 0.1f;
            UpdateTransportEffectPosition();
         } else
         {
            if (_ParticleEffectTransport1 != null)
            {
               _ParticleEffectTransport1.Stop();
               _ParticleEffectTransport1 = null;
            }
         }
      }

      /// <summary>
      /// Set the position of the visual effects for transport
      /// </summary>
      private void UpdateTransportEffectPosition()
      {
         if (_ParticleEffectTransport1 == null) return;

         var playTime = MyAPIGateway.Session.ElapsedPlayTime;
         var elapsed = _TransportTime.Ticks != 0 ? (double)playTime.Subtract(_LastTransportStartTime).Ticks / _TransportTime.Ticks : 0d;
         elapsed = elapsed < 1 ? elapsed : 1;

         MatrixD startMatrix;
         var target = _CurrentTransportDestination ?? _CurrentTransportSource;
         if (target != null)
         {
            Vector3D endPosition;
            target.ComputeWorldCenter(out endPosition);

            startMatrix = _Welder.WorldMatrix;
            startMatrix.Translation = Vector3D.Transform(_EmitterPosition, _Welder.CubeGrid.WorldMatrix);

            var direction = endPosition - startMatrix.Translation;
            var distance = direction.Normalize();
            elapsed = (elapsed > 0.5 ? 1 - elapsed : elapsed) * 2;
            startMatrix.Translation += direction * (distance * elapsed);
         }
         else
         {
            startMatrix = _Welder.WorldMatrix;
            startMatrix.Translation += startMatrix.Forward * 0.2f;
         }

         _ParticleEffectTransport1.WorldMatrix = startMatrix;
      }

      /// <summary>
      /// Get a list of currently build/repairable blocks (Scripting)
      /// </summary>
      /// <returns></returns>
      internal Dictionary<VRage.Game.MyDefinitionId, int> GetMissingComponentsDict()
      {
         var dict = new Dictionary<VRage.Game.MyDefinitionId, int>();
         lock (State.MissingComponents)
         {
            foreach (var item in State.MissingComponents)
            {
               dict.Add(item.Key, item.Value);
            }
         }
         return dict;
      }

      /// <summary>
      /// Get a list of currently build/repairable blocks (Scripting)
      /// </summary>
      /// <returns></returns>
      internal List<VRage.Game.ModAPI.Ingame.IMySlimBlock> GetPossibleWeldTargetsList()
      {
         var list = new List<VRage.Game.ModAPI.Ingame.IMySlimBlock>();
         lock (State.PossibleWeldTargets)
         {
            foreach (var blockData in State.PossibleWeldTargets)
            {
               list.Add(blockData.Block);
            }
         }
         return list;
      }
   }

   public class TargetBlockData
   {
      public IMySlimBlock Block { get; internal set; }
      public double Distance { get; internal set; }
      public TargetBlockData(IMySlimBlock block, double distance)
      {
         Block = block;
         Distance = distance;
      }
   }
}
