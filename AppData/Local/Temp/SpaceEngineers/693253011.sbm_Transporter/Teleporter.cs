/*
Copyright � 2016 Leto
This work is free. You can redistribute it and/or modify it under the
terms of the Do What The Fuck You Want To Public License, Version 2,
as published by Sam Hocevar. See http://www.wtfpl.net/ for more details.
*/


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Collections;

using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage;
using VRage.ObjectBuilders;
using VRage.Game;
using VRage.ModAPI;
using VRage.Game.Components;
using VRageMath;
using Sandbox.Engine.Multiplayer;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;


namespace LSE.Teleporter
{
    public enum TeleportError
    {
        None,
        MissingPower,
        NoTarget,
        NoPlayer,
        TargetBlocked,
        GPSOutOfRange
    }

    public class TeleportInformation
    {
        public IMyEntity Ent;
        public TransportEndpoint Endpoint;

        public IMyEntity Transporter;

        public MyParticleEffect StartEffect;
        public MyParticleEffect EndEffect;

        public void AddSound()
        {
            var emitter = new Sandbox.Game.Entities.MyEntity3DSoundEmitter((VRage.Game.Entity.MyEntity)Ent);
            var pair = new Sandbox.Game.Entities.MySoundPair("Transporter");
            emitter.PlaySound(pair);
        }

        public MatrixD? GetTargetMatrix(bool recalc=false)
        {
            var matrix = new MatrixD(Ent.WorldMatrix);
            var translation = Endpoint.GetCurrentPosition(recalc);
            if (translation == null)
            {
                // no free space
                return null;
            }
            if (Endpoint.Ent != null)
            {
                matrix = Endpoint.Ent.WorldMatrix;
                //var forward = Endpoint.Entity.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward);
            }
            matrix.Translation = translation.Value;
            return matrix;
        }

        public void UpdateEffects(bool recalc=false)
        {
            if (StartEffect == null &&
                MyParticlesManager.TryCreateParticleEffect(53, out StartEffect, false))
            {
                AddSound();
            }

            if (EndEffect == null &&
                MyParticlesManager.TryCreateParticleEffect(53, out EndEffect, false))
            {
            }

            var rotMatrix = VRageMath.MatrixD.CreateRotationX(-Math.PI / 2);
            if (StartEffect != null)
            {
                var matrixEntity = new MatrixD(Ent.WorldMatrix);
                matrixEntity.Translation = matrixEntity.Translation - matrixEntity.Down * 2.5;
                matrixEntity = rotMatrix * matrixEntity;
                
                StartEffect.WorldMatrix = matrixEntity;
                StartEffect.UserScale = 0.07f;
                StartEffect.UserBirthMultiplier = 1.0f;
                StartEffect.UserAxisScale = new Vector3(1.1, 1.1, 1.1);
            }

            if (EndEffect != null)
            {
                var matrix = GetTargetMatrix(recalc);
                if (matrix != null)
                {
                    var matrixEntity = matrix.Value;
                    matrixEntity.Translation = matrixEntity.Translation - matrixEntity.Down * 2.5;
                    matrixEntity = rotMatrix * matrixEntity;
                    EndEffect.WorldMatrix = matrixEntity;
                    EndEffect.UserScale = 0.07f;
                    EndEffect.UserBirthMultiplier = 1.0f;
                    EndEffect.UserAxisScale = new Vector3(1.1, 1.1, 1.1);
                }
            }
        }

        public void StopEffects()
        {

        }

        public double Distance
        {
            get
            {
                if (Ent != null && Endpoint != null)
                {
                    var pos = Endpoint.GetCurrentPosition();
                    if (pos != null)
                    {
                        return (pos.Value - Ent.GetPosition()).Length();
                    }
                }
                return 0.0f;
            }
        }
    }


    public class TransportEndpoint
    {
        public IMyEntity Ent;
        public Vector3D Position = Vector3D.Zero;
        public bool RecalculateWhenBlocked = true;

        public Vector3D? GetCurrentPosition(bool recalculate=true)
        {
            var position = Position;
            if (Ent != null)
            {
                position += Ent.GetPosition();
            }

            if (recalculate && RecalculateWhenBlocked)
            {
                var realPos = MyAPIGateway.Entities.FindFreePlace(position, 0.9f, 21, 7, 0.2f);
                if (realPos == null)
                {
                    realPos = MyAPIGateway.Entities.FindFreePlace(position, 0.9f, 21, 7, 0.8f);
                    if (realPos == null)
                    {
                        realPos = MyAPIGateway.Entities.FindFreePlace(position, 0.9f, 21, 7, 3.2f);
                    }
                }

                if (realPos == null)
                {
                    return null;
                }
                else
                {
                    if (Ent != null)
                    {
                        Position = realPos.Value - Ent.GetPosition();
                        position = realPos.Value;
                    }
                }
            }
            return position;
        }
    }


    public class Teleporter : GameLogicComponent
    {
        public static HashSet<Teleporter> TeleporterList = new HashSet<Teleporter>();

        public double GPSTargetRange
		{
			get
			{
				return GetConfig().GPSTargetRange; 
			}	
		}

		public double MaximumRange
		{
			get
			{
				return GetConfig().MaximumRange;
			}	
		}

		public float PowerPerKilometer
		{
			get
			{
				return GetConfig().PowerPerKilometer;
			}	
		}

		public double MAX_PLANET_SIZE
		{
			get
			{
				return GetConfig().PlanetRange;
			}
		}

		public List<int> ValidTypes
		{
			get
			{
				return GetConfig ().ValidTypes;
			}
		}

        public int ProfilesAmount
        {
            get
            {
                return 4;
            }
        }

        public bool BeamEnemies
        {
            get
            {
                return GetConfig().BeamEnemy;
            }
        }


        public static bool DEBUG = false;

        public string Subtype = "Transporter";
        public double TeleporterDiameter = 2.5;

        public Vector3D TeleporterCenter = new Vector3(0.0f, 0.0f, -0.9f);
		public List<Vector3> TeleporterPads = new List<Vector3>() { };

