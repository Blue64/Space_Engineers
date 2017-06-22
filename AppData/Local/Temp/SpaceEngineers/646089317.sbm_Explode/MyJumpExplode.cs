using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRageMath;
using VRage.Game.Entity;
using VRage.Game.Components;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Game.ModAPI.Interfaces;
using VRage.Game.ModAPI;
using Sandbox.Game.Entities.Cube;

namespace JumpExplode
{
	public class MyJumpExplode
	{

		internal bool done = false;
		private Vector3D m_center;
		private IMyDestroyableObject m_damageblock;
		private float m_power;
		private long m_entityid = 0;
		private int step = 120;
		public bool started = false;
		BoundingSphereD m_explosion;
		MyEntity3DSoundEmitter emitter;
		List<IMyEntity> m_exp_entities = new List<IMyEntity>();


		public long EntityId
		{
			get
			{
				return m_entityid;
			}
			private set
			{
				m_entityid = value;
			}
		}

		public MyJumpExplode(IMyDestroyableObject damageblock, float power, Vector3D center, long entityid)
		{

			m_damageblock	= damageblock;
			m_power			= power;
			m_center		= center;
			m_entityid		= entityid;
			m_explosion		= new BoundingSphereD(center, Math.Pow(power, 1d / 3d));
			m_damageblock.DoDamage(float.MaxValue / 2f, MyDamageType.Destruction, true);

		}

		internal void Play()
		{
			step--;
			int cnt = 0;
		
            foreach (var ent in m_exp_entities)
			{
				if (ent is MyCubeGrid)
				{

					var grid = ent as MyCubeGrid;
					var igrid = ent as IMyCubeGrid;

					if (grid.Closed) return;

					cnt++;

					if (grid.Physics != null && grid.CubeBlocks.Count > 0)
					{

						var explodeblock = (grid.CubeBlocks.FirstElement() as IMySlimBlock);
						//var damageblock = (IMyDestroyableObject)explodeblock;

                        var blockpos = explodeblock.Position;
						var worldpos = grid.GridIntegerToWorld(blockpos);
				
						//damageblock.DoDamage(explodeblock.MaxIntegrity / 150f * MyUtils.GetRandomFloat(0.9f, 1.5f), MyDamageType.Deformation, true);

						if (explodeblock.IsDestroyed)
						{
							igrid.RemoveBlock(explodeblock, true);
							continue;
						}

						if ((step + cnt) % 60 >= 30)
							grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, Vector3.Multiply(Vector3.Normalize( m_center - worldpos), 400000), ent.Physics.CenterOfMassWorld, null);
						else if ((step + cnt) % 60 <= 29)
							grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, Vector3.Multiply(Vector3.Normalize(worldpos - m_center), 200000), ent.Physics.CenterOfMassWorld, null);

					}
				}
			}
			if (step <= 0)
			{
				foreach (var ent in m_exp_entities)
				{
					if (ent is MyCubeGrid)
					{
						var grid = ent as MyCubeGrid;
						if (grid.Physics != null && grid.CubeBlocks.Count > 0)
						{

							var explodeblock = (grid.CubeBlocks.FirstElement() as IMySlimBlock);
							var blockpos = explodeblock.Position;
							var worldpos = grid.GridIntegerToWorld(blockpos);
							grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, Vector3.Multiply(Vector3.Normalize(worldpos - m_center), 4000000), ent.Physics.CenterOfMassWorld, null);

						}
					}
				}
				done = true;

