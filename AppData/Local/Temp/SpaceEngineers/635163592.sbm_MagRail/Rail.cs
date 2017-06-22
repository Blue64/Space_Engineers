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
using System.Globalization;
using SpaceEngineers.Game.ModAPI;

namespace MagRails
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_LandingGear))]
	class Rail : MyGameLogicComponent
	{
		private MyObjectBuilder_EntityBase objectBuilder;
		private bool valid = false;
		private bool initDirty = true;
		private bool updateHook = false;
		private IMyLandingGear gear;
		private RailDefinition def = new RailDefinition();
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
				initBlockSettings();
				if (gear.BlockDefinition.SubtypeName.EndsWith("_D_RAIL") || def.valid)
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
			if (CoreMagRails.instance == null) return;
			try
			{
				initDirty = false;
				
				if (CoreMagRails.instance.Definitions.TryGetDef(gear.BlockDefinition.SubtypeName, out def))
					return;
				def = new RailDefinition();
				def.type = RailType.straight;
				MyCubeBlockDefinition blockDefinition = null;
				if (MyDefinitionManager.Static.TryGetCubeBlockDefinition(gear.BlockDefinition, out blockDefinition))
				{
					var descriptionStr = blockDefinition.DescriptionString;
					def.size = blockDefinition.Size;
					def.sizeenum = (blockDefinition.CubeSize == MyCubeSize.Large ? 2.5 : 0.5);
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
								case "rail":
									if (dataeq[1].ToLowerInvariant() == "yes")
										def.valid = true;
									if (dataeq[1].ToLowerInvariant() == "true")
										def.valid = true;
									break;
								case "curve":
									if (dataeq[1].ToLowerInvariant() == "yes")
										def.type = RailType.curve;
									if (dataeq[1].ToLowerInvariant() == "true")
										def.type = RailType.curve;
									break;
								case "cross":
									if (dataeq[1].ToLowerInvariant() == "yes")
										def.type = RailType.cross;
									if (dataeq[1].ToLowerInvariant() == "true")
										def.type = RailType.cross;
									break;
								case "straight":
									if (dataeq[1].ToLowerInvariant() == "yes")
										def.type = RailType.straight;
									if (dataeq[1].ToLowerInvariant() == "true")
										def.type = RailType.straight;
									break;
								case "slant":
									if (dataeq[1].ToLowerInvariant() == "yes")
										def.type = RailType.slant;
									if (dataeq[1].ToLowerInvariant() == "true")
										def.type = RailType.slant;
									break;
								case "ymin":
									var minvec = def.min;
									minvec.Y = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
									def.min = minvec;
									break;
								case "x":
									def.X = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
									break;
								case "y":
									def.Y = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
									break;
								case "z":
									def.Z = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
									break;
								case "radius":
									def.radius = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
									break;
							}
						}
					}
					if( def.valid )
					if(CoreMagRails.instance.Definitions.TryAdd(gear.BlockDefinition.SubtypeName, def))
					{

					}
					else
					{
						//fail
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

			if (Entity == null || Entity.Closed || Entity.MarkedForClose)
			{
				return;
			}
			if (CoreMagRails.instance == null) return;
			if (!valid) return;
			if (initDirty) initBlockSettings();
			if (CoreMagRails.instance.debug != DebugLevel.Verbose)
			{
				if (updateHook)
				{
					CoreMagRails.UpdateHook -= Update;
					updateHook = false;
				}
					
				return;
			}


			if (ents.Count == 0)
			{
				int n = 30;
				while (n-- > 0)
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
			}
			Vector3D home = WorldtoGrid(Entity.WorldMatrix.Translation, Entity.WorldMatrixNormalizedInv);
			home.X -= def.X;
			home.Y -= def.Y;
			home.Z -= def.Z;

			if (def.type == RailType.curve)
			{
				Vector3D baseangle = new Vector3D(0, 0, 1);

				foreach (var ent in ents)
				{
					int index = ents.IndexOf(ent);

					//MyAPIGateway.Utilities.ShowMessage("Test", Entity.WorldMatrix.Translation.ToString());
					//MyAPIGateway.Utilities.ShowMessage("TestD", (Entity.WorldMatrix.Translation + Vector3D.Multiply(Entity.WorldMatrix.Down, 3)).ToString());
					//going to use X and Z
					double d_angle = ((double)index / 30) / 2;
					d_angle += 0.5d;
					if (def.X > 0)
						d_angle += 0.5d;
					d_angle *= Math.PI;
					double s = Math.Sin(d_angle);
					double c = Math.Cos(d_angle);
					var angle = new Vector3D(-baseangle.Z * s, 0, baseangle.Z * c);//note that for the rail part this will be easier...
					var radianvector = angle * def.radius;
					radianvector += home;
					ent.SetPosition(GridToWorld(radianvector, Entity.WorldMatrix));
				}
			}
			else if (def.type == RailType.straight)
			{
				home.Z -= def.size.Z * 0.5d * def.sizeenum;
				foreach (var ent in ents)
				{
					double index = ents.IndexOf(ent);
					var lerp = Vector3.Lerp(home, new Vector3(home.X, home.Y, def.size.Z * 0.5d * def.sizeenum), (float)(index / 30.0d));
					//var newpos = home + new Vector3D(0,  0, (index - 15d) / 5);
					ent.SetPosition(GridToWorld(lerp, Entity.WorldMatrix));
				}
			}
			else if (def.type == RailType.slant)
			{
				home.Z -= def.size.Z * 0.5d * def.sizeenum;
				//Log.DebugWrite( DebugLevel.Info, "Drawing line");
				//Log.DebugWrite(DebugLevel.Info, home);
				foreach (var ent in ents)
				{
					double index = ents.IndexOf(ent);
					var lerp = Vector3.Lerp(home, new Vector3(home.X, -def.min.Y, def.size.Z * 0.5d * def.sizeenum), (float)(index / 30.0d));
					//var newpos = new Vector3D(home.X, ( home.Y + (def.min.Y - home.Y) * (((index - 15d) / 5) / def.size.Z)), home.Z + ((index - 15d) / 5));

					//Log.DebugWrite(DebugLevel.Info, lerp);
					ent.SetPosition(GridToWorld(lerp, Entity.WorldMatrix));
				}
			}
			else if (def.type == RailType.cross)
			{
				var ftb = new Vector3D(home);
				ftb.Z -= def.size.Z * 0.5d * def.sizeenum;
				var ltr = new Vector3D(home);
				ltr.X -= def.size.X * 0.5d * def.sizeenum;
				foreach (var ent in ents)
				{
					double index = ents.IndexOf(ent);
					Vector3D lerp;
					if(index % 2 == 0)
					{
						lerp = Vector3D.Lerp(ftb, new Vector3D(ftb.X, ftb.Y, def.size.Z * 0.5d * def.sizeenum), (float)(index / 30.0d));
					}
					else
					{
						lerp = Vector3D.Lerp(ltr, new Vector3D(def.size.X * 0.5d * def.sizeenum, ltr.Y, ltr.Z), (float)(index / 30.0d));
					}
					ent.SetPosition(GridToWorld(lerp, Entity.WorldMatrix));
				}
			}
			




			//DOSTUFF
		}

		Vector3D WorldtoGrid(Vector3D coords, MatrixD worldMatrixNormalizedInv)
		{
			Vector3D localCoords = Vector3D.Transform(coords, worldMatrixNormalizedInv);
			return localCoords;
		}
		private Vector3D GridToWorld(Vector3D retval, MatrixD worldMatrix)
		{
			//Vector3D retval = voxPos;
			//retval *= (Term.FrontExtend * _resMult);
			return Vector3D.Transform(retval, worldMatrix);
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