        public int TELEPORT_TIME = 9500; // effect timespan is 10 seconds... but time in game runs faster (?!)

        public bool FirstStart = true;
        public static bool FirstStartStatic = true;
        public double Distance = 1;

        public StringBuilder ErrorMessage = new StringBuilder();

        public MyDefinitionId PowerDefinitionId = new VRage.Game.MyDefinitionId(typeof(VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GasProperties), "Electricity");
        public Sandbox.Game.EntityComponents.MyResourceSinkComponent Sink;
        
        public Dictionary<int, TransporterNetwork.MessageProfile> Profiles = new Dictionary<int, TransporterNetwork.MessageProfile>();

        public List<TeleportInformation> TeleportingInfos = new List<TeleportInformation>();
        public DateTime? TeleportStart = null;

        public LSE.Control.ButtonControl<Sandbox.ModAPI.Ingame.IMyOreDetector> Button;
        public LSE.Control.ControlAction<Sandbox.ModAPI.Ingame.IMyOreDetector> ActionBeam;

        public ProfileListbox<Sandbox.ModAPI.Ingame.IMyOreDetector> ProfileListbox;
        public Control.SwitchControl<Sandbox.ModAPI.Ingame.IMyOreDetector> SwitchControl;
        public TargetCombobox<Sandbox.ModAPI.Ingame.IMyOreDetector> TargetsListbox;
        public ProfileTextbox<Sandbox.ModAPI.Ingame.IMyOreDetector> ProfileTextbox;

        public RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> FilterOutrange;
        public RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> FilterGPS;
        public RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> FilterPlayers;
        public RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> FilterTransporter;
        public RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector> FilterPlanets;
        
        public static Dictionary<string, TransporterNetwork.MessageConfig> s_Configs = new Dictionary<string, TransporterNetwork.MessageConfig>();
        public static void SetConfig(string subtype, TransporterNetwork.MessageConfig config)
		{
			s_Configs[subtype] = config;
		}

		public LSE.TransporterNetwork.MessageConfig GetConfig()
		{
			if (s_Configs.ContainsKey(Subtype))
			{
				return s_Configs [Subtype];
			}
			return new LSE.TransporterNetwork.MessageConfig ();
		}

        public IMyCubeBlock CubeBlock;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            CubeBlock = (IMyCubeBlock)Entity;
            if (CubeBlock.BlockDefinition.SubtypeName != Subtype) { return; }

            CubeBlock.Components.TryGet<Sandbox.Game.EntityComponents.MyResourceSinkComponent>(out Sink);
            Sink.SetRequiredInputFuncByType(PowerDefinitionId, CalcRequiredPower);
            CubeBlock.IsWorkingChanged += IsWorkingChanged;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;

			((IMyFunctionalBlock)Entity).AppendingCustomInfo += AppendingCustomInfo;
        }

        float CalcRequiredPower()
        {
			return Math.Max((float)Distance / 1000 * PowerPerKilometer, 0.001f);
        }

        bool IsEnoughPower(double distance)
        {
			return Sink.IsPowerAvailable(PowerDefinitionId, (float)distance / 1000 * PowerPerKilometer);
        }
        
        void IsWorkingChanged(IMyCubeBlock block)
        {
            if (!FirstStart)
            {
                try
                {

                    var teleporter = block.GameLogic.GetAs<Teleporter>();
                    if (teleporter == null) { return ; }
                    if (block.IsWorking)
                    {
                        Teleporter.TeleporterList.Add(teleporter);
                    }
                    else
                    {
                        Teleporter.TeleporterList.Remove(teleporter);
                        TeleportingInfos.Clear();
                        TeleportStart = null;
                        Distance = 0;
                    }
                    teleporter.DrawEmissive();
                }
                catch (Exception error)
                {
                    LSELogger.Instance.LogMessage("ERROR (1): " + error.Message);
                }
            }
        }

        public override void UpdateAfterSimulation()
        {

            if (CubeBlock.BlockDefinition.SubtypeName != Subtype) { return; }
            if (MyAPIGateway.Multiplayer == null) { return; }


            if (FirstStartStatic)
            {
                //MyAPIGateway.Utilities.SendMessage("[Transporter] The actions are now working without crashing the game. So you can bind an transport profile to a toolbar, button or timer block. I am sorry that it took so long. This message will ");
                LSELogger.Instance.LogMessage(Subtype.ToString() + " First Static Start.");
                FirstStartStatic = false;
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    LSELogger.Instance.LogMessage("It's a Server. Loading Config.");

                    foreach (var subtype in new List<string>(s_Configs.Keys))
                    {
                        //byte[] byteData = System.Text.Encoding.UTF8.GetBytes(xml);
                        if (!MyAPIGateway.Utilities.FileExistsInGlobalStorage(subtype + ".xml"))
                        {
                            var message = new LSE.TransporterNetwork.MessageConfig();
                            var xml = MyAPIGateway.Utilities.SerializeToXML<TransporterNetwork.MessageConfig>(s_Configs[subtype]);
                            var writer = MyAPIGateway.Utilities.WriteFileInGlobalStorage(subtype + ".xml");
                            writer.Write(xml);
                            writer.Close();
                        }
                        var reader = MyAPIGateway.Utilities.ReadFileInGlobalStorage(subtype + ".xml");
                        var text = reader.ReadToEnd();
                        var messageNew = MyAPIGateway.Utilities.SerializeFromXML<TransporterNetwork.MessageConfig>(text);
                        SetConfig(subtype, messageNew);
                    }
                }
                else
                {
                    LSELogger.Instance.LogMessage("It's not a Server. Getting Config.");
                    LSE.TransporterNetwork.MessageUtils.SendMessageToServer(new TransporterNetwork.MessageClientConnected());
                }
            }

            if (FirstStart)
            {
                LSELogger.Instance.LogMessage("First Start.");

                CreateUI();
                //UpdateVisual();
                LoadProfiles();

                FirstStart = false;
                IsWorkingChanged(CubeBlock);
            }

            if (TeleportStart == null)
            {
                return;
            }
            LSELogger.Instance.LogMessage("Something to transport.");

