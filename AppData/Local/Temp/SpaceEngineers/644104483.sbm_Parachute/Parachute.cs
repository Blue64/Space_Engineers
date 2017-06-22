using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRageMath;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Entities;

namespace Parachute
{
	
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_AdvancedDoor), true, "LgParachute", "SmParachute")]
    class Parachute : MyGameLogicComponent
	{
		
		private MyObjectBuilder_EntityBase objectBuilder;
		private IMyEntity chute;
		private IMyDoor block;
		private bool valid = false;
		private int deployStage = 0;
		private int step = 0;
		private int cutstep = 0;
		private bool open = false;
		double scalex = 1;
		double scaley = 1;
		double scalez = 1;
		private bool isupdating = false;
		Vector3D lastvector = new Vector3D(0, 1, 0);
		private int rotstep = 0;
		private double radius = 0;
		Quaternion lastrot = Quaternion.Identity;
		Vector3 gravity = Vector3.Zero;
		Vector3D lastscale = new Vector3D(1);
		bool isCut = false;
		private float atmosphere
		{
			get
			{
				float retval = 0;
				if (CoreParachute.instance == null)
					return 0.0f;
				if (CoreParachute.instance.planets.Count == 0)
					return 0.0f;
				gravity = Vector3.Zero;
				foreach (var planet in CoreParachute.instance.planets)
				{
					var air = planet.Value.GetAirDensity(Entity.WorldMatrix.Translation);
					if (air > 0)
					{
						retval += air;
						IMyGravityProvider grav = planet.Value.Components.Get<MyGravityProviderComponent>();
						if (grav == null) continue;
						gravity = Vector3D.Normalize(grav.GetWorldGravity(Entity.WorldMatrix.Translation));//planet.Value.GetWorldGravity()
					}

				}
				return retval;
			}
		}

		public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
		{
			return copy ? (MyObjectBuilder_EntityBase)objectBuilder.Clone() : objectBuilder;
		}
		public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{
			Log.DebugWrite(DebugLevel.Info, "Init Called");
			this.objectBuilder = objectBuilder;
			block = (IMyDoor)Entity;
			if (block.BlockDefinition.SubtypeName == "LgParachute" || block.BlockDefinition.SubtypeName == "SmParachute")
			{
				Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
				
			
				valid = true;
				block.DoorStateChanged += Block_DoorStateChanged;

            }
		}

		private void Block_DoorStateChanged(bool state)
		{
			Log.DebugWrite(DebugLevel.Info, "Block State Change Detected");
			open = state;
		}
		public void Update()
		{
			if (!valid)
				return;
			Log.DebugWrite(DebugLevel.Info, "Parachute Update Called");
			try
			{
				
				if (Entity == null)
				{

					if (chute != null)
					{
						if (!chute.Closed && !chute.MarkedForClose)
							chute.Close();

					}
					if (isupdating)
					{
						CoreParachute.updateChute -= Update;//stop updating
						isupdating = false;
					}
					return;
				}
				if (block?.Parent?.Physics == null) return;
				if (open && !isCut)
				{
					if (atmosphere > 0.1f)
					{
						switch (deployStage)
						{
							case 0:
								doDeployChute();//spawns chute and attaches
								break;
							case 1:
								updateChute(); //deploying
								moveChute();//constrain
								break;
							case 2:
								//double lastscale = scalex;
								double scale = 10d * (atmosphere - 0.6);
                                if (scale <= 0.5d || double.IsNaN(scale))
								{
									scale = 0.5d;
								}
								else
								{
									scale = Math.Log(scale - 0.99d) + 5;
									if (scale < 0.5d || double.IsNaN(scale))
										scale = 0.5d;
                                }
								scalex = scaley = (scale * 8d * block.CubeGrid.GridSize);
                                moveChute();//constrain
								break;
						}
					}
				}
				else
				{
					if (deployStage > 0)
					{
						deployStage = 0;
						cutChute();
						step = 60;//60 ticks and delete
					}
					else if (step > 0)
					{
						playCut();
						step--;
					}
					if (chute != null && step <= 0)
					{
						if (!chute.Closed && !chute.MarkedForClose)
							chute.Close();
						if (isupdating)
						{
							CoreParachute.updateChute -= Update;//stop updating
							isupdating = false;
						}
						isCut = false;//make it false

					}
				}
			}
			catch (Exception ex)
			{
				Log.DebugWrite(DebugLevel.Error, ex);
			}
		}

		public override void UpdateBeforeSimulation()
		{
			if (!valid) return;
			if (CoreParachute.instance == null) return;
			if (MyAPIGateway.Utilities == null) return;
			if (Entity.Closed || Entity.MarkedForClose) return;
			if (block?.Parent?.Physics == null) return;
			if (block.InScene && block.IsFunctional)
			{

				if (open)
				{
					if(!isupdating && deployStage == 0)
					{
						isupdating = true;
						CoreParachute.updateChute += Update;
					}
				}
				else
				{

				}
			}
		}

		private void playCut()
		{
			
		}

		private void cutChute()
		{
			isCut = true;
		}

		private void moveChute()
		{
			if (chute == null || chute.Closed || chute.MarkedForClose)
			{
				deployStage = 0;
				step = 0;

				return;
			}

			if(block.Parent.Physics.LinearVelocity.Length() > 2)
			{
				lastvector = block.Parent.Physics.LinearVelocity;
				cutstep = 0;
            }
			else
			{
				lastvector = Vector3.Lerp(lastvector, -gravity, 0.05f);
				if (Vector3.Distance(lastvector, -gravity) < 0.05d)
				{

					cutstep++;
					if(cutstep > 60)
					{
						
						cutChute();
					}

				}
			}


			Vector3D lvNorm = Vector3D.Normalize(lastvector);
			rotstep++;
			var rotmat = Matrix.CreateFromDir(lvNorm, new Vector3(0, 1, 0));
			Quaternion rot = Quaternion.CreateFromRotationMatrix(rotmat.GetOrientation());//Quaternion.FromVector4(new Vector4(Vector3.Normalize(block.WorldMatrix.Forward), 0));
			rot = Quaternion.Lerp(lastrot, rot, 0.02f);
			var newscale = new Vector3D(scalex, scaley, scalez * 10);
			newscale = Vector3D.Lerp(lastscale, newscale, 0.02d);
			radius = newscale.X / 2;//this will always be true ;)
			if (radius <= 0.0d)
				return;
			MatrixD mat = MatrixD.CreateFromTransformScale(rot, block.WorldMatrix.Translation +(block.WorldMatrix.Up * (block.CubeGrid.GridSize / 2d) ), newscale);//

			lastscale = newscale;
			lastrot = rot;
			chute.SetWorldMatrix(mat);
			var cgrid = (IMyCubeGrid)chute;
	
			if (block.Parent.Physics.LinearVelocity.Length() <= 1) return;
			if (atmosphere < 0.2d) return;
			Vector3D drag = -lvNorm;
			double a = Math.PI * radius * radius;
			double c = (2.5d * (atmosphere * 1.225d) * lastvector.LengthSquared() * a);
			if (c > 0d)
				block.CubeGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, Vector3D.Multiply(drag, c), block.WorldMatrix.Translation, Vector3.Zero);
        }

		private void updateChute()
		{
			step++;
			if (step >= 50)
			{
				deployStage = 2;
				return; 
			}
			scalez = Math.Log(step / 1.5d) * block.CubeGrid.GridSize * 2d;
        }

		private void doDeployChute()
		{

			if (chute == null || chute.Closed)
			{

				

				if (block.Parent.Physics.LinearVelocity.Length() < 2.0f)
					return;//dont deploy.
				
				if (atmosphere < 0.5d) return;
				
				if (!canDeploy())
					return;
				damageChute();
				Vector3D lvNorm = Vector3D.Normalize(block.Parent.Physics.LinearVelocity);
				var rotmat = Matrix.CreateFromDir(lvNorm, new Vector3(0, 1, 0));
				lastrot = Quaternion.CreateFromRotationMatrix(rotmat.GetOrientation());
				var prefab = MyDefinitionManager.Static.GetPrefabDefinition("ParachuteLg");
				var p_grid = prefab.CubeGrids[0];
				p_grid.PositionAndOrientation = new VRage.MyPositionAndOrientation(block.WorldMatrix.Translation, block.WorldMatrix.Forward, block.WorldMatrix.Up );
				p_grid.LinearVelocity = Vector3.Zero;
				p_grid.CreatePhysics = false;
				foreach (var cubeblock in p_grid.CubeBlocks)
				{
					cubeblock.ColorMaskHSV = block.Render.ColorMaskHsv;
				}
				MyAPIGateway.Entities.RemapObjectBuilder(p_grid);
				chute = MyAPIGateway.Entities.CreateFromObjectBuilder(p_grid);
				chute.Flags |= EntityFlags.Visible;
				chute.Flags &= ~EntityFlags.Save;//do not save
				(chute as MyCubeGrid).IsSplit = true;//cheat!
				MyAPIGateway.Entities.AddEntity(chute);
				deployStage = 1;
				step = 0;
				scalex = 1;
				scaley = 1;
				scalez = 1;
				cutstep = 0;
				lastscale = new Vector3D(1);
			}

		}

		private void damageChute()
		{
			var fblock = block.CubeGrid.GetCubeBlock(block.Position);
			var damageblock = (IMyDestroyableObject)fblock;
			damageblock.DoDamage((block.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 5f : 25f), MyDamageType.Unknown, true);			
		}

		private bool canDeploy()
		{
			var fblock = block.CubeGrid.GetCubeBlock(block.Position);
			if (block.IsWorking && fblock.IsFullIntegrity)
			{
				return true;
			}
			return false;
		}

		private void onClose(IMyEntity obj)
		{
			if(chute != null)
			{
				chute.Close();
			}
			if (isupdating)
				CoreParachute.updateChute -= Update;
			isupdating = false;
		}
	}
}
