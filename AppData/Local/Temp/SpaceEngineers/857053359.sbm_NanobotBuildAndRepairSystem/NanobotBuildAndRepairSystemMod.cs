namespace SpaceEquipmentLtd.NanobotBuildAndRepairSystem
{
   using System;
   using System.Collections.Generic;

   using VRage.Game;
   using VRage.Game.Components;
   using VRage.Game.ModAPI;
   using VRage.ModAPI;

   using Sandbox.ModAPI;
   using Sandbox.ModAPI.Weapons;

   using SpaceEquipmentLtd.Utils;

   static class Mod
   {
      public static Logging Log = new Logging("NanobotBuildAndRepairSystem", 0, "NanobotBuildAndRepairSystem.log", typeof(NanobotBuildAndRepairSystemMod)) {
         LogLevel = Logging.Level.Error | Logging.Level.Event, //Default
         EnableHudNotification = false
      };
   }

   [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
   public class NanobotBuildAndRepairSystemMod : MySessionComponentBase
   {
      private const string CmdKey = "/nanobars";
      private const string CmdHelp1 = "/?";
      private const string CmdHelp2 = "/help";
      private const string CmdCwsf = "/cwsf";
      private const string CmdLogLevel = "/loglevel";
      private const string CmdLogLevel_All = "all";
      private const string CmdLogLevel_Default = "default";

      private const string CmdHelpText_Client = "Available commands:" +
         "\n["+ CmdHelp1 + ";" + CmdHelp2 + "]: Shows this info" +
         "\n["+ CmdLogLevel + " "+ CmdLogLevel_All + ";" + CmdLogLevel_Default + "]: Set the current logging level. Warning: Setting level to All could produce very large log-files";
      private const string CmdHelpText_Server = CmdHelpText_Client +
         "\n["+ CmdCwsf + "]: Creates a settings file inside your current world folder. After restart the settings in this file will be used, instead of the global mod-settings file.";

      private static ushort MSGID_MOD_DATAREQUEST = 40000;
      private static ushort MSGID_MOD_SETTINGS = 40001;
      private static ushort MSGID_BLOCK_DATAREQUEST = 40100;
      private static ushort MSGID_BLOCK_SETTINGS_FROM_SERVER = 40102;
      private static ushort MSGID_BLOCK_SETTINGS_FROM_CLIENT = 40103;
      private static ushort MSGID_BLOCK_STATE_FROM_SERVER = 40104;

      private bool _Init = false;
      public static bool SettingsValid = false;
      public static SyncModSettings Settings = new SyncModSettings();
      private static TimeSpan _LastSourcesAndTargetsUpdate;

      public static NanobotBuildAndRepairSystemMod Instance { get; private set; }
      public static Guid ModGuid = new Guid("8B57046C-DA20-4DE1-8E35-513FD21E3B9F");

      /// <summary>
      /// Current known Build and Repair Systems in world
      /// </summary>
      private static Dictionary<long, NanobotBuildAndRepairSystemBlock> _BuildAndRepairSystems;
      public static Dictionary<long, NanobotBuildAndRepairSystemBlock> BuildAndRepairSystems
      {
         get
         {
            return _BuildAndRepairSystems != null ? _BuildAndRepairSystems : _BuildAndRepairSystems = new Dictionary<long, NanobotBuildAndRepairSystemBlock>();
         }
      }

      /// <summary>
      /// 
      /// </summary>
      public void Init()
      {
         Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: Initializing.");
         _Init = true;

         Settings = SyncModSettings.Load();
         SettingsValid = MyAPIGateway.Session.IsServer;
         foreach (var entry in BuildAndRepairSystems)
         {
            entry.Value.SettingsChanged();
         }

         Mod.Log.LogLevel = Settings.LogLevel;

         MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, BeforeDamageHandlerNoDamageByBuildAndRepairSystem);
         if (MyAPIGateway.Session.IsServer)
         {
            //Detect friendly damage onl needed on server
            MyAPIGateway.Session.DamageSystem.RegisterAfterDamageHandler(100, AfterDamageHandlerNoDamageByBuildAndRepairSystem);
         }

         if (MyAPIGateway.Utilities.IsDedicated)
         {
            MyAPIGateway.Multiplayer.RegisterMessageHandler(MSGID_MOD_DATAREQUEST, SyncModDataRequestReceived);
            MyAPIGateway.Multiplayer.RegisterMessageHandler(MSGID_BLOCK_DATAREQUEST, SyncBlockDataRequestReceived);
            //Same as MSGID_BLOCK_SETTINGS but SendMessageToOthers sends also to self, which will result in stack overflow
            MyAPIGateway.Multiplayer.RegisterMessageHandler(MSGID_BLOCK_SETTINGS_FROM_CLIENT, SyncBlockSettingsReceived);
         }
         else if (!MyAPIGateway.Session.IsServer) {
            MyAPIGateway.Multiplayer.RegisterMessageHandler(MSGID_MOD_SETTINGS, SyncModSettingsReceived);
            MyAPIGateway.Multiplayer.RegisterMessageHandler(MSGID_BLOCK_SETTINGS_FROM_SERVER, SyncBlockSettingsReceived);
            MyAPIGateway.Multiplayer.RegisterMessageHandler(MSGID_BLOCK_STATE_FROM_SERVER, SyncBlockStateReceived);

            SyncModDataRequestSend();
         }
         MyAPIGateway.Utilities.MessageEntered += Utilities_MessageEntered;

         Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: Initialized.");
      }

      private void Utilities_MessageEntered(string messageText, ref bool sendToOthers)
      {
         if (string.IsNullOrEmpty(messageText)) return;
         var cmd = messageText.ToLower();
         if (cmd.StartsWith(CmdKey))
         {
            Mod.Log.Write(Logging.Level.Error, "BuildAndRepairSystemMod: Cmd: {0}", messageText);
            var args = cmd.Remove(0, CmdKey.Length).Trim().Split(' ');
            if (args.Length > 0)
            {
               Mod.Log.Write(Logging.Level.Error, "BuildAndRepairSystemMod: Cmd args[0]: {0}", args[0]);
               switch (args[0].Trim())
               {
                  case CmdCwsf:
                     if (MyAPIGateway.Session.IsServer)
                     {
                        SyncModSettings.Save(Settings, true);
                        MyAPIGateway.Utilities.ShowMessage(CmdKey, "Settings file created inside world folder");
                     } else
                     {
                        MyAPIGateway.Utilities.ShowMessage(CmdKey, "command not allowed on client");
                     }
                     break;
                  case CmdLogLevel:
                     if (args.Length > 1)
                     {
                        switch (args[1].Trim())
                        {
                           case CmdLogLevel_All:
                              Mod.Log.LogLevel = Logging.Level.All;
                              MyAPIGateway.Utilities.ShowMessage(CmdKey, string.Format("Logging level switched to All [{0:X}]", Mod.Log.LogLevel));
                              break;
                           case CmdLogLevel_Default:
                           default:
                              Mod.Log.LogLevel = Settings.LogLevel;
                              MyAPIGateway.Utilities.ShowMessage(CmdKey, string.Format("Logging level switched to Default [{0:X}]", Mod.Log.LogLevel));
                              break;
                        }
                     }
                     break;
                  case CmdHelp1:
                  case CmdHelp2:
                  default:
                     MyAPIGateway.Utilities.ShowMissionScreen("NanobotBuildAndRepairSystem", "Help", "", MyAPIGateway.Session.IsServer ? CmdHelpText_Server : CmdHelpText_Client);
                     break;
               }
            } else
            {
               MyAPIGateway.Utilities.ShowMissionScreen("NanobotBuildAndRepairSystem", "Help", "", MyAPIGateway.Session.IsServer ? CmdHelpText_Server : CmdHelpText_Client);
            }
            sendToOthers = false;
         }
      }

      /// <summary>
      /// 
      /// </summary>
      protected override void UnloadData()
      {
         _Init = false;

         if (MyAPIGateway.Utilities.IsDedicated)
         {
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(MSGID_MOD_DATAREQUEST, SyncModDataRequestReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(MSGID_BLOCK_DATAREQUEST, SyncBlockDataRequestReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(MSGID_BLOCK_SETTINGS_FROM_CLIENT, SyncBlockSettingsReceived);
         }
         else if (!MyAPIGateway.Session.IsServer)
         {
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(MSGID_MOD_SETTINGS, SyncModSettingsReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(MSGID_BLOCK_SETTINGS_FROM_SERVER, SyncBlockSettingsReceived);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(MSGID_BLOCK_STATE_FROM_SERVER, SyncBlockStateReceived);
         }
         Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: UnloadData.");
         Mod.Log.Close();
      }

      /// <summary>
      /// 
      /// </summary>
      public override void UpdateBeforeSimulation()
      {
         try
         {
            if (!_Init)
            {
               if (MyAPIGateway.Session == null)
                  return;
               Init();
            }
            else
            {
               if (MyAPIGateway.Session.IsServer)
               {
                  AsyncRebuildSourcesAndTargets();
               }
            }
         }
         catch (Exception e)
         {
            Mod.Log.Error(e);
         }
      }

      /// <summary>
      /// Damage Handler: Prevent Damage from BuildAndRepairSystem
      /// </summary>
      public void BeforeDamageHandlerNoDamageByBuildAndRepairSystem(object target, ref MyDamageInformation info)
      {
         try
         {
            if (info.Type == MyDamageType.Weld)
            {
               if (target is IMyCharacter)
               {
                  var logicalComponent = BuildAndRepairSystems.GetValueOrDefault(info.AttackerId);
                  if (logicalComponent != null)
                  {
                     var terminalBlock = logicalComponent.Entity as IMyTerminalBlock;
                     if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: Prevent Damage from BuildAndRepairSystem={0} Amount={1}", terminalBlock != null ? terminalBlock.CustomName : logicalComponent.Entity.DisplayName, info.Amount);
                     info.Amount = 0;
                  }
               }
            }
         }
         catch (Exception e)
         {
            Mod.Log.Error("BuildAndRepairSystemMod: Exception in BeforeDamageHandlerNoDamageByBuildAndRepairSystem: Source={0}, Message={1}", e.Source, e.Message);
         }
      }

      /// <summary>
      /// Damage Handler: Register friendly damage
      /// </summary>
      public void AfterDamageHandlerNoDamageByBuildAndRepairSystem(object target, MyDamageInformation info)
      {
         try
         {
            if (info.Type == MyDamageType.Grind && info.Amount > 0)
            {
               var targetBlock = target as IMySlimBlock;
               if (targetBlock != null)
               {
                  IMyEntity attackerEntity;
                  MyAPIGateway.Entities.TryGetEntityById(info.AttackerId, out attackerEntity);

                  var attackerId = 0L;

                  var shipGrinder = attackerEntity as IMyShipGrinder;
                  if (shipGrinder != null)
                  {
                     attackerId = shipGrinder.OwnerId;
                  }
                  else
                  {
                     var characterGrinder = attackerEntity as IMyEngineerToolBase;
                     if (characterGrinder != null)
                     {
                        attackerId = characterGrinder.OwnerIdentityId;
                        if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: AfterDamaged1 {0} from {1} Amount={2}", Logging.BlockName(target), Logging.BlockName(characterGrinder), info.Amount);
                     }
                  }

                  if (attackerId != 0)
                  {
                     if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: Damaged {0} from {1} Amount={2}", Logging.BlockName(target), attackerId, info.Amount);
                     foreach (var entry in BuildAndRepairSystems)
                     {
                        var relation = entry.Value.Welder.GetUserRelationToOwner(attackerId);
                        if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: Damaged Check Add FriendlyDamage {0} relation {1}", Logging.BlockName(targetBlock), relation);
                        if (MyRelationsBetweenPlayerAndBlockExtensions.IsFriendly(relation))
                        {
                           //A 'friendly' damage from grinder -> do not repair (for a while)
                           //I don't check block relation here, because if it is enemy we won't repair it in any case and it just times out
                           entry.Value.FriendlyDamage[targetBlock] = MyAPIGateway.Session.ElapsedPlayTime + Settings.FriendlyDamageTimeout;
                           if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: Damaged Add FriendlyDamage {0} Timeout {1}", Logging.BlockName(targetBlock), entry.Value.FriendlyDamage[targetBlock]);
                        }
                     }
                  }
               }
            }
         }
         catch (Exception e)
         {
            Mod.Log.Error("BuildAndRepairSystemMod: Exception in BeforeDamageHandlerNoDamageByBuildAndRepairSystem: Source={0}, Message={1}", e.Source, e.Message);
         }
      }

      /// <summary>
      /// Rebuild the list of targets and inventory sources
      /// </summary>
      protected void AsyncRebuildSourcesAndTargets()
      {
         if (MyAPIGateway.Session.ElapsedPlayTime.Subtract(_LastSourcesAndTargetsUpdate) > Settings.SourcesAndTargetsUpdateInterval)
         {
            foreach (var buildAndRepairSystem in BuildAndRepairSystems.Values)
            {
               buildAndRepairSystem.AsyncUpdateSourcesAndTargets((buildAndRepairSystem.SourcesAndTargetsUpdateRun % 6) == 0);
               buildAndRepairSystem.SourcesAndTargetsUpdateRun++;
            }
            _LastSourcesAndTargetsUpdate = MyAPIGateway.Session.ElapsedPlayTime;
         }
      }

      /// <summary>
      /// 
      /// </summary>
      private void SyncModDataRequestSend()
      {
         if (!MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Session.IsServer) return;

         var msgSnd = new MsgModDataRequest();
         if (MyAPIGateway.Session.Player != null)
            msgSnd.SteamId = MyAPIGateway.Session.Player.SteamUserId;
         else
            msgSnd.SteamId = 0;

         if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: SyncModDataRequestSend {0}", msgSnd.SteamId);
         MyAPIGateway.Multiplayer.SendMessageToServer(MSGID_MOD_DATAREQUEST, MyAPIGateway.Utilities.SerializeToBinary(msgSnd), true);
      }

      /// <summary>
      /// 
      /// </summary>
      private void SyncModDataRequestReceived(byte[] dataRcv)
      {
         var msgRcv = MyAPIGateway.Utilities.SerializeFromBinary<MsgModDataRequest>(dataRcv);
         if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: SyncModDataRequestReceived {0}", msgRcv.SteamId);
         SyncModSettingsSend(msgRcv.SteamId);
      }

      /// <summary>
      /// 
      /// </summary>
      private void SyncModSettingsSend(ulong steamId)
      {
         if (!MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Session.IsServer) return;

         var msgSnd = new MsgModSettings();
         msgSnd.Settings = Settings;
         if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: SyncModSettingsSend {0}", steamId);
         if (!MyAPIGateway.Multiplayer.SendMessageTo(MSGID_MOD_SETTINGS, MyAPIGateway.Utilities.SerializeToBinary(msgSnd), steamId, true))
         {
            if (Mod.Log.ShouldLog(Logging.Level.Error)) Mod.Log.Write(Logging.Level.Error, "BuildAndRepairSystemMod: SyncModSettingsSend failed");
         }
      }

      /// <summary>
      /// 
      /// </summary>
      private void SyncModSettingsReceived(byte[] dataRcv)
      {
         try
         {
            var msgRcv = MyAPIGateway.Utilities.SerializeFromBinary<MsgModSettings>(dataRcv);
            Settings = msgRcv.Settings;
            SettingsValid = true;
            if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: SyncModSettingsReceived");
            foreach (var buildAndRepairSystem in BuildAndRepairSystems.Values)
            {
               buildAndRepairSystem.SettingsChanged();
            }
         }
         catch (Exception ex)
         {
            Mod.Log.Write(Logging.Level.Error, "BuildAndRepairSystemMod: SyncModSettingsReceived Exception:{0}", ex);
         }
      }

      /// <summary>
      /// 
      /// </summary>
      public static void SyncBlockDataRequestSend(NanobotBuildAndRepairSystemBlock block)
      {
         if (!MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Session.IsServer) return;

         var msgSnd = new MsgBlockDataRequest();
         if (MyAPIGateway.Session.Player != null)
            msgSnd.SteamId = MyAPIGateway.Session.Player.SteamUserId;
         else
            msgSnd.SteamId = 0;
         msgSnd.EntityId = block.Welder.EntityId;

         if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: SyncBlockDataRequestSend {0}/{1}", msgSnd.SteamId, Logging.BlockName(block.Welder, Logging.BlockNameOptions.None));
         MyAPIGateway.Multiplayer.SendMessageToServer(MSGID_BLOCK_DATAREQUEST, MyAPIGateway.Utilities.SerializeToBinary(msgSnd), true);
      }

      /// <summary>
      /// 
      /// </summary>
      private void SyncBlockDataRequestReceived(byte[] dataRcv)
      {
         var msgRcv = MyAPIGateway.Utilities.SerializeFromBinary<MsgBlockDataRequest>(dataRcv);

         NanobotBuildAndRepairSystemBlock system;
         if (BuildAndRepairSystems.TryGetValue(msgRcv.EntityId, out system))
         {
            if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: SyncBlockDataRequestReceived {0}/{1}", msgRcv.SteamId, Logging.BlockName(system.Welder, Logging.BlockNameOptions.None));
            SyncBlockSettingsSend(msgRcv.SteamId, system);
            SyncBlockStateSend(msgRcv.SteamId, system);
         }
         else
         {
            if (Mod.Log.ShouldLog(Logging.Level.Error)) Mod.Log.Write(Logging.Level.Error, "BuildAndRepairSystemMod: SyncBlockDataRequestReceived for unknown system {0}/{1}", msgRcv.SteamId, msgRcv.EntityId);
         }
      }

      /// <summary>
      /// 
      /// </summary>
      public static void SyncBlockSettingsSend(ulong steamId, NanobotBuildAndRepairSystemBlock block)
      {
         if (!MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Session.IsServer) return;

         var msgSnd = new MsgBlockSettings();
         msgSnd.EntityId = block.Welder.EntityId;
         msgSnd.Settings = block.Settings.GetTransmit();

         var res = false;
         if (MyAPIGateway.Session.IsServer)
         {
            if (steamId == 0)
            {
               if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: SyncBlockSettingsSend {0} to Others", Logging.BlockName(block.Welder, Logging.BlockNameOptions.None));
               res = MyAPIGateway.Multiplayer.SendMessageToOthers(MSGID_BLOCK_SETTINGS_FROM_SERVER, MyAPIGateway.Utilities.SerializeToBinary(msgSnd), true);
            }
            else
            {
               if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: SyncBlockSettingsSend {0} to {1}", Logging.BlockName(block.Welder, Logging.BlockNameOptions.None), steamId);
               res = MyAPIGateway.Multiplayer.SendMessageTo(MSGID_BLOCK_SETTINGS_FROM_SERVER, MyAPIGateway.Utilities.SerializeToBinary(msgSnd), steamId, true);
            }
         }
         else
         {
            if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: SyncBlockSettingsSend {0} to Server", Logging.BlockName(block.Welder, Logging.BlockNameOptions.None));
            res = MyAPIGateway.Multiplayer.SendMessageToServer(MSGID_BLOCK_SETTINGS_FROM_CLIENT, MyAPIGateway.Utilities.SerializeToBinary(msgSnd), true);
         }
         if (!res && Mod.Log.ShouldLog(Logging.Level.Error)) Mod.Log.Write(Logging.Level.Error, "BuildAndRepairSystemMod: SyncBlockSettingsSend failed", Logging.BlockName(block.Welder, Logging.BlockNameOptions.None));
      }

      /// <summary>
      /// 
      /// </summary>
      private void SyncBlockSettingsReceived(byte[] dataRcv)
      {
         try
         {
            var msgRcv = MyAPIGateway.Utilities.SerializeFromBinary<MsgBlockSettings>(dataRcv);

            NanobotBuildAndRepairSystemBlock system;
            if (BuildAndRepairSystems.TryGetValue(msgRcv.EntityId, out system))
            {
               if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: SyncBlockSettingsReceived {0}", Logging.BlockName(system.Welder, Logging.BlockNameOptions.None));
               system.Settings.AssignReceived(msgRcv.Settings, system.BuildPriority);
               if (MyAPIGateway.Session.IsServer)
               {
                  SyncBlockSettingsSend(0, system);
               }
            } else
            {
               if (Mod.Log.ShouldLog(Logging.Level.Error)) Mod.Log.Write(Logging.Level.Error, "BuildAndRepairSystemMod: SyncBlockSettingsReceived for unknown system {0}", msgRcv.EntityId);
            }
         }
         catch (Exception ex)
         {
            Mod.Log.Write(Logging.Level.Error, "BuildAndRepairSystemMod: SyncBlockSettingsReceived Exception:{0}", ex);
         }
      }

      /// <summary>
      /// 
      /// </summary>
      public static void SyncBlockStateSend(ulong steamId, NanobotBuildAndRepairSystemBlock system)
      {
         if (!MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Session.IsServer) return;

         var msgSnd = new MsgBlockState();
         msgSnd.EntityId = system.Welder.EntityId;
         msgSnd.State = system.State.GetTransmit();

         var res = false;
         if (steamId == 0)
         {
            if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: SyncBlockStateSend {0} to Others {1}/{2}", Logging.BlockName(system.Welder, Logging.BlockNameOptions.None), msgSnd.State.MissingComponentsSync.Count, msgSnd.State.PossibleWeldTargetsSync.Count);
            res = MyAPIGateway.Multiplayer.SendMessageToOthers(MSGID_BLOCK_STATE_FROM_SERVER, MyAPIGateway.Utilities.SerializeToBinary(msgSnd), true);
         }
         else
         {
            if (Mod.Log.ShouldLog(Logging.Level.Info)) Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: SyncBlockStateSend {0} to {1} {2}/{3}", Logging.BlockName(system.Welder, Logging.BlockNameOptions.None), steamId, msgSnd.State.MissingComponentsSync.Count, msgSnd.State.PossibleWeldTargetsSync.Count);
            res = MyAPIGateway.Multiplayer.SendMessageTo(MSGID_BLOCK_STATE_FROM_SERVER, MyAPIGateway.Utilities.SerializeToBinary(msgSnd), steamId, true);
         }


         if (!res && Mod.Log.ShouldLog(Logging.Level.Error))
         {
            Mod.Log.Write(Logging.Level.Error, "BuildAndRepairSystemMod: SyncBlockStateSend Failed");
         }
      }

      /// <summary>
      /// 
      /// </summary>
      private void SyncBlockStateReceived(byte[] dataRcv)
      {
         try
         {
            var msgRcv = MyAPIGateway.Utilities.SerializeFromBinary<MsgBlockState>(dataRcv);

            NanobotBuildAndRepairSystemBlock system;
            if (BuildAndRepairSystems.TryGetValue(msgRcv.EntityId, out system))
            {
               if (Mod.Log.ShouldLog(Logging.Level.Info))
               {
                  Mod.Log.Write(Logging.Level.Info, "BuildAndRepairSystemMod: SyncBlockStateReceived {0} {1}/{2}", Logging.BlockName(system.Welder, Logging.BlockNameOptions.None), msgRcv.State.MissingComponentsSync.Count, msgRcv.State.PossibleWeldTargetsSync.Count);
               }
               system.State.AssignReceived(msgRcv.State);
            }
            else
            {
               if (Mod.Log.ShouldLog(Logging.Level.Error)) Mod.Log.Write(Logging.Level.Error, "BuildAndRepairSystemMod: SyncBlockStateReceived for unknown system {0}", msgRcv.EntityId);
            }
         }
         catch (Exception ex)
         {
            Mod.Log.Write(Logging.Level.Error, "BuildAndRepairSystemMod: SyncBlockStateReceived Exception:{0}", ex);
         }
      }
   }
}