            var transportTime = TeleportStart + new TimeSpan(0, 0, 0, 0, TELEPORT_TIME);

            if (!CubeBlock.IsWorking)
            {
                LSELogger.Instance.LogMessage("Transporter has stopped working.");

                foreach (var info in TeleportingInfos)
                {
                    info.StopEffects();
                }
                TeleportingInfos.Clear();
                
                return;
            }

            LSELogger.Instance.LogMessage("Effects are about to get updated.");
            foreach (var info in TeleportingInfos)
            {
                info.UpdateEffects();
            }

            if (MyAPIGateway.Session.GameDateTime > transportTime)
            {
                LSELogger.Instance.LogMessage("Transport ready.");

                double distance = 0.0f;
                foreach (var info in TeleportingInfos)
                {

                    var matrix = info.GetTargetMatrix(true);

                    if (matrix != null && IsPositionInRange(matrix.Value.Translation))
                    {
                        if (IsEnoughPower(distance + info.Distance))
                        {
                            if (!BeamEnemies)
                            {
                                var friendly = true;
                                var friendlyEndpoint = true;
                                var player = MyAPIGateway.Players.GetPlayerControllingEntity(info.Ent);
                                var playerEndpoint = MyAPIGateway.Players.GetPlayerControllingEntity(info.Endpoint.Ent);
                                if (player != null)
                                {
                                    friendly = player.GetRelationTo(CubeBlock.OwnerId).IsFriendly();
                                }
                                if (playerEndpoint != null)
                                {
                                    friendlyEndpoint = playerEndpoint.GetRelationTo(CubeBlock.OwnerId).IsFriendly();
                                }

                                if (!friendly || !friendlyEndpoint)
                                {
                                    StartFailedTransportSound(info.Ent);
                                    continue;
                                }

                            }

                            var endpointProtected = Jammer.IsProtected(matrix.Value.Translation, CubeBlock);
                            var entityProtected = Jammer.IsProtected(info.Ent.GetPosition(), CubeBlock);
                            if (endpointProtected || entityProtected)
                            {
                                StartFailedTransportSound(info.Ent);
                                if (!MyAPIGateway.Multiplayer.IsServer &&
                                    MyAPIGateway.Session.LocalHumanPlayer.Controller.ControlledEntity.Entity == info.Ent)
                                {
                                    MyAPIGateway.Utilities.ShowNotification("Jammer prevented transport.");
                                }
                                continue;
                            }

                            distance += info.Distance;
                            var matrixEntity = matrix.Value;
                            //info.Entity.LocalAABB.Center
                            //matrixEntity.Translation = matrixEntity.Translation + matrixEntity.Down * 0.9;
                            info.Ent.SetWorldMatrix(matrixEntity);
                            
                            LSELogger.Instance.LogMessage("Entity beamed.");
                        }
                    }
                    info.StopEffects();
                    LSELogger.Instance.LogMessage("Effect stopped.");
                }

                if (TeleportingInfos.Count() == 0)
                {
                    LSELogger.Instance.LogMessage("Transport failed.");
                    StartFailedTransportSound(Entity);
                }

                LSELogger.Instance.LogMessage("Clearing Teleport Infos.");
                TeleportingInfos.Clear();
                TeleportStart = null;
                Distance = 0;
                Sink.Update();
            }

                /*
                var maxLength = 0.003;
                var antiVelocity = (info.StartEffect.Value - entity.GetPosition());
                
                if (antiVelocity.Length() > maxLength)
                {
                    antiVelocity.Normalize();
                    entity.SetPosition(info.Start.Value - antiVelocity * maxLength);
                    info.Start = entity.GetPosition();
                }
                //entity.Physics.Clear();
                    
                if (diff > 1)
                {
                    TeleportFromTo(info.Player, info.Target.Value);
                }
                 * */

