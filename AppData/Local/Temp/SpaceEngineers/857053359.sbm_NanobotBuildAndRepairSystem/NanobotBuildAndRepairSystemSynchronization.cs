namespace SpaceEquipmentLtd.NanobotBuildAndRepairSystem
{
   using System;
   using System.Collections.Generic;
   using System.ComponentModel;
   using System.Xml.Serialization;

   using VRage.Game;
   using VRage.Game.ModAPI;
   using VRage.ModAPI;
   using VRage.ObjectBuilders;
   using VRageMath;

   using ProtoBuf;
   using Sandbox.Game.EntityComponents;
   using Sandbox.ModAPI;

   using SpaceEquipmentLtd.Utils;

   [ProtoContract(UseProtoMembersOnly = true)]
   public class SyncBlockId
   {
      [ProtoMember(1)]
      public long EntityId { get; set; }
      [ProtoMember(2)]
      public long GridId { get; set; }
      [ProtoMember(3)]
      public Vector3I Position { get; set; }

      public static SyncBlockId GetSyncBlockId(VRage.Game.ModAPI.Ingame.IMySlimBlock slimBlock)
      {
         if (slimBlock == null) return null;
         if (slimBlock.FatBlock != null)
         {
            return new SyncBlockId() { EntityId = slimBlock.FatBlock.EntityId };
         }
         else if (slimBlock.CubeGrid != null)
         {
            return new SyncBlockId() { EntityId = 0, GridId = slimBlock.CubeGrid.EntityId, Position = slimBlock.Position };
         }
         return null;
      }
      public static IMySlimBlock GetSlimBlock(SyncBlockId id)
      {
         if (id == null) return null;
         if (id.EntityId != 0)
         {
            IMyEntity entity;
            if (MyAPIGateway.Entities.TryGetEntityById(id.EntityId, out entity))
            {
               var block = entity as IMyCubeBlock;
               return block != null ? block.SlimBlock : null;
            }
         }
         if (id.GridId != 0)
         {
            IMyEntity entity;
            if (MyAPIGateway.Entities.TryGetEntityById(id.GridId, out entity))
            {
               var grid = entity as IMyCubeGrid;
               return grid != null ? grid.GetCubeBlock(id.Position) : null;
            }
         }
         return null;
      }
   }

   [ProtoContract(UseProtoMembersOnly = true)]
   public class SyncTargetBlockData
   {
      [ProtoMember(1)]
      public SyncBlockId Block { get; set; }
      [ProtoMember(2)]
      public double Distance { get; set; }
   }

   [ProtoContract(UseProtoMembersOnly = true)]
   public class SyncComponents
   {
      [ProtoMember(1)]
      public SerializableDefinitionId Component { get; set; }
      [ProtoMember(2)]
      public int Amount { get; set; }
   }

   /// <summary>
   /// The settings for Mod
   /// </summary>
   [ProtoContract(SkipConstructor = true, UseProtoMembersOnly = true)]
   public class SyncModSettings
   {
      private const int CurrentSettingsVersion = 4;
      [XmlElement]
      public int Version { get; set; }

      [ProtoMember(1), XmlElement]
      public Logging.Level LogLevel { get; set; }

      [XmlIgnore]
      public TimeSpan SourcesAndTargetsUpdateInterval { get; set; }

      [XmlIgnore]
      public TimeSpan FriendlyDamageTimeout { get; set; }

      [XmlIgnore]
      public TimeSpan FriendlyDamageCleanup { get; set; }

      [ProtoMember(2), XmlElement]
      public int Range { get; set; }

      [ProtoMember(3), XmlElement]
      public long SourcesAndTargetsUpdateIntervalTicks
      {
         get { return SourcesAndTargetsUpdateInterval.Ticks; }
         set { SourcesAndTargetsUpdateInterval = new TimeSpan(value); }
      }

      [ProtoMember(4), XmlElement]
      public long FriendlyDamageTimeoutTicks
      {
         get { return FriendlyDamageTimeout.Ticks; }
         set { FriendlyDamageTimeout = new TimeSpan(value); }
      }

      [ProtoMember(5), XmlElement]
      public long FriendlyDamageCleanupTicks
      {
         get { return FriendlyDamageCleanup.Ticks; }
         set { FriendlyDamageCleanup = new TimeSpan(value); }
      }

      [ProtoMember(8), XmlElement]
      public float MaximumRequiredElectricPowerTransport { get; set; }

      [ProtoMember(10), XmlElement]
      public SyncModSettingsWelder Welder { get; set; }


      public SyncModSettings()
      {
         LogLevel = Logging.Level.Error; //Default
         SourcesAndTargetsUpdateInterval = TimeSpan.FromSeconds(10);
         FriendlyDamageTimeout = TimeSpan.FromSeconds(60);
         FriendlyDamageCleanup = TimeSpan.FromSeconds(10);
         Range = NanobotBuildAndRepairSystemBlock.WELDER_RANGE_DEFAULT_IN_M;
         MaximumRequiredElectricPowerTransport = NanobotBuildAndRepairSystemBlock.WELDER_REQUIRED_ELECTRIC_POWER_TRANSPORT_DEFAULT;

         Welder = new SyncModSettingsWelder();
         Welder.AllowedSearchModes = SearchModes.BoundingBox | SearchModes.Grids;
         Welder.AllowedWorkModes = WorkModes.WeldBeforeGrind | WorkModes.GrindBeforeWeld | WorkModes.GrindIfWeldGetStuck;
         Welder.MaximumRequiredElectricPowerWelding = NanobotBuildAndRepairSystemBlock.WELDER_REQUIRED_ELECTRIC_POWER_WELDING_DEFAULT;
         Welder.MaximumRequiredElectricPowerGrinding = NanobotBuildAndRepairSystemBlock.WELDER_REQUIRED_ELECTRIC_POWER_GRINDING_DEFAULT;
         Welder.WeldingMultiplier = 1;
         Welder.GrindingMultiplier = 1;
      }

      public static SyncModSettings Load()
      {
         var world = false;
         SyncModSettings settings = null;
         try
         {
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage("ModSettings.xml", typeof(SyncModSettings)))
            {
               world = true;
               using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage("ModSettings.xml", typeof(SyncModSettings)))
               {
                  settings = MyAPIGateway.Utilities.SerializeFromXML<SyncModSettings>(reader.ReadToEnd());
                  Mod.Log.Write(Logging.Level.Info, "NanobotBuildAndRepairSystemSettings: Loaded from world file.");
               }
            }
            else if (MyAPIGateway.Utilities.FileExistsInLocalStorage("ModSettings.xml", typeof(SyncModSettings)))
            {
               using (var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage("ModSettings.xml", typeof(SyncModSettings)))
               {
                  settings = MyAPIGateway.Utilities.SerializeFromXML<SyncModSettings>(reader.ReadToEnd());
                  Mod.Log.Write(Logging.Level.Info, "NanobotBuildAndRepairSystemSettings: Loaded from local storage.");

                  Save(settings, true);

               }
            }

            if (settings != null)
            {
               var adjusted = false;
               if (settings.Version < CurrentSettingsVersion)
               {
                  Mod.Log.Write(Logging.Level.Info, "NanobotBuildAndRepairSystemSettings: Settings have old version: {0} update to {1}", settings.Version, CurrentSettingsVersion);
                  switch (settings.Version)
                  {
                     case 0:
                        settings.LogLevel = Logging.Level.Error;
                        break;
                  }

                  if (settings.Welder.AllowedSearchModes == 0) settings.Welder.AllowedSearchModes = SearchModes.Grids | SearchModes.BoundingBox;
                  if (settings.Welder.AllowedWorkModes == 0) settings.Welder.AllowedWorkModes = WorkModes.WeldBeforeGrind | WorkModes.GrindBeforeWeld | WorkModes.GrindIfWeldGetStuck;
                  if (settings.Welder.WeldingMultiplier == 0) settings.Welder.WeldingMultiplier = 1;
                  if (settings.Welder.GrindingMultiplier == 0) settings.Welder.GrindingMultiplier = 1;

                  adjusted = true;
                  settings.Version = CurrentSettingsVersion;
               }
               if (settings.Range > NanobotBuildAndRepairSystemBlock.WELDER_RANGE_MAX_IN_M)
               {
                  settings.Range = NanobotBuildAndRepairSystemBlock.WELDER_RANGE_MAX_IN_M;
                  adjusted = true;
               }
               else if (settings.Range < NanobotBuildAndRepairSystemBlock.WELDER_RANGE_MIN_IN_M)
               {
                  settings.Range = NanobotBuildAndRepairSystemBlock.WELDER_RANGE_MIN_IN_M;
                  adjusted = true;
               }

               if (settings.Welder.WeldingMultiplier < NanobotBuildAndRepairSystemBlock.WELDING_GRINDING_MULTIPLIER_MIN)
               {
                  settings.Welder.WeldingMultiplier = NanobotBuildAndRepairSystemBlock.WELDING_GRINDING_MULTIPLIER_MIN;
                  adjusted = true;
               }
               else if (settings.Welder.WeldingMultiplier >= NanobotBuildAndRepairSystemBlock.WELDING_GRINDING_MULTIPLIER_MAX)
               {
                  settings.Welder.WeldingMultiplier = NanobotBuildAndRepairSystemBlock.WELDING_GRINDING_MULTIPLIER_MAX;
                  adjusted = true;
               }

               if (settings.Welder.GrindingMultiplier < NanobotBuildAndRepairSystemBlock.WELDING_GRINDING_MULTIPLIER_MIN)
               {
                  settings.Welder.GrindingMultiplier = NanobotBuildAndRepairSystemBlock.WELDING_GRINDING_MULTIPLIER_MIN;
                  adjusted = true;
               }
               else if (settings.Welder.GrindingMultiplier >= NanobotBuildAndRepairSystemBlock.WELDING_GRINDING_MULTIPLIER_MAX)
               {
                  settings.Welder.GrindingMultiplier = NanobotBuildAndRepairSystemBlock.WELDING_GRINDING_MULTIPLIER_MAX;
                  adjusted = true;
               }

               Mod.Log.Write(Logging.Level.Info, "NanobotBuildAndRepairSystemSettings: Settings {0} {1} ", settings.Welder.GrindingMultiplier, settings);
               if (adjusted) Save(settings, world);
            } else
            {
               settings = new SyncModSettings() { Version = CurrentSettingsVersion };
               Save(settings, world);
            }
         }
         catch (Exception ex)
         {
            Mod.Log.Write(Logging.Level.Error, "NanobotBuildAndRepairSystemSettings: Exception while loading: {0}", ex);
         }

         return settings;
      }

      public static void Save(SyncModSettings settings, bool world)
      {
         if (world)
         {
            using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("ModSettings.xml", typeof(SyncModSettings)))
            {
               writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
            }
         }
         else
         {
            using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage("ModSettings.xml", typeof(SyncModSettings)))
            {
               writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
            }
         }
      }
   }

   [ProtoContract(SkipConstructor = true, UseProtoMembersOnly = true)]
   public class SyncModSettingsWelder
   {
      [ProtoMember(1), XmlElement]
      public float MaximumRequiredElectricPowerWelding { get; set; }

      [ProtoMember(2), XmlElement]
      public float MaximumRequiredElectricPowerGrinding { get; set; }

      [ProtoMember(10), XmlElement]
      public float WeldingMultiplier { get; set; }

      [ProtoMember(11), XmlElement]
      public float GrindingMultiplier { get; set; }

      [ProtoMember(100), XmlElement]
      public SearchModes AllowedSearchModes { get; set; }

      [ProtoMember(101), XmlElement, DefaultValue(false)]
      public bool AllowBuildFixed { get; set; }

      [ProtoMember(102), XmlElement, DefaultValue(false)]
      public bool AllowBuildDefault { get; set; }

      [ProtoMember(105), XmlElement]
      public WorkModes AllowedWorkModes { get; set; }

      [ProtoMember(110), XmlElement, DefaultValue(false)]
      public bool UseIgnoreColorFixed { get; set; }
      [ProtoMember(111), XmlElement, DefaultValue(false)]
      public bool UseIgnoreColorDefault { get; set; }
      [ProtoMember(112), XmlElement, DefaultValue(null)]
      public double[] IgnoreColorDefault { get; set; }

      [ProtoMember(115), XmlElement, DefaultValue(false)]
      public bool UseGrindColorFixed { get; set; }
      [ProtoMember(116), XmlElement, DefaultValue(false)]
      public bool UseGrindColorDefault { get; set; }
      [ProtoMember(117), XmlElement, DefaultValue(null)]
      public double[] GrindColorDefault { get; set; }

      [ProtoMember(120), XmlElement, DefaultValue(false)]
      public bool ShowAreaFixed { get; set; }
      
      [ProtoMember(130), XmlElement, DefaultValue(false)]
      public bool AreaSizeFixed { get; set; }

      [ProtoMember(140), XmlElement, DefaultValue(false)]
      public bool PriorityFixed { get; set; }

      [ProtoMember(150), XmlElement, DefaultValue(false)]
      public bool SoundVolumeFixed { get; set; }
      [ProtoMember(151), XmlElement, DefaultValue(0)]
      public float SoundVolumeDefault { get; set; }

      [ProtoMember(160), XmlElement, DefaultValue(false)]
      public bool ScriptControllFixed { get; set; }
   }


   /// <summary>
   /// The settings for Block
   /// </summary>
   [ProtoContract(SkipConstructor=true, UseProtoMembersOnly=true)]
   public class SyncBlockSettings
   {
      private BoundingBoxD _AreaBoundingBox;
      private bool _AllowBuild;
      private bool _UseIgnoreColor;
      private Vector3 _IgnoreColor;
      private bool _UseGrindColor;
      private Vector3 _GrindColor;
      private bool _ShowArea;
      private int _AreaWidthLeft;
      private int _AreaWidthRight;
      private int _AreaHeightTop;
      private int _AreaHeightBottom;
      private int _AreaDepthFront;
      private int _AreaDepthRear;
      private string _BuildPriority;
      private bool _ScriptControlled;
      private float _SoundVolume;
      private SearchModes _SearchMode;
      private WorkModes _WorkMode;
      private VRage.Game.ModAPI.Ingame.IMySlimBlock _CurrentPickedWeldingBlock;
      private VRage.Game.ModAPI.Ingame.IMySlimBlock _CurrentPickedGrindingBlock;
      private TimeSpan _LastStored;
      private TimeSpan _LastTransmitted;


      [XmlIgnore]
      public uint Changed { get; private set; }

      [ProtoMember(10), XmlElement]
      public bool AllowBuild
      {
         get
         {
            return _AllowBuild;
         }
         set
         {
            if (_AllowBuild != value)
            {
               _AllowBuild = value;
               Changed = 3u;
            }
         }
      }

      [ProtoMember(20), XmlElement]
      public SearchModes SearchMode
      {
         get
         {
            return _SearchMode;
         }
         set
         {
            if (_SearchMode != value)
            {
               _SearchMode = value;
               Changed = 3u;
            }
         }
      }

      [ProtoMember(25), XmlElement]
      public WorkModes WorkMode
      {
         get
         {
            return _WorkMode;
         }
         set
         {
            if (_WorkMode != value)
            {
               _WorkMode = value;
               Changed = 3u;
            }
         }
      }

      [ProtoMember(30), XmlElement]
      public bool UseIgnoreColor
      {
         get
         {
            return _UseIgnoreColor;
         }
         set
         {
            if (_UseIgnoreColor != value)
            {
               _UseIgnoreColor = value;
               Changed = 3u;
            }
         }
      }

      [ProtoMember(31), XmlElement]
      public Vector3 IgnoreColor
      {
         get
         {
            return _IgnoreColor;
         }
         set
         {
            if (_IgnoreColor != value)
            {
               _IgnoreColor = value;
               Changed = 3u;
            }
         }
      }

      [ProtoMember(35), XmlElement]
      public bool UseGrindColor
      {
         get
         {
            return _UseGrindColor;
         }
         set
         {
            if (_UseGrindColor != value)
            {
               _UseGrindColor = value;
               Changed = 3u;
            }
         }
      }

      [ProtoMember(36), XmlElement]
      public Vector3 GrindColor
      {
         get
         {
            return _GrindColor;
         }
         set
         {
            if (_GrindColor != value)
            {
               _GrindColor = value;
               Changed = 3u;
            }
         }
      }

      [ProtoMember(40), XmlElement]
      public bool ShowArea
      {
         get
         {
            return _ShowArea;
         }
         set
         {
            if (_ShowArea != value)
            {
               _ShowArea = value;
               Changed = 3u;
            }
         }
      }

      [ProtoMember(41), XmlElement]
      public int AreaWidthLeft
      {
         get
         {
            return _AreaWidthLeft;
         }
         set
         {
            if (_AreaWidthLeft != value)
            {
               _AreaWidthLeft = value;
               Changed = 3u;
               RecalcAreaBoundigBox();
            }
         }
      }

      [ProtoMember(42), XmlElement]
      public int AreaWidthRight
      {
         get
         {
            return _AreaWidthRight;
         }
         set
         {
            if (_AreaWidthRight != value)
            {
               _AreaWidthRight = value;
               Changed = 3u;
               RecalcAreaBoundigBox();
            }
         }
      }

      [ProtoMember(43), XmlElement]
      public int AreaHeightTop
      {
         get
         {
            return _AreaHeightTop;
         }
         set
         {
            if (_AreaHeightTop != value)
            {
               _AreaHeightTop = value;
               Changed = 3u;
               RecalcAreaBoundigBox();
            }
         }
      }

      [ProtoMember(44), XmlElement]
      public int AreaHeightBottom
      {
         get
         {
            return _AreaHeightBottom;
         }
         set
         {
            if (_AreaHeightBottom != value)
            {
               _AreaHeightBottom = value;
               Changed = 3u;
               RecalcAreaBoundigBox();
            }
         }
      }

      [ProtoMember(45), XmlElement]
      public int AreaDepthFront
      {
         get
         {
            return _AreaDepthFront;
         }
         set
         {
            if (_AreaDepthFront != value)
            {
               _AreaDepthFront = value;
               Changed = 3u;
               RecalcAreaBoundigBox();
            }
         }
      }

      [ProtoMember(46), XmlElement]
      public int AreaDepthRear
      {
         get
         {
            return _AreaDepthRear;
         }
         set
         {
            if (_AreaDepthRear != value)
            {
               _AreaDepthRear = value;
               Changed = 3u;
               RecalcAreaBoundigBox();
            }
         }
      }

      [ProtoMember(60), XmlElement]
      public string BuildPriority
      {
         get
         {
            return _BuildPriority;
         }
         set
         {
            if (_BuildPriority != value)
            {
               _BuildPriority = value;
               Changed = 3u;
            }
         }
      }

      [ProtoMember(70), XmlElement]
      public bool ScriptControlled
      {
         get
         {
            return _ScriptControlled;
         }
         set
         {
            if (_ScriptControlled != value)
            {
               _ScriptControlled = value;
               Changed = 3u;
            }
         }
      }

      
      [ProtoMember(80), XmlElement]
      public float SoundVolume
      {
         get
         {
            return _SoundVolume;
         }
         set
         {
            if (_SoundVolume != value)
            {
               _SoundVolume = value;
               Changed = 3u;
            }
         }
      }


      [XmlIgnore]
      public VRage.Game.ModAPI.Ingame.IMySlimBlock CurrentPickedWeldingBlock
      {
         get
         {
            return _CurrentPickedWeldingBlock;
         }
         set
         {
            if (_CurrentPickedWeldingBlock != value)
            {
               _CurrentPickedWeldingBlock = value;
               Changed = 3u;
            }
         }
      }

      [ProtoMember(100), XmlElement]
      public SyncBlockId CurrentPickedWeldingBlockSync
      {
         get
         {
            return SyncBlockId.GetSyncBlockId(_CurrentPickedWeldingBlock);
         }
         set
         {
            CurrentPickedWeldingBlock = SyncBlockId.GetSlimBlock(value);
         }
      }

      [XmlIgnore]
      public VRage.Game.ModAPI.Ingame.IMySlimBlock CurrentPickedGrindingBlock
      {
         get
         {
            return _CurrentPickedGrindingBlock;
         }
         set
         {
            if (_CurrentPickedGrindingBlock != value)
            {
               _CurrentPickedGrindingBlock = value;
               Changed = 3u;
            }
         }
      }

      [ProtoMember(105), XmlElement]
      public SyncBlockId CurrentPickedGrindingBlockSync
      {
         get
         {
            return SyncBlockId.GetSyncBlockId(_CurrentPickedGrindingBlock);
         }
         set
         {
            CurrentPickedGrindingBlock = SyncBlockId.GetSlimBlock(value);
         }
      }

      internal BoundingBoxD AreaBoundingBox
      {
         get
         {
            return _AreaBoundingBox;
         }
      }

      public SyncBlockSettings()
      {
         _AllowBuild = true;
         _UseIgnoreColor = false;
         _IgnoreColor = Vector3.Zero;
         _UseGrindColor = false;
         _GrindColor = Vector3.Zero;
         _ShowArea = false;
         _BuildPriority = string.Empty;
         _ScriptControlled = false;
         _SoundVolume = NanobotBuildAndRepairSystemBlock.WELDER_SOUND_VOLUME / 2;
         _SearchMode = SearchModes.Grids;
         _WorkMode = WorkModes.WeldBeforeGrind;

         Changed = 0;
         _LastStored = MyAPIGateway.Session.ElapsedPlayTime;
         _LastTransmitted = MyAPIGateway.Session.ElapsedPlayTime;

         RecalcAreaBoundigBox();
      }

      public void TrySave(IMyEntity entity, Guid guid)
      {
         if ((Changed & 2u) == 0) return;
         if (MyAPIGateway.Session.ElapsedPlayTime.Subtract(_LastStored) < TimeSpan.FromSeconds(20)) return;
         Save(entity, guid);
      }

      public void Save(IMyEntity entity, Guid guid)
      {
         if (entity.Storage == null)
         {
            entity.Storage = new MyModStorageComponent();
         }
         var storage = entity.Storage;
         storage[guid] = MyAPIGateway.Utilities.SerializeToXML(this);
         Changed = (Changed & ~2u);
         _LastStored = MyAPIGateway.Session.ElapsedPlayTime;
      }

      public static SyncBlockSettings Load(IMyEntity entity, Guid guid, NanobotBuildAndRepairSystemPriorityHandling buildPriority)
      {
         var storage = entity.Storage;
         string data;
         SyncBlockSettings settings = null;
         if (storage != null && storage.TryGetValue(guid, out data))
         {
            try
            {
               settings = MyAPIGateway.Utilities.SerializeFromXML<SyncBlockSettings>(data);
               if (settings != null)
               {
                  settings.RecalcAreaBoundigBox();
                  buildPriority.SetEntries(settings.BuildPriority);
                  settings.Changed = 0;
                  return settings;
               }
            }
            catch { }
         }

         settings = new SyncBlockSettings();
         var control = entity as IMyTerminalBlock;
         var system = control != null ? control.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>() : null;
         var maxValue = system != null ? system.WelderMaximumRange : NanobotBuildAndRepairSystemMod.Settings.Range;
         settings.AreaWidthLeft = maxValue;
         settings.AreaWidthRight = maxValue;
         settings.AreaHeightTop = maxValue;
         settings.AreaHeightBottom = maxValue;
         settings.AreaDepthFront = maxValue;
         settings.AreaDepthRear = maxValue;
         buildPriority.SetEntries(settings.BuildPriority);
         settings.Changed = 0;
         return settings;
      }

      public void AssignReceived(SyncBlockSettings newSettings, NanobotBuildAndRepairSystemPriorityHandling buildPriority)
      {
         _AllowBuild = newSettings.AllowBuild;
         _UseIgnoreColor = newSettings.UseIgnoreColor;
         _IgnoreColor = newSettings.IgnoreColor;
         _UseGrindColor = newSettings.UseGrindColor;
         _GrindColor = newSettings.GrindColor;
         _ShowArea = newSettings.ShowArea;

         _AreaWidthLeft = newSettings.AreaWidthLeft;
         _AreaWidthRight = newSettings.AreaWidthRight;
         _AreaHeightTop = newSettings.AreaHeightTop;
         _AreaHeightBottom = newSettings.AreaHeightBottom;
         _AreaDepthFront = newSettings.AreaDepthFront;
         _AreaDepthRear = newSettings.AreaDepthRear;

         _BuildPriority = newSettings.BuildPriority;

         _ScriptControlled = newSettings.ScriptControlled;
         _SoundVolume = newSettings.SoundVolume;
         _SearchMode = newSettings.SearchMode;
         _WorkMode = newSettings.WorkMode;

         RecalcAreaBoundigBox();
         buildPriority.SetEntries(BuildPriority);

         Changed = 2u;
      }

      public SyncBlockSettings GetTransmit()
      {
         _LastTransmitted = MyAPIGateway.Session.ElapsedPlayTime;
         Changed = Changed & ~1u;
         return this;
      }

      public bool IsTransmitNeeded()
      {
         return (Changed & 1u) != 0 && MyAPIGateway.Session.ElapsedPlayTime.Subtract(_LastTransmitted) >= TimeSpan.FromSeconds(2);
      }

      private void RecalcAreaBoundigBox()
      {
         var border = 0.25d;
         _AreaBoundingBox = new BoundingBoxD(new Vector3D(-AreaDepthRear + border, -AreaWidthLeft + border, -AreaHeightBottom + border), new Vector3D(AreaDepthFront - border, AreaWidthRight - border, AreaHeightTop - border));
      }

      public void CheckLimits(NanobotBuildAndRepairSystemBlock system)
      {
         var maxValue = system != null ? system.WelderMaximumRange : NanobotBuildAndRepairSystemMod.Settings.Range;
         if (AreaWidthLeft > maxValue) AreaWidthLeft = maxValue;
         if (AreaWidthRight > maxValue) AreaWidthRight = maxValue;
         if (AreaHeightTop > maxValue) AreaHeightTop = maxValue;
         if (AreaHeightBottom > maxValue) AreaHeightBottom = maxValue;
         if (AreaDepthFront > maxValue) AreaDepthFront = maxValue;
         if (AreaDepthRear > maxValue) AreaDepthRear = maxValue;

         if (NanobotBuildAndRepairSystemMod.Settings.Welder.AllowBuildFixed) AllowBuild = NanobotBuildAndRepairSystemMod.Settings.Welder.AllowBuildDefault;
         if (NanobotBuildAndRepairSystemMod.Settings.Welder.UseIgnoreColorFixed)
         {
            UseIgnoreColor = NanobotBuildAndRepairSystemMod.Settings.Welder.UseIgnoreColorDefault;
            if (NanobotBuildAndRepairSystemMod.Settings.Welder.IgnoreColorDefault != null && NanobotBuildAndRepairSystemMod.Settings.Welder.IgnoreColorDefault.Length >= 3)
               IgnoreColor = new Vector3D(NanobotBuildAndRepairSystemMod.Settings.Welder.IgnoreColorDefault[0],
                  NanobotBuildAndRepairSystemMod.Settings.Welder.IgnoreColorDefault[1],
                  NanobotBuildAndRepairSystemMod.Settings.Welder.IgnoreColorDefault[2]);
         }
         if (NanobotBuildAndRepairSystemMod.Settings.Welder.UseGrindColorFixed)
         {
            UseGrindColor = NanobotBuildAndRepairSystemMod.Settings.Welder.UseGrindColorDefault;
            if (NanobotBuildAndRepairSystemMod.Settings.Welder.GrindColorDefault != null && NanobotBuildAndRepairSystemMod.Settings.Welder.GrindColorDefault.Length >= 3)
               GrindColor = new Vector3D(NanobotBuildAndRepairSystemMod.Settings.Welder.GrindColorDefault[0],
                  NanobotBuildAndRepairSystemMod.Settings.Welder.GrindColorDefault[1],
                  NanobotBuildAndRepairSystemMod.Settings.Welder.GrindColorDefault[2]);
         }
         if (NanobotBuildAndRepairSystemMod.Settings.Welder.ShowAreaFixed) ShowArea = false;
         if (NanobotBuildAndRepairSystemMod.Settings.Welder.AreaSizeFixed) {
            AreaWidthLeft = maxValue;
            AreaWidthRight = maxValue;
            AreaHeightTop = maxValue;
            AreaHeightBottom = maxValue;
            AreaDepthFront = maxValue;
            AreaDepthRear = maxValue;
         }
         if (NanobotBuildAndRepairSystemMod.Settings.Welder.SoundVolumeFixed) SoundVolume = NanobotBuildAndRepairSystemMod.Settings.Welder.SoundVolumeDefault;
         if (NanobotBuildAndRepairSystemMod.Settings.Welder.ScriptControllFixed) ScriptControlled = false;

         if ((NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedSearchModes & SearchMode) == 0)
         {
            if ((NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedSearchModes & SearchModes.Grids) != 0) SearchMode = SearchModes.Grids;
            else if ((NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedSearchModes & SearchModes.BoundingBox) != 0) SearchMode = SearchModes.BoundingBox;
         }

         if ((NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedWorkModes & WorkMode) == 0)
         {
            if ((NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedWorkModes & WorkModes.WeldBeforeGrind) != 0) WorkMode = WorkModes.WeldBeforeGrind;
            else if ((NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedWorkModes & WorkModes.GrindBeforeWeld) != 0) WorkMode = WorkModes.GrindBeforeWeld;
            else if ((NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedWorkModes & WorkModes.GrindIfWeldGetStuck) != 0) WorkMode = WorkModes.GrindIfWeldGetStuck;
         }
      }
   }

   /// <summary>
   /// Current State of block
   /// </summary>
   [ProtoContract(SkipConstructor = true, UseProtoMembersOnly = true)]
   public class SyncBlockState
   {
      public const int MaxSyncItems = 20;

      private TimeSpan _LastTransmitted;
      private bool _Ready;
      private bool _Welding;
      private bool _NeedWelding;
      private bool _Grinding;
      private bool _NeedGrinding;
      private bool _InventoryFull;
      private IMySlimBlock _CurrentWeldingBlock;
      private IMySlimBlock _CurrentGrindingBlock;
      private List<SyncComponents> _MissingComponentsSync;
      private List<SyncTargetBlockData> _PossibleWeldTargetsSync;
      private List<SyncTargetBlockData> _PossibleGrindTargetsSync;

      public bool Changed { get; private set; }

      [ProtoMember(1)]
      public bool Ready
      {
         get { return _Ready; }
         set
         {
            if (value != _Ready)
            {
               _Ready = value;
               Changed = true;
            }
         }
      }

      [ProtoMember(2)]
      public bool Welding {
         get { return _Welding; }
         set
         {
            if (value != _Welding)
            {
               _Welding = value;
               Changed = true;
            }
         }
      }

      [ProtoMember(3)]
      public bool NeedWelding {
         get { return _NeedWelding; }
         set
         {
            if (value != _NeedWelding)
            {
               _NeedWelding = value;
               Changed = true;
            }
         }
      }

      [ProtoMember(4)]
      public bool Grinding
      {
         get { return _Grinding; }
         set
         {
            if (value != _Grinding)
            {
               _Grinding = value;
               Changed = true;
            }
         }
      }

      [ProtoMember(5)]
      public bool NeedGrinding
      {
         get { return _NeedGrinding; }
         set
         {
            if (value != _NeedGrinding)
            {
               _NeedGrinding = value;
               Changed = true;
            }
         }
      }

      public IMySlimBlock CurrentWeldingBlock {
         get { return _CurrentWeldingBlock; }
         set
         {
            if (value != _CurrentWeldingBlock)
            {
               _CurrentWeldingBlock = value;
               Changed = true;
            }
         }
      }

      [ProtoMember(10)]
      public SyncBlockId CurrentWeldingBlockSync
      {
         get
         {
            return SyncBlockId.GetSyncBlockId(_CurrentWeldingBlock);
         }
         set
         {
            CurrentWeldingBlock = SyncBlockId.GetSlimBlock(value);
         }
      }

      public IMySlimBlock CurrentGrindingBlock
      {
         get { return _CurrentGrindingBlock; }
         set
         {
            if (value != _CurrentGrindingBlock)
            {
               _CurrentGrindingBlock = value;
               Changed = true;
            }
         }
      }

      [ProtoMember(15)]
      public SyncBlockId CurrentGrindingBlockSync
      {
         get
         {
            return SyncBlockId.GetSyncBlockId(_CurrentGrindingBlock);
         }
         set
         {
            CurrentGrindingBlock = SyncBlockId.GetSlimBlock(value);
         }
      }

      public Dictionary<MyDefinitionId, int> MissingComponents { get; private set; }

      [ProtoMember(20)]
      public List<SyncComponents> MissingComponentsSync {
         get
         {
            if (_MissingComponentsSync == null)
            {
               _MissingComponentsSync = new List<SyncComponents>();
               var idx = 0;
               if (MissingComponents != null)
               {
                  foreach (var item in MissingComponents)
                  {
                     _MissingComponentsSync.Add(new SyncComponents() { Component = item.Key, Amount = item.Value });
                     idx++;
                     if (idx > MaxSyncItems) break;
                  }
               }
            }
            return _MissingComponentsSync;
         }
      }

      [ProtoMember(21)]
      public bool InventoryFull
      {
         get { return _InventoryFull; }
         set
         {
            if (value != _InventoryFull)
            {
               _InventoryFull = value;
               Changed = true;
            }
         }
      }

      public List<TargetBlockData> PossibleWeldTargets { get; private set; }

      [ProtoMember(30)]
      public List<SyncTargetBlockData> PossibleWeldTargetsSync
      {
         get
         {
            if (_PossibleWeldTargetsSync == null)
            {
               _PossibleWeldTargetsSync = new List<SyncTargetBlockData>();
               var idx = 0;
               if (PossibleWeldTargets != null)
               {
                  foreach (var item in PossibleWeldTargets)
                  {
                     _PossibleWeldTargetsSync.Add(new SyncTargetBlockData() { Block = SyncBlockId.GetSyncBlockId(item.Block), Distance = item.Distance });
                     idx++;
                     if (idx > MaxSyncItems) break;
                  }
               }
            }
            return _PossibleWeldTargetsSync;
         }
      }

      public List<TargetBlockData> PossibleGrindTargets { get; private set; }

      [ProtoMember(35)]
      public List<SyncTargetBlockData> PossibleGrindTargetsSync
      {
         get
         {
            if (_PossibleGrindTargetsSync == null)
            {
               _PossibleGrindTargetsSync = new List<SyncTargetBlockData>();
               var idx = 0;
               if (PossibleGrindTargets != null)
               {
                  foreach (var item in PossibleGrindTargets)
                  {
                     _PossibleGrindTargetsSync.Add(new SyncTargetBlockData() { Block = SyncBlockId.GetSyncBlockId(item.Block), Distance = item.Distance });
                     idx++;
                     if (idx > MaxSyncItems) break;
                  }
               }
            }
            return _PossibleGrindTargetsSync;
         }
      }

      public SyncBlockState() {
         MissingComponents = new Dictionary<MyDefinitionId, int>();
         PossibleWeldTargets = new List<TargetBlockData>();
         PossibleGrindTargets = new List<TargetBlockData>();
      }

      internal void HasChanged()
      {
         Changed = true;
      }

      internal bool IsTransmitNeeded()
      {
         return Changed && MyAPIGateway.Session.ElapsedPlayTime.Subtract(_LastTransmitted) >= TimeSpan.FromSeconds(2);
      }

      internal SyncBlockState GetTransmit()
      {
         _MissingComponentsSync = null;
         _PossibleWeldTargetsSync = null;
         _PossibleGrindTargetsSync = null;
         _LastTransmitted = MyAPIGateway.Session.ElapsedPlayTime;
         Changed = false;
         return this;
      }

      internal void AssignReceived(SyncBlockState newState)
      {
         _Welding = newState.Welding;
         _NeedWelding = newState.NeedWelding;
         _CurrentWeldingBlock = newState.CurrentWeldingBlock;
         _Grinding = newState.Grinding;
         _NeedGrinding = newState.NeedGrinding;
         _CurrentGrindingBlock = newState.CurrentGrindingBlock;

         MissingComponents.Clear();
         foreach (var item in newState.MissingComponentsSync) MissingComponents.Add(item.Component, item.Amount);
         PossibleWeldTargets.Clear();
         foreach (var item in newState.PossibleWeldTargetsSync) PossibleWeldTargets.Add(new TargetBlockData(SyncBlockId.GetSlimBlock(item.Block), item.Distance));
         PossibleGrindTargets.Clear();
         foreach (var item in newState.PossibleGrindTargetsSync) PossibleGrindTargets.Add(new TargetBlockData(SyncBlockId.GetSlimBlock(item.Block), item.Distance));

         Changed = true;
      }

      internal void ResetChanged()
      {
         Changed = false;
      }
   }

   [ProtoContract(SkipConstructor = true, UseProtoMembersOnly = true)]
   public class MsgModDataRequest
   {
      [ProtoMember(1)]
      public ulong SteamId { get; set; }
   }

   [ProtoContract(SkipConstructor = true, UseProtoMembersOnly = true)]
   public class MsgModSettings
   {
      [ProtoMember(2)]
      public SyncModSettings Settings { get; set; }
   }

   [ProtoContract(SkipConstructor = true, UseProtoMembersOnly = true)]
   public class MsgBlockDataRequest
   {
      [ProtoMember(1)]
      public ulong SteamId { get; set; }
      [ProtoMember(2)]
      public long EntityId { get; set; }
   }

   [ProtoContract(SkipConstructor = true, UseProtoMembersOnly = true)]
   public class MsgBlockSettings
   {
      [ProtoMember(1)]
      public long EntityId { get; set; }
      [ProtoMember(2)]
      public SyncBlockSettings Settings { get; set; }
   }

   [ProtoContract(SkipConstructor = true, UseProtoMembersOnly = true)]
   public class MsgBlockState
   {
      [ProtoMember(1)]
      public long EntityId { get; set; }
      [ProtoMember(2)]
      public SyncBlockState State { get; set; }
   }

   public static class SyncExtensions
   {
      public static long GetHash<TKey, TValue>(this Dictionary<TKey, TValue> dict)
      {
         uint hash = 0;
         var idx = 0;
         foreach (var entry in dict)
         {
            hash ^= RotateLeft((uint)entry.GetHashCode(), idx+1);
            idx++;
            if (idx >= SyncBlockState.MaxSyncItems) break;
         }
         return hash;
      }

      public static long GetHash(this List<TargetBlockData> list)
      {
         uint hash = 0;
         var idx = 0;
         foreach (var entry in list)
         {
            hash ^= RotateLeft((uint)entry.Block.GetHashCode(), idx + 1);
            idx++;
            if (idx >= SyncBlockState.MaxSyncItems) break;
         }
         return hash;
      }

      private static uint RotateLeft(uint x, int n)
      {
         return (x << n) | (x >> (32 - n));
      }
   }
}
