using System;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
//using Sandbox.ModAPI.Ingame;
using VRageMath;
using VRage.Game.ModAPI;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.ModAPI;
using System.Collections.Generic;
using Sandbox.Game.Weapons;
using Sandbox.Game.Gui;

namespace TurretAssist
{
	[MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
	public class TurretAssist : MySessionComponentBase
	{
		private const ushort _RCV_FROMCLIENT = 10910;
		public static TurretAssist instance;
		private bool init = false;
		public bool running = true;
		public bool isServer = false;
		public bool isDedicated = false;
		//private string m_lastmessage = "";
		private long m_lasttarget = 0;
		Dictionary<IMyLargeTurretBase, MyEntity> TurretTrackers = new Dictionary<IMyLargeTurretBase, MyEntity>();
		private struct CloseDetect
		{
			public IMyEntity ent;
			public double distance;


			public CloseDetect(IMyEntity p, double v) : this()
			{
				ent = p;
				distance = v;
			}

			internal void isCloser(IMyEntity ent, float v)
			{
				if (v < distance)
				{
					distance = v;
					this.ent = ent;
				}	
			}
		}

		public override void UpdateAfterSimulation()
		{
			if (!init)
			{
				if (MyAPIGateway.Session == null)
					return;
				Init();
			}
			Dictionary<IMyLargeTurretBase, MyEntity> _TurretTrackers = new Dictionary<IMyLargeTurretBase, MyEntity>(TurretTrackers);
            foreach (var tracker in _TurretTrackers)
			{
				if(tracker.Value.MarkedForClose)
				{
					//reset turret
					TurretTrackers.Remove(tracker.Key);
					ResetTurret(tracker.Key);
					//remove
					continue;
				}
				if(tracker.Value is IMyFunctionalBlock)
				{
					var func = tracker.Value as IMyFunctionalBlock;
					if(!func.IsFunctional)
					{
						TurretTrackers.Remove(tracker.Key);
						ResetTurret(tracker.Key);
					}
				}
				/*if(tracker.Value != tracker.Key.Target)
				{
					ResetTurret(tracker.Key);
					TurretTrackers.Remove(tracker.Key);
					continue;
				}*/
				
				if(Vector3D.Distance(tracker.Key.GetPosition(), tracker.Value.PositionComp.WorldMatrix.Translation) > tracker.Key.Range)
				{
					ResetTurret(tracker.Key);
					TurretTrackers.Remove(tracker.Key);
					continue;
				}
			}
			if (!isDedicated)
			{
				if(MyAPIGateway.Session.ControlledObject != null )
				{
					if(MyAPIGateway.Session.ControlledObject.Entity != null)
					{
						//todo cache this!
						var controlledent = MyAPIGateway.Session.ControlledObject.Entity;
						if (controlledent is IMyLargeTurretBase)
						{
							var turret = controlledent as IMyLargeTurretBase;
							List<IMyLargeTurretBase> turretlist = new List<IMyLargeTurretBase>();
							turretlist.Add(turret);
							var termsystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid((IMyCubeGrid)turret.CubeGrid);
							List<IMyBlockGroup> blockGroups = new List<IMyBlockGroup>();
							termsystem.GetBlockGroups(blockGroups);
							foreach(var group in blockGroups)
							{
								List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
								group.GetBlocksOfType<IMyTerminalBlock>(blocks);
								if (!blocks.Contains(turret as IMyTerminalBlock)) continue;

									foreach(var groupblock in blocks)
									{
										var turretblock = groupblock as IMyLargeTurretBase;
										if(turretblock != null)
										{
											if(!turretlist.Contains(turretblock as IMyLargeTurretBase))
												turretlist.Add(turretblock as IMyLargeTurretBase);
										}
									}
									//break;
								
							}

							if (MyAPIGateway.Input.IsAnyCtrlKeyPressed())
							{
								var controllable = controlledent as Sandbox.Game.Entities.IMyControllableEntity;
								MatrixD realview = MatrixD.Invert(controllable.GetHeadMatrix(true));
								LineD vec = new LineD(realview.Translation, realview.Translation + Vector3D.Multiply(MyBlockBuilderBase.IntersectionDirection, 800));
								for (int i = 0; i < 5; i++)
								{
									Vector3D point = realview.Translation + Vector3D.Multiply(MyBlockBuilderBase.IntersectionDirection, 800) * i / 5f;
									double radius = 800 * 1 / 10f;
									BoundingSphereD bs = new BoundingSphereD(point, radius);
									var ents = MyAPIGateway.Entities.GetEntitiesInSphere(ref bs);
									CloseDetect closest = new CloseDetect(null, 800);

									foreach (var ent in ents)
									{
										if (!ent.WorldAABB.Intersects(ref vec))
											continue;
										if (ent is IMyCubeBlock)
										{
											var block = ent as IMyCubeBlock;
											if (block.CubeGrid.EntityId == turret.CubeGrid.EntityId)//ignore own grid
												continue;
											var ownership = block.GetPlayerRelationToOwner();
											if (ownership == MyRelationsBetweenPlayerAndBlock.Owner || ownership == MyRelationsBetweenPlayerAndBlock.Neutral)
												continue;
											closest.isCloser(ent, Vector3.Distance(realview.Translation, ent.GetPosition()));
										}
										if(ent is IMyCharacter)
										{
											
											closest.isCloser(ent, Vector3.Distance(realview.Translation, ent.GetPosition()));
										}
										if(ent is IMyMeteor)
										{
											closest.isCloser(ent, Vector3.Distance(realview.Translation, ent.GetPosition()));
										}
									}
									if(closest.ent != null)
									{
										foreach(var listturret in turretlist)
										{
											sendToServer(listturret, closest.ent);
										}
										string entityname = "";
										if (closest.ent is IMyCubeBlock)
										{
											entityname = (closest.ent as IMyCubeBlock).DefinitionDisplayNameText;
										}
										if (closest.ent is IMyMeteor)
										{
											entityname = "Meteor";
										}
										if (closest.ent is IMyCharacter)
										{
											entityname = closest.ent.DisplayName;
										}
										string newmessage = "Changing target to " + entityname;
										if(closest.ent.EntityId != m_lasttarget)
										{
											MyAPIGateway.Utilities.ShowNotification(newmessage, 1000, MyFontEnum.Red);
											m_lasttarget = closest.ent.EntityId;
										}
                                        
										break;//dont need to loop anymore, we have a target.
									}
								}
								
                            }
                        }
					}
				}
			}
		}



		private void Init()
		{
			init = true;
			instance = this;
			isServer = MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE || MyAPIGateway.Multiplayer.IsServer;
			isDedicated = (MyAPIGateway.Utilities.IsDedicated && isServer);
			if (isServer && MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE)
			{
				MyAPIGateway.Multiplayer.RegisterMessageHandler(_RCV_FROMCLIENT, recieveFromClient);
			}
			if (!isDedicated)
			{
				//MyAPIGateway.Multiplayer.RegisterMessageHandler(10902, recieveRedMessage);
				//MyAPIGateway.Multiplayer.RegisterMessageHandler(10903, recieveWhiteMessage);
			}

		}
		private void sendToServer(IMyEntity turret, IMyEntity target)
		{
			if (isServer)
			{
				SetTurretTarget(turret as IMyLargeTurretBase, (MyEntity)target);
				return;
			}


			var turretbyte = BitConverter.GetBytes(turret.EntityId);
			var targetbyte = BitConverter.GetBytes(target.EntityId);
			byte[] msg = new byte[sizeof(long) * 2];
			turretbyte.CopyTo(msg, sizeof(long) * 0);
			targetbyte.CopyTo(msg, sizeof(long) * 1);

			MyAPIGateway.Multiplayer.SendMessageToServer(_RCV_FROMCLIENT, msg, true);
		}
		private void recieveFromClient(byte[] msg)
		{
			try
			{
				if (msg.Length != sizeof(long) * 2)
					return;
				var turretid = BitConverter.ToInt64(msg, sizeof(long) * 0);
				var targetid = BitConverter.ToInt64(msg, sizeof(long) * 1);
				var turretentity = MyAPIGateway.Entities.GetEntityById(turretid) as MyEntity;
				var targetentity = MyAPIGateway.Entities.GetEntityById(targetid) as MyEntity;

				if (turretentity == null || targetentity == null)
				{
					return;
				}
				if (turretentity is IMyLargeTurretBase)
				{
					var turret = turretentity as IMyLargeTurretBase;
					SetTurretTarget(turret, targetentity);
				}
			}
			catch
			{

			}

		}

		private void SetTurretTarget(IMyLargeTurretBase turret, MyEntity targetentity)
		{
			if (turret == null)
				return;
			turret.TrackTarget(targetentity);//was SetTarget
			MyEntity outtracker;
			if(TurretTrackers.TryGetValue(turret, out outtracker))
			{
				TurretTrackers.Remove(turret);
			}
			TurretTrackers.Add(turret, targetentity);
		}
		private void ResetTurret(IMyLargeTurretBase turret)
		{

            if (!turret.MarkedForClose && turret.IsFunctional)
			{
				
				turret.ApplyAction("ShootOnce");
			}
		}

		protected override void UnloadData()
		{
			TurretTrackers.Clear();
			if (isServer && MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE)
			{
				MyAPIGateway.Multiplayer.UnregisterMessageHandler(_RCV_FROMCLIENT, recieveFromClient);
			}
		}
	}


}