            //}
        }


        public override void UpdateBeforeSimulation100()
        {
            base.UpdateBeforeSimulation100();
            
            if (CubeBlock.BlockDefinition.SubtypeName != Subtype) { return; }


            DrawEmissive();

            if (CubeBlock.IsWorking)
            {
                TeleporterList.Add(this);
            }
            else
            {
                TeleporterList.Remove(this);
            }
        }

        public void DrawEmissive()
        {
            if (!CubeBlock.IsWorking)
            {
                CubeBlock.SetEmissiveParts("Emissive", new Color(255, 0, 0), 0.0f);
            }
            else if (TeleportStart != null && TeleportingInfos.Count == 0)
            {
                CubeBlock.SetEmissiveParts("Emissive", new Color(32, 16, 0), 0.0f);
            }
            else
            {
                CubeBlock.SetEmissiveParts("Emissive", new Color(0, 128, 0), 1.0f);
            }
        }

        /*
                if (teleportInfo.Error == TeleportError.None)
                {
                    CubeBlock.SetEmissiveParts("Emissive", new Color(0, 128, 0), 1.0f);
                    continue;
                }   
                else if (teleportInfo.Error == TeleportError.MissingPower)
                {
                    CubeBlock.SetEmissiveParts("Emissive", new Color(255, 0, 0), 0.0f);
                    //details.Append("WARNING: Not enough power. \r\n");
                    break;
                }
                else if (teleportNr == 0)
                {
                    CubeBlock.SetEmissiveParts("Emissive", new Color(32, 16, 0), 0.0f);
                    /*
                    if (teleportInfo.Error == TeleportError.NoPlayer)
                    {
                    //    details.Append("WARNING: No life signs at the given GPS/no charactere selected. \r\n");
                    }
                    if (teleportInfo.Error == TeleportError.NoTarget)
                    {
                        details.Append("WARNING: No Target selected. \r\n");
                    }
                    if (teleportInfo.Error == TeleportError.TargetBlocked)
                    {
                        details.Append("WARNING: Selected Target may be blocked. \r\n");
                    }
                    if (teleportInfo.Error == TeleportError.GPSOutOfRange)
                    {
                        details.Append("WARNING: Target out of Range. \r\n");
                    }*/
            //details.Append("Teleportation Power Consumption: " + (Distance / 1000).ToString() + "MW");
            //details.Append(lifeSigns.ToString() + " Life signs to teleport.");

        public void BeamUpAll(int profileNr)
        {
            var infos = BeamUpPossible(profileNr);

            Distance = CalcNeededPower(infos);
            TeleportingInfos = infos;
            TeleportStart = MyAPIGateway.Session.GameDateTime;

            foreach (var info in infos)
            {
                info.UpdateEffects(true);
            }

            Sink.Update();
        }


        public override void Close()
        {
            try
            {
                Teleporter.TeleporterList.Remove(this);
            }
            catch
            {
            }
            base.Close();
        }

        public override void MarkForClose()
        {
            try
            {
                Teleporter.TeleporterList.Remove(this);
            }
            catch
            {
            }
            base.MarkForClose();
        }

        public void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            /*
            stringBuilder.Clear();
            stringBuilder.Append("Saved Profile \r\n");
            stringBuilder.Append("Teleport: \r\n");

            foreach (var player in GetTeleportingPlayers(GetSavedType()))
            {
                if (player != null)
                {
                    stringBuilder.Append(player.DisplayName + " \r\n");
                }
            }

            var target = "No Target";
            switch (GetSavedType())
            {
                case 0:
                    target = GetSavedGPSName();
                    break;
                case 1:
                    var playersTo = GetSavedPlayers();
                    if (playersTo.Count > 0)
                    {
                        target = playersTo[0].DisplayName;
                    }
                    break;
                case 2:
                case 3:
                case 6:
                    target = "Teleporter";
                    break;
                case 5:
                    target = "Foreign Teleporter";
                    break;
                case 4:
                    var targetPos = GetPlanetPosInRange();
                    if (targetPos == null)
                    {
                        target = "No Planet in range";
                    }
                    else
                    {
                        target = "Planet in range";
                    }
                    break;
                default:
                    break;
            }
            
            stringBuilder.Append("To: " + target + " \r\n");

            */
        }

        public List<IMyEntity> GetTeleportingEntities(TransporterNetwork.MessageProfile profile)
        {
            
            var entities = new List<IMyEntity>();
            if (!profile.From)
            {
                var players = GetPlayersByPosition(GetTeleporterCenter(), TeleporterDiameter * TeleporterDiameter);
                entities = players.ConvertAll<IMyEntity>((x) => x.Controller.ControlledEntity.Entity);
            }
            else
            {
                foreach (var target in profile.Targets)
                {
                    try
                    {
                        switch (target.Type)
                        {
                            case 0:
                                if (target.TargetPos != null)
                                {
                                    var players = GetPlayersByPosition(target.TargetPos.Value, GPSTargetRange * GPSTargetRange);
                                    entities.AddList(players.ConvertAll<IMyEntity>((x) => x.Controller.ControlledEntity.Entity));
                                }
                                break;
                            case (1):
                                var entity = target.GetPlayerEntity();
                                if (entity != null)
                                {
                                    entities.Add(entity);
                                }
                                break;
                            case (2):
                                var foreignTransporter = target.GetTarget();
                                var teleporter = foreignTransporter.GameLogic.GetAs<Teleporter>();
                                if (teleporter == null) { return new List<IMyEntity>(); }

                                var players2 = teleporter.GetPlayersByPosition(
                                    foreignTransporter.GetPosition(),
                                    teleporter.TeleporterDiameter * teleporter.TeleporterDiameter);
                                entities.AddList(players2.ConvertAll<IMyEntity>((x) => x.Controller.ControlledEntity.Entity));
                                break;
                            case (3):
                                // shouldn't exist
                                break;
                        }
                    }
                    catch (Exception error)
                    {
                        if (Teleporter.DEBUG)
                        {
                            MyAPIGateway.Utilities.ShowNotification("ERROR (3): " + error.Message);
                        }
                    }
                }
            }
            return entities;
        }

        public List<TransportEndpoint> GetEndpoints(TransporterNetwork.MessageProfile profile)
        {
            
            var targets = new List<TransportEndpoint>();
            if (profile.From)
            {
                for (int teleportNr = 0; teleportNr < TeleporterPads.Count; ++teleportNr)
                {
                    var relativePos = GetRelativePosition(CubeBlock.GetPosition(), teleportNr);
                    targets.Add(new TransportEndpoint() { Position = relativePos, Ent = Entity, RecalculateWhenBlocked = false });
                }
            }
            else
            {
                foreach (var target in profile.Targets)
                {
                    switch (target.Type)
                    {
                        case 0:
                            if (target.TargetPos != null)
                            {
                                for (int teleportNr = 0; teleportNr < TeleporterPads.Count; ++teleportNr)
                                {
                                    var relativePos = GetRelativePosition(CubeBlock.GetPosition(), teleportNr);
                                    targets.Add(new TransportEndpoint() { Position = target.TargetPos.Value + relativePos });
                                }
                            }
                            break;
                        case 1:
                            var entity = target.GetPlayerEntity();
                            if (entity != null)
                            {
                                for (int teleportNr = 0; teleportNr < TeleporterPads.Count; ++teleportNr)
                                {
                                    var relativePos = GetRelativePosition(CubeBlock.GetPosition(), teleportNr);
                                    targets.Add(new TransportEndpoint() { Ent = entity, Position = relativePos });
                                }
                            }
                            break;
                        case 2:
                            var entity2 = target.GetTarget();
                            if (entity2 != null)
                            {
                                var teleporter = entity2.GameLogic.GetAs<Teleporter>();
                                if (teleporter == null) { return new List<TransportEndpoint>(); }

                                for (int teleportNr = 0; teleportNr < teleporter.TeleporterPads.Count; ++teleportNr)
                                {
                                    var relativePos = teleporter.GetRelativePosition(entity2.GetPosition(), teleportNr);
                                    targets.Add(new TransportEndpoint() { Ent = entity2, Position = relativePos, RecalculateWhenBlocked=false });
                                }
                            }
                            break;
                        case 3:
                            var entity3 = target.GetTarget();
                            if (entity3 != null)
                            {
                                var position = CubeBlock.GetPosition();
                                for (int teleportNr = 0; teleportNr < TeleporterPads.Count; ++teleportNr)
                                {
                                    var planet = (Sandbox.Game.Entities.MyPlanet)entity3;
                                    var surfacePosition = planet.GetClosestSurfacePointGlobal(ref position);
                                    targets.Add(new TransportEndpoint() { Position = surfacePosition });
                                }
                            }
                            break;
                    }
                }
            }
            return targets;
        }

        public double CalcNeededPower(List<TeleportInformation> infos)
        {
            double distance = 0;
            foreach (var info in infos)
            {
                distance += info.Distance;
            }
            return distance;
        }

        public List<TeleportInformation> BeamUpPossible(int profileNr)
        {
            var profile = GetProfile(profileNr);
            var teleportInfos = new List<TeleportInformation>();

            var teleportEntities = GetTeleportingEntities(profile);
            teleportEntities.RemoveAll((x) => !IsPositionInRange(x.GetPosition()));
            var endpoints = GetEndpoints(profile);
            endpoints.RemoveAll((x) => !IsPositionInRange(x.GetCurrentPosition()));
            var maxTransports = Math.Min(Math.Min(teleportEntities.Count, endpoints.Count), TeleporterPads.Count);

            double distance = 0;
            for (var teleportNr = 0; teleportNr < maxTransports; ++teleportNr)
            {   
                var info = new TeleportInformation() { Ent = teleportEntities[teleportNr], Endpoint = endpoints[teleportNr], Transporter = Entity };
                if (IsEnoughPower(info.Distance + distance))
                {
                    distance += info.Distance;
                    teleportInfos.Add(info);
                }
            }
            return teleportInfos;
        }

        /*
        public Vector3D GetRelativePosition(Vector3 target, int teleportNr)
        {
            return target + (Entity.WorldMatrix.Forward + Entity.WorldMatrix.Right + Entity.WorldMatrix.Up) * TeleporterPads[teleportNr];
        }

        public Vector3D GetTeleporterCenter()
        {
            return Entity.GetPosition() + (Entity.WorldMatrix.Forward + Entity.WorldMatrix.Right + Entity.WorldMatrix.Up) * TeleporterCenter;
        }
        */

        public Vector3D GetRelativePosition(Vector3 target, int teleportNr)
        {
            return (CubeBlock.WorldMatrix.Forward * TeleporterPads[teleportNr].X + CubeBlock.WorldMatrix.Right * TeleporterPads[teleportNr].Z + CubeBlock.WorldMatrix.Up * TeleporterPads[teleportNr].Y);
        }

        public Vector3D GetTeleporterCenter()
        {
            return CubeBlock.GetPosition() + (CubeBlock.WorldMatrix.Forward * TeleporterCenter.X + CubeBlock.WorldMatrix.Right * TeleporterCenter.Z + CubeBlock.WorldMatrix.Up * TeleporterCenter.Y);
        }

        public MatrixD VectorToMatrix(Vector3D target)
        {
            var matrix = new MatrixD(CubeBlock.WorldMatrix);
            matrix.Translation = target;
            return matrix;
        }

        public bool IsPositionInRange(Vector3D? pos)
        {
            if (pos != null)
            {
                return (pos.Value - CubeBlock.GetPosition()).LengthSquared() < MaximumRange * MaximumRange;
            }
            return false;
        }

        public List<IMyPlayer> GetPlayersByPosition(Vector3D pos, double distanceSquared)
        {
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, (x) => (x.GetPosition() - pos).LengthSquared() < distanceSquared && IsPositionInRange(x.GetPosition()));
            if (!BeamEnemies)
            {
                players.RemoveAll((x) => !x.GetRelationTo(CubeBlock.OwnerId).IsFriendly());
            }
            return players;
        }

        void ShowBeamText(IMyTerminalBlock block, StringBuilder hotbarText)
        {
            hotbarText.Clear();
            hotbarText.Append("Activate Teleport");
        }


        public int GetSavedType()
        {
            int teleportationType;
            MyAPIGateway.Utilities.GetVariable<int>(CubeBlock.EntityId.ToString() + "-TeleportationType", out teleportationType);
            return teleportationType;
        }

        public string GetSavedGPSName()
        {
            string name;
            MyAPIGateway.Utilities.GetVariable<string>(CubeBlock.EntityId.ToString() + "-SavedGPS", out name);
            return name;
        }


        public void SendBeamMessage(int profileNr)
        {
            var message = new TransporterNetwork.MessageBeam()
            {
                EntityId = CubeBlock.EntityId,
                ProfileNr = profileNr
            };
            var possibilities = BeamUpPossible(profileNr);
            //BeamUpAll(message.ProfileNr);
            if (Teleporter.DEBUG)
            {
                MyAPIGateway.Utilities.ShowNotification("SendBeamMessage");
            }

            TransporterNetwork.MessageUtils.SendMessageToAll(message);
        }

        void RemoveOreUI()
        {
            
            List<IMyTerminalAction> actions = new List<IMyTerminalAction>();
            MyAPIGateway.TerminalControls.GetActions<Sandbox.ModAPI.Ingame.IMyOreDetector>(out actions);
            var actionAntenna = actions.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            actionAntenna.Enabled = ShowControlOreDetectorControls;

            List<IMyTerminalControl> controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<Sandbox.ModAPI.Ingame.IMyOreDetector>(out controls);
            var antennaControl = controls.First((x) => x.Id.ToString() == "BroadcastUsingAntennas");
            antennaControl.Visible = ShowControlOreDetectorControls;

            try
            {
                var radiusControl = controls.First((x) => x.Id.ToString() == "Range");
                radiusControl.Visible = ShowControlOreDetectorControls;
            }
            catch
            {
            }
        }

        
        void CreateUI()
        {

            RemoveOreUI();
            //new Control.Seperator<Sandbox.ModAPI.IMyOreDetector>((IMyTerminalBlock)CubeBlock, "TransporterSeperator1");
            //new Control.Seperator<Sandbox.ModAPI.IMyOreDetector>((IMyTerminalBlock)CubeBlock, "TransporterSeperator2");

            Button = new BeamButton<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)CubeBlock,
                "Beam",
                "Teleport");

            ProfileListbox = new ProfileListbox<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)CubeBlock,
                "Profiles",
                "Profiles:",
                4);

            ProfileTextbox = new ProfileTextbox<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)CubeBlock, "ProfileName", "Profile Name", "");

            SwitchControl = new FromSwitch<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)CubeBlock,
                "FromTo",
                "Transport from or to Target?",
                "From",
                "To");

            TargetsListbox = new TargetCombobox<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)CubeBlock,
                "Targets",
                "Targets (+saved targets):",
                8);

            FilterOutrange = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)CubeBlock,
                "FilterOutrange",
                "Hide far targets ",
                true);

            FilterGPS = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)CubeBlock,
                "FilterGPS",
                "Hide GPS           ",
                false);

            FilterPlayers = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)CubeBlock,
                "FilterPlayers",
                "Hide Players      ",
                false);

            FilterTransporter = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)CubeBlock,
                "FilterTransporter",
                "Hide Transporter",
                false);

            FilterPlanets = new RefreshCheckbox<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)CubeBlock,
                "FilterPlanets",
                "Hide Planets      ",
                false);

            for (var actionIndex = 0; actionIndex < ProfilesAmount; ++actionIndex)
            {
                ActionBeam = new ActivateProfileAction<Sandbox.ModAPI.Ingame.IMyOreDetector>((IMyTerminalBlock)CubeBlock,
                    "Beam" + actionIndex.ToString(),
                    "Teleport - Profile " + actionIndex.ToString(),
                    actionIndex,
                    @"Textures\GUI\Icons\Actions\SmallShipSwitchOff.dds");
            } 
        }

        public void StartFailedTransportSound(IMyEntity entity)
        {
            var emitter = new Sandbox.Game.Entities.MyEntity3DSoundEmitter((VRage.Game.Entity.MyEntity)CubeBlock);
            var pair = new Sandbox.Game.Entities.MySoundPair("FailedTransport");
            emitter.PlaySound(pair);            
        }

        public List<IMyGps> GetGPS(bool filterOutRange)
        {
            var gpsList = MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.LocalHumanPlayer.IdentityId);
            if (filterOutRange)
            {
                gpsList.RemoveAll((x) => !IsPositionInRange(x.Coords));
            }
            return gpsList;
        }

        public List<Teleporter> GetTransporter(bool filterOutRange)
        {
            var transporterInRange = new List<Teleporter>(Teleporter.TeleporterList);
            if (filterOutRange)
            {
                transporterInRange.RemoveAll((x) => !IsPositionInRange(x.CubeBlock.GetPosition()));
            }
            transporterInRange.Remove(this);
            return transporterInRange;
        }

        public List<IMyPlayer> GetPlayers(bool filterOutRange)
        {
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            if (filterOutRange)
            {
                players.RemoveAll((x) => !IsPositionInRange(x.GetPosition()));
            }

            if (!BeamEnemies)
            {
                players.RemoveAll((x) => !x.GetRelationTo(CubeBlock.OwnerId).IsFriendly());
            }
            return players;
        }

        public List<Sandbox.Game.Entities.MyPlanet> GetPlanets(bool filterOutRange)
        {
            var planets = new List<Sandbox.Game.Entities.MyPlanet>();
            var entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities);
            foreach (var entity in entities)
            {
                if (entity is Sandbox.Game.Entities.MyPlanet)
                {
                    var planet = entity as Sandbox.Game.Entities.MyPlanet;
                    var pos = entity.GetPosition();
                    if (!filterOutRange ||
                        IsPositionInRange(planet.GetClosestSurfacePointGlobal(ref pos)))
                    {
                        planets.Add(planet);
                    }
                }
            }
            return planets;
        }



        bool ShowControlOreDetectorControls(IMyTerminalBlock block)
        {
            return block.BlockDefinition.SubtypeName.Contains("OreDetector");
        }

        public void UpdateVisual()
        {

            var controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<Sandbox.ModAPI.Ingame.IMyOreDetector>(out controls);
            foreach (var control in controls)
            {
                control.UpdateVisual();
            }
            DrawEmissive();
            ((IMyFunctionalBlock)CubeBlock).RefreshCustomInfo();
        }

        public void LoadProfiles()
        {
            string xml = null;
            MyAPIGateway.Utilities.GetVariable<string>(CubeBlock.EntityId + "-Profiles", out xml);
            if (xml != null)
            {
                var profileList = MyAPIGateway.Utilities.SerializeFromXML<List<TransporterNetwork.MessageProfile>>(xml);
                foreach (var profile in profileList)
                {
                    Profiles[profile.Profile] = profile;
                }
            }
        }

        public void SaveProfileFromOutside(TransporterNetwork.MessageProfile message)
        {
            Profiles[message.Profile] = message;
            var profileList = new List<TransporterNetwork.MessageProfile>(Profiles.Values);
            var xml = MyAPIGateway.Utilities.SerializeToXML<List<TransporterNetwork.MessageProfile>>(profileList);
            MyAPIGateway.Utilities.SetVariable<string>(CubeBlock.EntityId + "-Profiles", xml);
            UpdateVisual();
        }

        public TransporterNetwork.MessageProfile GetProfile(int profileNr)
        {
            var profile = Profiles.GetValueOrDefault(profileNr, new TransporterNetwork.MessageProfile() {
                Profile = profileNr,
                ProfileName = "Profile " + profileNr.ToString(),
                TransporterId = CubeBlock.EntityId
            });
            Profiles[profileNr] = profile;
            return profile;
        }

        public void SendProfileMessage(int profileNr)
        {
            var profile = GetProfile(profileNr);
            SaveProfileFromOutside(profile);
            TransporterNetwork.MessageUtils.SendMessageToAll(profile);
        }
    }

    public class FromSwitch<T> : LSE.Control.SwitchControl<T>
    {
        public FromSwitch(
            IMyTerminalBlock block,
            string internalName,
            string title,
            string onButton,
            string offButton,
            bool defaultValue = true)
            : base(block, internalName, title, onButton, offButton, defaultValue)
        {
        }

        public override bool Getter(IMyTerminalBlock block)
        {
            if (block.GameLogic == null)
            {
                return DefaultValue;
            }
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return false; }

            var profileNr = teleporter.ProfileListbox.GetterObjects(block)[0];
            var profile = teleporter.GetProfile(profileNr);
            return profile.From;
        }

        public override void Setter(IMyTerminalBlock block, bool newState)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return; }

            var profileNr = teleporter.ProfileListbox.GetterObjects(block)[0];
            var profile = teleporter.GetProfile(profileNr);
            profile.From = newState;
            teleporter.SendProfileMessage(profileNr);
        }
    }

    public class ProfileListbox<T> : LSE.Control.ComboboxControl<T, int>
    {
        public ProfileListbox(
            IMyTerminalBlock block,
            string internalName,
            string title,
            int size = 5,
            List<MyTerminalControlListBoxItem> content = null)
            : base(block, internalName, title, size, false, content)
        {

        }


        public override void FillContent(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
        {
            if (block.GameLogic == null)
            {
                return;
            }
            FixValues(block);
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return; }

            var newItems = new List<MyTerminalControlListBoxItem>();
            items.Clear();

            var maxProfiles = teleporter.ProfilesAmount;
            for (var profileNr = 0; profileNr < maxProfiles; ++profileNr)
            {
                var profile = teleporter.GetProfile(profileNr);
                var item = new MyTerminalControlListBoxItem(VRage.Utils.MyStringId.GetOrCompute(profile.ProfileName),
                    VRage.Utils.MyStringId.GetOrCompute(profile.ProfileName),
                    profileNr);

                newItems.Add(item);
            }
            Values[InternalName][block.EntityId] = newItems;
            base.FillContent(block, items, selected);
        }

        public virtual List<int> GetterObjects(IMyTerminalBlock block)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return new List<int>(); }

            var objects = base.GetterObjects(block);
            if (objects.Count == 0)
            {
                objects.Add(0);
            }
            else if (objects[0] >= teleporter.ProfilesAmount)
            {
                objects.Clear();
                objects.Add(0);
            }
            return objects;
        }
        
        public override void Setter(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return;  }
            base.Setter(block, selected);

            teleporter.UpdateVisual();
        }
    }

    public class TargetCombobox<T> : LSE.Control.ComboboxControl<T, TransporterNetwork.MessageTarget>
    {
        public TargetCombobox(
            IMyTerminalBlock block,
            string internalName,
            string title,
            int size = 5,
            List<MyTerminalControlListBoxItem> content = null)
            : base(block, internalName, title, size, true, content)
        {
        }


        public double DistanceTo(IMyTerminalBlock block, TransporterNetwork.MessageTarget target)
        {
            var pos = target.GetApproximatelyPosition();
            if (pos != null)
            {
                return (block.GetPosition() - pos.Value).Length();
            }
            return 0.0f;
        }

        public MyTerminalControlListBoxItem TargetToItem(IMyTerminalBlock block, TransporterNetwork.MessageTarget target)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return new MyTerminalControlListBoxItem(VRage.Utils.MyStringId.GetOrCompute(""), VRage.Utils.MyStringId.GetOrCompute(""), null); }

            var from = teleporter.SwitchControl.Getter(block);

            var name = target.GetDisplayName(from);
            var distance = DistanceTo(block, target);
            string distanceString = "";

            var pos = target.GetApproximatelyPosition();
            if (distance > teleporter.MaximumRange || (pos != null && Jammer.IsProtected(pos.Value, (IMyCubeBlock)block)))
            {
                distanceString = "???km";
            }
            else
            {
                distanceString = (distance / 1000).ToString("0.000") + "km";
            }

            return new MyTerminalControlListBoxItem(VRage.Utils.MyStringId.GetOrCompute(name),
                VRage.Utils.MyStringId.GetOrCompute(distanceString),
                target);
        }

        public void AddTarget(IMyTerminalBlock block, TransporterNetwork.MessageTarget target)
        {
            var item = TargetToItem(block, target);
            Values[InternalName][block.EntityId].Add(item);
        }

        public void AddGps(IMyTerminalBlock block)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return; }

            if (teleporter.FilterGPS.Getter(block))
            {
                return;
            }
            var gpsList = teleporter.GetGPS(teleporter.FilterOutrange.Getter(block));
            foreach (var gps in gpsList)
            {
                var target = new TransporterNetwork.MessageTarget()
                {
                    Type = 0,
                    TargetName = gps.Name,
                    TargetPos = gps.Coords,
                };
                AddTarget(block, target);
            }
        }

        public void AddPlayers(IMyTerminalBlock block)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return; }

            if (teleporter.FilterPlayers.Getter(block))
            {
                return;
            }

            var players = teleporter.GetPlayers(teleporter.FilterOutrange.Getter(block));
            foreach (var player in players)
            {
                var target = new TransporterNetwork.MessageTarget()
                {
                    Type = 1,
                    TargetName = player.DisplayName,
                    PlayerId = player.IdentityId,
                };
                AddTarget(block, target);
            }
        }

        public void AddTransporters(IMyTerminalBlock block)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return; }

            if (teleporter.FilterTransporter.Getter(block))
            {
                return;
            }

            var transporters = teleporter.GetTransporter(teleporter.FilterOutrange.Getter(block));
            foreach (var otherTransporter in transporters)
            {
                var name = ((IMyFunctionalBlock)otherTransporter.CubeBlock).CustomName;
                var target = new TransporterNetwork.MessageTarget()
                {
                    Type = 2,
                    TargetName = name,
                    TargetId = otherTransporter.CubeBlock.EntityId,
                };
                AddTarget(block, target);
            }
        }

        public void AddPlanets(IMyTerminalBlock block)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return; }

            if (teleporter.FilterPlanets.Getter(block))
            {
                return;
            }
            foreach (var planet in teleporter.GetPlanets(teleporter.FilterOutrange.Getter(block)))
            {
                var target = new TransporterNetwork.MessageTarget()
                {
                    Type = 3,
                    TargetName = planet.Generator.Id.SubtypeId.ToString(),
                    TargetId = planet.EntityId,
                };
                AddTarget(block, target);                
            }
        }

        public override void FillContent(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
        {
            if (block.GameLogic == null)
            {
                return;
            }

            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return; }

            Values[InternalName][block.EntityId].Clear();
            items.Clear();

            var from = teleporter.SwitchControl.Getter(block);
            if (from)
            {
                if (teleporter.ValidTypes.Contains(2))
                {
                    AddGps(block);
                }
                if (teleporter.ValidTypes.Contains(3))
                {
                    AddPlayers(block);
                }
                if (teleporter.ValidTypes.Contains(6))
                {
                    AddTransporters(block);
                }
            }
            else
            {
                if (teleporter.ValidTypes.Contains(4))
                {
                    AddPlanets(block);
                }
                if (teleporter.ValidTypes.Contains(0))
                {
                    AddGps(block);
                }
                if (teleporter.ValidTypes.Contains(1))
                {
                    AddPlayers(block);
                }
                if (teleporter.ValidTypes.Contains(5))
                {
                    AddTransporters(block);
                }
            }

            var profileNr = teleporter.ProfileListbox.GetterObjects(block)[0];
            var profile = teleporter.GetProfile(profileNr);
            SetterObjects(block, profile.Targets);

            base.FillContent(block, items, selected);
        }

        void SetterObjects(IMyTerminalBlock block,
            List<TransporterNetwork.MessageTarget> targets)
        {
            var selected = new List<MyTerminalControlListBoxItem>();
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return; }

            foreach (var target in targets)
            {
                try
                {
                    var index = Values[InternalName][block.EntityId].FindIndex((x) => (target.Equals(x.UserData)));
                    if (index == -1)
                    {
                        var item = TargetToItem(block, target);
                        Values[InternalName][block.EntityId].Insert(0, item);
                        selected.Add(item);
                    }
                    else
                    {
                        selected.Add(Values[InternalName][block.EntityId][index]);
                    }
                }
                catch (Exception error)
                {
                    if (Teleporter.DEBUG)
                    {
                        MyAPIGateway.Utilities.ShowNotification("ERROR (2): " + error.Message);
                    }
                }
            }
            base.Setter(block, selected);
        }

        public override void Setter(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
        {
            var targets = new List<TransporterNetwork.MessageTarget>();
            foreach (var target in selected)
            {
                targets.Add((TransporterNetwork.MessageTarget)target.UserData);
            }

            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return; }

            var profileNr = teleporter.ProfileListbox.GetterObjects(block)[0];
            var profile = teleporter.GetProfile(profileNr);
            profile.Targets = targets;
            teleporter.SendProfileMessage(profileNr);

            base.Setter(block, selected);

            teleporter.UpdateVisual();
        }

    }

    public class ProfileTextbox<T> : Control.Textbox<T>
    {
        public ProfileTextbox(
            IMyTerminalBlock block,
            string internalName,
            string title,
            string defaultValue)
            : base(block, internalName, title, defaultValue)
        {
            CreateUI();
        }

        public override StringBuilder Getter(IMyTerminalBlock block)
        {
            if (block.GameLogic == null)
            {
                return new StringBuilder("");
            }

            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return new StringBuilder(""); }

            var profileNr = teleporter.ProfileListbox.GetterObjects(block)[0];
            var profile = teleporter.GetProfile(profileNr);
            return new StringBuilder(profile.ProfileName);
        }

        public override void Setter(IMyTerminalBlock block, StringBuilder builder)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return; }

            var profileNr = teleporter.ProfileListbox.GetterObjects(block)[0];
            var profile = teleporter.GetProfile(profileNr);
            profile.ProfileName = builder.ToString();
            teleporter.SendProfileMessage(profileNr);
        }
    }

    //            var profileNr = ProfileListbox.GetterObjects((IMyFunctionalBlock)Entity)[0];


    public class ActivateProfileAction<T> : Control.ControlAction<T>
    {
        public int ProfileNr;
        public ActivateProfileAction(
            IMyTerminalBlock block,
            string internalName,
            string name,
            int profileNr,
            string icon)
            : base(block, internalName, name, icon)
        {
            ProfileNr = profileNr;
        }

        public override bool Visible(IMyTerminalBlock block)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return false; }
            return base.Visible(block) && ProfileNr < teleporter.MaximumRange;
        }

        public override void OnAction(IMyTerminalBlock block)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return; }
            teleporter.SendBeamMessage(ProfileNr);
        }

        public override void Writer(IMyTerminalBlock block, StringBuilder builder)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return; }

            var profile = teleporter.GetProfile(ProfileNr);
            builder.Clear();
            builder.Append(profile.ProfileName);
        }

    }
    public class BeamButton<T> : LSE.Control.ButtonControl<T>
    {
        public BeamButton(
            IMyTerminalBlock block,
            string internalName,
            string title)
            : base(block, internalName, title)
        {
            CreateUI();
        }

        public override void OnAction(IMyTerminalBlock block)
        {
            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return; }
            var profileNr = teleporter.ProfileListbox.GetterObjects(block)[0];
            if (block.IsWorking)
            {
                teleporter.SendBeamMessage(profileNr);
            }
        }
    }

    public class RefreshCheckbox<T> : LSE.Control.Checkbox<T>
    {
        public RefreshCheckbox(IMyTerminalBlock block,
            string internalName,
            string title,
            bool defaultValue = true)
            :base(block, internalName, title, defaultValue)
        {
            CreateUI();
        }


        public override void Setter(IMyTerminalBlock block, bool newState)
        {
            base.Setter(block, newState);

            var teleporter = block.GameLogic.GetAs<Teleporter>();
            if (teleporter == null) { return; }
            teleporter.UpdateVisual();
        }
    }
}