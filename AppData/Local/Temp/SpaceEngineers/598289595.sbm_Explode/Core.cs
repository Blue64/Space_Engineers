using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRageMath;
using VRage.Game.Entity;
using Sandbox.Definitions;
//using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;

namespace GasExplode
{
	[MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
	public class Core : MySessionComponentBase
	{
		public static Core instance;
		bool init = false;
		bool isServer = false;
		public readonly DebugLevel debug = DebugLevel.None;
		MyEntity3DSoundEmitter emitter;
		
		public override void UpdateAfterSimulation()
		{
			if (!init)
			{
				if (MyAPIGateway.Session == null)
					return;
				//if (MyAPIGateway.Multiplayer == null && MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE)
				//	return;
				Init();
			}
		}

		private void Init()
		{
			instance = this;
			init = true;
			isServer = MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE || MyAPIGateway.Multiplayer.IsServer;
			MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(100, damagemult);
			MyAPIGateway.Session.DamageSystem.RegisterAfterDamageHandler(100, handler);
        }
		protected override void UnloadData()
		{
			Log.Close();
        }
		private void damagemult(object target, ref MyDamageInformation info)
		{
			if (target is IMySlimBlock)
			{
				var entity = target as IMySlimBlock;
				{
					if (entity.FatBlock is IMyOxygenTank)
					{
						if (info.Type == MyDamageType.Fire
							|| info.Type == MyDamageType.Explosion
							|| info.Type == MyDamageType.Bullet
							|| info.Type == MyDamageType.Mine
							|| info.Type == MyDamageType.Rocket
							|| info.Type == MyDamageType.Bolt
							)
						{
							info.Amount *= 25;
						}
					}
				}
			}
		}

		private void handler(object target, MyDamageInformation info)
		{
			if (target is IMySlimBlock)
			{
				var entity = target as IMySlimBlock;
				{
					if (entity.FatBlock is IMyOxygenTank)
					{
						if (info.Type == MyDamageType.Fire
							|| info.Type == MyDamageType.Explosion
							|| info.Type == MyDamageType.Bullet
							|| info.Type == MyDamageType.Mine
							|| info.Type == MyDamageType.Rocket
							|| info.Type == MyDamageType.Bolt
							)
						{
							var explode = ((entity.CurrentDamage + info.Amount)) > entity.MaxIntegrity * 0.1;
							//MyAPIGateway.Utilities.ShowNotification(string.Format("explode: {0}", explode));
							var block = entity.FatBlock as IMyOxygenTank;
							if (block.MarkedForClose || block.Closed) return;
							var gas = block.GetOxygenLevel();
							MyCubeBlockDefinition bdef;

							MyDefinitionManager.Static.TryGetCubeBlockDefinition((MyDefinitionId)block.BlockDefinition, out bdef);
							MyGasTankDefinition odef = bdef as MyGasTankDefinition;
							gas *= (odef.Capacity / ( 50 * odef.Size.Size * (odef.CubeSize == MyCubeSize.Small ? 0.5f : 2.5f) ) );
							if (block.IsFunctional && explode)
							{
								//MyAPIGateway.Utilities.ShowNotification("BOOM " + gas.ToString());
								if (gas < 0.2f) return;
								var damageblock = (IMyDestroyableObject)entity;
								BoundingSphereD explosion = new BoundingSphereD(block.WorldAABB.Center, Math.Pow(gas, 1d / 3d));
								//Log.DebugWrite(DebugLevel.Info, string.Format("Radius: {0}", explosion.Radius));
								List<MyEntity> entities = MyEntities.GetEntitiesInSphere(ref explosion);
								//PLAY SOUND
								emitter = new MyEntity3DSoundEmitter(null);
								emitter.SetPosition(explosion.Center);
								emitter.SetVelocity(Vector3.Zero);
								MySoundPair m_bombExpl = new MySoundPair("ArcWepLrgWarheadExpl");
								emitter.CustomMaxDistance = (float)Math.Pow(explosion.Radius, 2);
								emitter.CustomVolume = (float)explosion.Radius / 5;
								emitter.PlaySingleSound(m_bombExpl, true);

								try
								{
									foreach (var loopentity in entities)
									{
										//Log.DebugWrite(DebugLevel.Info, "loop");
										//Log.DebugWrite(DebugLevel.Info, loopentity);
										IMyDestroyableObject destroyableObj = null;
										if (loopentity.EntityId == entity.FatBlock.EntityId) continue;
										float damage = gas;
										if (loopentity is IMyCharacter)
										{
											damage /= 10;
										}
										else
											damage *= 50;
										if (loopentity is MyCubeBlock)
										{
											continue;
										}
										if (loopentity is MyCubeGrid)
										{
											var grid = (IMyCubeGrid)loopentity;
											List<IMySlimBlock> blocks = grid.GetBlocksInsideSphere(ref explosion);

											try
											{
												foreach (var bck in blocks)
												{
													if (bck.FatBlock != null)
														if (bck.FatBlock.EntityId == entity.FatBlock.EntityId)
															continue;
													
													destroyableObj = bck as IMyDestroyableObject;
													var cdist = (float)Vector3D.Distance(explosion.Center, grid.GridIntegerToWorld(bck.Position)) - grid.GridSize;
													if (cdist < 1) cdist = 1;
													Vector3D dir = Vector3D.Normalize(grid.GridIntegerToWorld(bck.Position) - explosion.Center);
													if (isServer)
														grid.Physics.ApplyImpulse(Vector3D.Multiply(dir, (damage / cdist) * 10), grid.GridIntegerToWorld(bck.Position));
													if (isServer)
														destroyableObj.DoDamage(damage / cdist, MyDamageType.Explosion, true);
												}
												continue;
											}
											catch
											{
												continue;
											}
										}
										if ((loopentity is IMyDestroyableObject))
										{
											destroyableObj = loopentity as IMyDestroyableObject;
										}
										if (destroyableObj == null)
											continue;
										//Log.DebugWrite(DebugLevel.Info, " destroyable");
										var dist = (float)Vector3D.Distance(loopentity.WorldMatrix.Translation, explosion.Center) - 1;
										if (dist < 1) dist = 1;
										////Log.DebugWrite(DebugLevel.Info, damage);
										//Log.DebugWrite(DebugLevel.Info, dist);
										//Log.DebugWrite(DebugLevel.Info, damage / dist);
										if(isServer)
										destroyableObj.DoDamage(damage / dist, MyDamageType.Explosion, true);

									}
									if (isServer)
										damageblock.DoDamage(1000000, MyDamageType.Deformation, true);

								}
								catch
								{

								}
                            }
							
						}
					}
				}
			}
		}
	}
}