				return;
			}
		}

		internal void Start()
		{
			List<MyEntity> entities = MyEntities.GetEntitiesInSphere(ref m_explosion);
			started = true;
			//PLAY SOUND

			emitter = new MyEntity3DSoundEmitter(null);
			emitter.SetPosition(m_explosion.Center);
			emitter.SetVelocity(Vector3.Zero);
			MySoundPair m_bombExpl = new MySoundPair("ArcWepLrgWarheadExpl");
			emitter.CustomMaxDistance = (float)Math.Pow(m_explosion.Radius, 2);
			emitter.CustomVolume = 10f;
			emitter.PlaySingleSound(m_bombExpl, true);




			//try
			//{

			foreach (var loopentity in entities)
			{
				if (loopentity.EntityId == m_entityid)
					continue;

				//float damage = m_power;

				if (loopentity is IMyCharacter)
				{
					if (!MyAPIGateway.Session.CreativeMode)
					{
						var mycharacter = (IMyCharacter)loopentity;
						mycharacter.Kill();
					}

					continue;
				}
				if (loopentity is IMyFloatingObject)
				{
					loopentity.Close();
				}
				if (loopentity is MyVoxelBase)
				{
					var voxels = loopentity as MyVoxelBase;
					/*MyShape shape = new MyShapeSphere()
					{
						Center = m_center,
						Radius = (float)m_explosion.Radius
					};
					MyVoxelGenerator.CutOutShape(voxels, shape);*/
				}


				if (loopentity is MyCubeBlock)
				{
					continue;
				}

				if (loopentity is MyCubeGrid)
				{

					var grid = (IMyCubeGrid)loopentity;
					List<IMySlimBlock> blocks = grid.GetBlocksInsideSphere(ref m_explosion);

					var mgrid = (MyCubeGrid)loopentity;
					//HashSet<MySlimBlock> myblocks = new HashSet<MySlimBlock>();
					//mgrid.GetBlocksInsideSphere(ref m_explosion, myblocks);
					if (mgrid.Physics == null && !mgrid.IsStatic)
						return;

					try
					{
						if (mgrid.CubeBlocks.Count > 1)
						{
							/*foreach (var bck in myblocks)
							{
								var expl = (IMyDestroyableObject)bck;
								if (expl == damageblock) continue;
								expl.DoDamage(bck.MaxIntegrity / 2f, MyDamageType.Deformation, true);//* MyUtils.GetRandomFloat(0.0f, 3.0f)

								if(mgrid.CubeExists(bck.Position))
								{
									List<MySlimBlock> split = new List<MySlimBlock>();
									split.Add(bck);
									MyCubeGrid.CreateSplit(mgrid, split);
								}

							}*/
							foreach (var bck in blocks)
							{
								if (bck.IsDestroyed)
								{
									grid.RemoveDestroyedBlock(bck);
									continue;
								}
								var expl = (IMyDestroyableObject)bck;
								if (expl == m_damageblock) continue;
								var blockpos = bck.Position;
                                expl.DoDamage(MyUtils.GetRandomFloat(0.0f, 1.5f) * bck.MaxIntegrity, MyDamageType.Deformation, true);//* MyUtils.GetRandomFloat(0.0f, 3.0f)
								if (MyUtils.GetRandomInt(10) > 7 && !bck.IsDestroyed)
								{
									//var blockv3i = new List<Vector3I>();
									//blockv3i.Add(blockpos);
                                    //mgrid.CreateSplit_Implementation(blockv3i, 0);
									var builder = MyObjectBuilderSerializer.CreateNewObject(typeof(MyObjectBuilder_CubeGrid)) as MyObjectBuilder_CubeGrid;
									builder.EntityId = 0;
									builder.GridSizeEnum = mgrid.GridSizeEnum;
									builder.IsStatic = false;
									builder.PersistentFlags = mgrid.Render.PersistentFlags;
									builder.PositionAndOrientation = new MyPositionAndOrientation(mgrid.WorldMatrix);
									builder.CubeBlocks.Add(bck.GetObjectBuilder());
									builder.CreatePhysics = true;//physics on, not static

									MyAPIGateway.Entities.RemapObjectBuilder(builder);
									var physicsgrid = MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(builder);
									m_exp_entities.Add(physicsgrid);
									//var g = (IMyCubeGrid)physicsgrid;
									//g.ApplyDestructionDeformation(((MyCubeGrid)physicsgrid).CubeBlocks.FirstElement() as IMySlimBlock);

								}
								else
								{
									grid.RemoveBlock(bck, true);
								}
								//if(grid.CubeExists(blockpos))
									



							}
							continue;
						}
						var blk = mgrid.CubeBlocks.FirstElement() as IMySlimBlock;
						if (blk.IsDestroyed) continue;
						var expld = (IMyDestroyableObject)blk;
						expld.DoDamage(blk.MaxIntegrity / 2f, MyDamageType.Deformation, true);
						m_exp_entities.Add(loopentity);
					}
					catch
					{
						continue;
					}

				}
			}

		}
	}

}
