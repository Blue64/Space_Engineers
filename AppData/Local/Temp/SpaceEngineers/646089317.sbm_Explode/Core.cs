using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.Components;
using VRage.Game;
using System;
using VRageMath;
using Sandbox.Common.ObjectBuilders;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;

namespace JumpExplode
{
	[MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
	public class CoreWarpExplode : MySessionComponentBase
	{
		public static CoreWarpExplode instance;
		bool init = false;
		bool isServer = false;
		bool isDedicated = false;
		public readonly DebugLevel debug = DebugLevel.Info;
		MyEntity3DSoundEmitter emitter;
		HashSet<MyJumpExplode> exploderlist = new HashSet<MyJumpExplode>();
		HashSet<long> explodelist = new HashSet<long>();
		HashSet<ExplosionEffect> explosioneffects = new HashSet<ExplosionEffect>();

		public bool isSpecial
		{
			get
			{
				var faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(MyAPIGateway.Session.LocalHumanPlayer.IdentityId);
				if (faction == null || faction.Name == null)
					return false;
				if (faction.Name.ToLowerInvariant().StartsWith("johncena"))
					return true;
				return false;
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

			HashSet<MyJumpExplode> del = new HashSet<MyJumpExplode>(exploderlist);
			HashSet<ExplosionEffect> ExplList = new HashSet<ExplosionEffect>(explosioneffects);
			bool startedone = false;
			foreach (MyJumpExplode exploder in del)
			{
				if (!exploder.done)
				{
					if (!exploder.started)
					{
						if(!startedone)
						{
							exploder.Start();
							startedone = true;
						}

					}
					else
						exploder.Play();


				}
					
				else
				{
					exploderlist.Remove(exploder);
					explodelist.Remove(exploder.EntityId);
				}
			}

			foreach( var exp in ExplList)
			{
				if(!exp.done)
				{

					exp.Play();
				}
				else
				{
					explosioneffects.Remove(exp);
				}
			}
		}

		private void Init()
		{
			instance = this;
			init = true;
			isServer = MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE || MyAPIGateway.Multiplayer.IsServer;
			isDedicated = (MyAPIGateway.Utilities.IsDedicated && isServer);
			Log.isRunning = true;
			Log.LogDebugLevel = debug;
			Log.Info("Log Started");
			if(isServer)
				MyAPIGateway.Session.DamageSystem.RegisterAfterDamageHandler(100, handler);
			if (!isServer)
				MyAPIGateway.Multiplayer.RegisterMessageHandler(9008, messageHandler: mhandler);
        }
		private void sendExpl(Vector3D ExpPoint)
		{
			byte[] obj = new byte[sizeof(double) * 3];
			BitConverter.GetBytes(ExpPoint.X).CopyTo(obj, 0); 
			BitConverter.GetBytes(ExpPoint.Y).CopyTo(obj, sizeof(double));
			BitConverter.GetBytes(ExpPoint.Z).CopyTo(obj, sizeof(double)*2);
			if (MyAPIGateway.Multiplayer.IsServer)
				MyAPIGateway.Multiplayer.SendMessageToOthers(9008, obj, true);
		}
		private void mhandler(byte[] obj)
		{
			
			if (obj.Length == sizeof(double) * 3)
			{
				var x = BitConverter.ToDouble(obj, 0);
				var y = BitConverter.ToDouble(obj, sizeof(double));
				var z = BitConverter.ToDouble(obj, sizeof(double) * 2);
				var explodeEffect = new ExplosionEffect(new Vector3D(x, y, z));
				explosioneffects.Add(explodeEffect);
				
			}
			else
			{
				
			}

		}

		protected override void UnloadData()
		{
			Log.Close();
			if(!isServer)
				MyAPIGateway.Multiplayer.UnregisterMessageHandler(9008, messageHandler: mhandler);
        }


		private void handler(object target, MyDamageInformation info)
		{
			if (target is IMySlimBlock)
			{
				var entity = target as IMySlimBlock;
				{
					if (entity.FatBlock is MyJumpDrive)
					{
						
						if (explodelist.Contains(entity.FatBlock.EntityId)) return;
						var explode = ((entity.CurrentDamage + info.Amount)) > entity.MaxIntegrity * 0.1;
						var block = entity.FatBlock as MyJumpDrive;
						if (block.Closed) return;
						var blockObj = (MyObjectBuilder_JumpDrive)entity.GetObjectBuilder();
						var power = blockObj.StoredPower;
						
						if (blockObj.StoredPower < Math.Min(1.0, block.BlockDefinition.PowerNeededForJump / 3f))
							return;


						if (explode)
						{

							power *= 5000f;
							var damageblock = (IMyDestroyableObject)entity;
							explodelist.Add(block.EntityId);
							var exploder = new MyJumpExplode(damageblock, power, entity.FatBlock.WorldAABB.Center, block.EntityId);
							if(!(MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE))
								sendExpl(entity.FatBlock.WorldAABB.Center);

							if(!isDedicated)
							{
								var explodeEffect = new ExplosionEffect(entity.FatBlock.WorldAABB.Center);
								explosioneffects.Add(explodeEffect);
							}

							exploderlist.Add(exploder);
							block.Close();
                        }
					}
				}
			}
		}
	}
}
