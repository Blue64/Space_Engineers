using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRageMath;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using Sandbox.Definitions;
using VRage.Game;
using System.Text.RegularExpressions;
using VRage.Game.ModAPI;
using SpaceEngineers.Game.ModAPI;

namespace MagRails
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_LandingGear))]
	class MagLift : MyGameLogicComponent
	{
		private MyObjectBuilder_EntityBase objectBuilder;
		private bool valid = false;
		private bool initDirty = true;
		private bool updateHook = false;
		private IMyLandingGear gear;
		private IMyCubeGrid l_grid;
		private List<IMyEntity> ents = new List<IMyEntity>();

		public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
		{
			return copy ? (MyObjectBuilder_EntityBase)objectBuilder.Clone() : objectBuilder;
		}
		public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{
			this.objectBuilder = objectBuilder;
			try
			{
				gear = (IMyLandingGear)Entity;
				l_grid = (IMyCubeGrid)gear.CubeGrid;
				initBlockSettings();
				if (gear.BlockDefinition.SubtypeName.EndsWith("_D_MAG") || valid)
				{
					valid = true;
					updateHook = true;
					CoreMagRails.UpdateHook += Update;//constrain its position
				}
			}
			catch (Exception ex)
			{
				Log.DebugWrite(DebugLevel.Error, ex);
			}
		}

		private void initBlockSettings()
		{
			try
			{
				initDirty = false;
				MyCubeBlockDefinition blockDefinition = null;
				if (MyDefinitionManager.Static.TryGetCubeBlockDefinition(gear.BlockDefinition, out blockDefinition))
				{
					var descriptionStr = blockDefinition.DescriptionString;
					Regex reg = new Regex("maglift{(.*?)}");
					Regex regcom = new Regex(";");
					Regex regeq = new Regex("=");
					if (descriptionStr == null || descriptionStr.Length == 0) return;
					var res = reg.Split(descriptionStr);
					if (res.Length > 1)
					{
						var search = regcom.Split(res[1]);
						if (search == null)
						{
							return;
						}
						foreach (string parts in search)
						{
							var dataeq = regeq.Split(parts);
							if (dataeq.Length == 0)
							{
								continue;
							}
							switch (dataeq[0].ToLower())
							{
								case "maglift":
									if (dataeq[1].ToLowerInvariant() == "yes")
										valid = true;
									if (dataeq[1].ToLowerInvariant() == "true")
										valid = true;
									break;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				initDirty = true;
				Log.DebugWrite(DebugLevel.Error, "ERROR in definition, could not init check definition description for correct format. " + ex.ToString());
			}
		}
		public void Update()
		{
			if (CoreMagRails.instance == null) return;
			if (Entity == null || Entity.Closed || Entity.MarkedForClose)
			{
				if (updateHook)
				{
					updateHook = false;
					if (CoreMagRails.UpdateHook != null)
						CoreMagRails.UpdateHook -= Update;
					return;
				}
			}
			if (initDirty) initBlockSettings();
			if (!valid) return;
			bool hasphysics = true;
			if (gear.IsLocked)
				return;
			if (l_grid.Physics == null || l_grid.IsStatic == true )
				hasphysics = false;
			Vector3 velocity = Vector3.Zero;
			if(l_grid.Physics != null)
				velocity = l_grid.Physics.LinearVelocity;
			Vector3D myWorld = Entity.WorldMatrix.Translation + Vector3D.Multiply(Entity.WorldMatrix.Down, -0.15d);
			if (CoreMagRails.instance.debug == DebugLevel.Verbose)
			{
				if (ents.Count == 0)
				{
					var prefab = MyDefinitionManager.Static.GetPrefabDefinition("HoloProjectionPrefab");
					MyObjectBuilder_CubeGrid p_grid = prefab.CubeGrids[0];
					MyAPIGateway.Entities.RemapObjectBuilder(p_grid);
					p_grid.CreatePhysics = false;
					IMyEntity grid = MyAPIGateway.Entities.CreateFromObjectBuilder(p_grid);
					grid.Flags &= ~EntityFlags.Save;//do not save
					grid.Flags &= ~EntityFlags.Sync;//do not sync
					MyAPIGateway.Entities.AddEntity(grid);
					ents.Add(grid);
				}
				foreach (var marker in ents)
				{
					marker.SetPosition(myWorld);
				}
			}
			if (!gear.IsWorking) return;

			var wabb = Entity.WorldAABB;
			var detected = MyAPIGateway.Entities.GetEntitiesInAABB(ref wabb);
			var locksearch = new List<IMyEntity>(detected);

			foreach (var ent in locksearch)
			{
				if (ent is IMyLandingGear)
				{
					var l_gear = (IMyLandingGear)ent;
					if (l_gear.IsLocked)
						return;
				}
			}
			
			double factor = 5000000.0;
			double nfactor = 2000000.0;
			foreach (var ent in detected)
			{
				if (ent.EntityId == Entity.EntityId) continue;
				if(ent is IMyLandingGear)
				{
					var l_gear = (IMyLandingGear)ent;
					if (l_gear.CubeGrid == null) continue;
					
					if (l_gear.CubeGrid.EntityId == l_grid.EntityId) continue;
					if (!l_gear.IsWorking) continue;
					RailDefinition def;
					if(CoreMagRails.instance.Definitions.TryGetDef(l_gear.BlockDefinition.SubtypeName, out def))
					{
						Vector3D pos = WorldtoGrid(myWorld, l_gear.WorldMatrixNormalizedInv);
						var bb = new BoundingBoxD(Vector3D.Zero,  ( (Vector3D)def.size ) * def.sizeenum);
						var nbb = new BoundingBoxD(-bb.HalfExtents, bb.HalfExtents);
						if (nbb.Contains(pos) == ContainmentType.Disjoint)
							continue;
						if (nbb.Contains(pos) == ContainmentType.Intersects)
						{
							factor /= 2d;
							nfactor /= 2d;
						}
							
						var matrix = l_gear.WorldMatrixNormalizedInv;
						matrix.Translation = new Vector3D(0, 0, 0);
                        velocity = WorldVectorToGrid(velocity, matrix);
						var damp = (double)velocity.Y * 20000;
                        if (def.type == RailType.straight)
						{

							pos = pos + def.pos;
							double vert = 0;
							if (pos.Y >= 0)
								vert = (pos.Y * factor);
							else
								vert = (pos.Y * nfactor);
							damp = MathHelper.Clamp(Math.Abs(damp), 0, Math.Abs(vert)) * Math.Sign(damp);
							var _r = Vector3D.Multiply(l_gear.WorldMatrix.Left, pos.X * factor);
							Vector3D _u = Vector3D.Multiply(l_gear.WorldMatrix.Down, vert + damp);
							var force = (_r + _u);

							if (l_grid != null && hasphysics)
							{
								l_grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force, myWorld, Vector3.Zero);
							}
							if( l_gear.CubeGrid.Physics != null && !l_gear.CubeGrid.IsStatic)
							{
								l_gear.CubeGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -force, myWorld, Vector3.Zero);
							}
						}
						else if (def.type == RailType.cross)
						{
							pos = pos + def.pos;
							double vert = 0;
							if (pos.Y >= 0)
								vert = (pos.Y * factor);
							else
								vert = (pos.Y * nfactor);
							//clamp by the lesser of the two values
							damp = MathHelper.Clamp(Math.Abs(damp), 0, Math.Abs(vert)) * Math.Sign(damp);
							Vector3D _r = new Vector3D(Vector3D.Zero);
							Vector3D _f = new Vector3D(Vector3D.Zero);
							if (Math.Abs(pos.X) < Math.Abs(pos.Z))
							{
								_r = Vector3D.Multiply(l_gear.WorldMatrix.Left, pos.X * factor);
							}
							else if (Math.Abs(pos.Z) < Math.Abs(pos.X))
							{
								_f = Vector3D.Multiply(l_gear.WorldMatrix.Forward, pos.Z * factor);
							}
							

							Vector3D _u = Vector3D.Multiply(l_gear.WorldMatrix.Down, vert + damp);
							var force = (_r + _u + _f);

							if (l_grid != null && hasphysics)
							{
								l_grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force, myWorld, Vector3.Zero);
							}
							if (l_gear.CubeGrid.Physics != null && !l_gear.CubeGrid.IsStatic)
							{
								l_gear.CubeGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -force, myWorld, Vector3.Zero);
							}
						}
						else if (def.type == RailType.slant)
						{
							var sizez = def.size.Z * def.sizeenum;
							double _factor = ((sizez * 0.5) + pos.Z) / sizez;
							var lerp = Vector3.Lerp(def.pos, def.min, (float)_factor);
							pos = pos + lerp;
							var _r = Vector3D.Multiply(l_gear.WorldMatrix.Left, pos.X * factor);
							double vert = 0;
							if (pos.Y >= 0)
								vert = (pos.Y * factor);
							else
								vert = (pos.Y * nfactor);
							damp = MathHelper.Clamp(Math.Abs(damp), 0, Math.Abs(vert)) * Math.Sign(damp);
							var _u = Vector3D.Multiply(l_gear.WorldMatrix.Down, vert + damp);

							var force = (_r + _u);
							if (l_grid != null && hasphysics)
							{
									l_grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force, myWorld, Vector3.Zero);
							}
							if (l_gear.CubeGrid.Physics != null && !l_gear.CubeGrid.IsStatic)
							{
								l_gear.CubeGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -force, myWorld, Vector3.Zero);
							}
						}
						else if (def.type == RailType.curve)
						{
							var rel = def.pos + pos;
							var vec = Vector3.Zero;

							vec = Vector3.Multiply(Vector3D.Normalize(rel), (float)(rel.Length() - def.radius));
							if (vec.Length() < 0.25)
								vec = Vector3.Zero;
							else
							{
								vec -= Vector3.Multiply(Vector3.Normalize(vec), (float)0.25);
							}

							var _r = Vector3D.Multiply(l_gear.WorldMatrix.Left, (vec.X) * factor);
							var _f = Vector3D.Multiply(l_gear.WorldMatrix.Backward, (vec.Z) * factor);
							double vert = 0;
							if (pos.Y >= 0)
								vert = (pos.Y * factor);
							else
								vert = (pos.Y * nfactor);
							damp = MathHelper.Clamp(Math.Abs(damp), 0, Math.Abs(vert)) * Math.Sign(damp);
							var _u = Vector3D.Multiply(l_gear.WorldMatrix.Down, vert + damp);

							var force = (_r + _u);

							if (l_grid != null && hasphysics)
							{
								l_grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE,  force, myWorld, Vector3.Zero);
							}
							if (l_gear.CubeGrid.Physics != null && !l_gear.CubeGrid.IsStatic)
							{
								l_gear.CubeGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -force, myWorld, Vector3.Zero);
							}
						}

					}
				}
			}

		}
		Vector3D WorldtoGrid(Vector3D coords, MatrixD worldMatrixNormalizedInv)
		{
			Vector3D localCoords = Vector3D.Transform(coords, worldMatrixNormalizedInv);
			return localCoords;
		}
		Vector3D WorldVectorToGrid(Vector3D vector, MatrixD localMatrixNormalizedInv)
		{
			Vector3D localCoords = Vector3D.Transform(vector, localMatrixNormalizedInv);
			return localCoords;
		}

		public override void Close()
		{
			foreach (var ent in ents)
			{
				ent.Close();
			}
			if (updateHook)
			{
				updateHook = false;
				if (CoreMagRails.UpdateHook != null)
					CoreMagRails.UpdateHook -= Update;
			}
		}
	}
}
