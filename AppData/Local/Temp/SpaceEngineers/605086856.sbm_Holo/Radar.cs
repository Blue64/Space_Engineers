using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRageMath;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using Sandbox.Definitions;
using IMySensorBlock = Sandbox.ModAPI.IMySensorBlock;
using Sandbox.ModAPI.Interfaces;
using ParallelTasks;
using VRage.Voxels;
using VRage.Game.Entity;
using VRage.Game;
using VRage.Game.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Utils;
using VRage.Game.ObjectBuilders.ComponentSystem;
using Draygo.Utils;
using Sandbox.Game.EntityComponents;
namespace Hologram
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_SensorBlock), true)]
	public class Radar : MyGameLogicComponent
	{
		private MyObjectBuilder_EntityBase objectBuilder;
		private Task task;
		private IMySensorBlock Term;
		private double resolution = 5;
		//private MyObjectBuilder_CubeGrid projection;
		private Dictionary<Vector3D, IMySensorBlock> notifyCache;
		private bool activeMode = true;
		private int tick = 0;
		public bool valid = false;
		private double _resMult = 20;
		private bool blue = false;
		private RadarResult _result = new RadarResult();
		private RadarResult T_result = new RadarResult();
		private float m_range = 5f;
		Dictionary<Vector3I, IMySensorBlock> pingResult = new Dictionary<Vector3I, IMySensorBlock>();
		ThreadManagerTask UpdateTask;
		public float Range
		{
			get { return m_range; }
			set
			{
				float new_value = MathHelper.Clamp(value, 0.001f, 50.0f);
				if (new_value != m_range)
				{
					m_range = new_value;
					Store(m_range);
					if (CoreHolo.instance != null) CoreHolo.instance.SendUpdate(Entity.EntityId, m_range);
				}
			}
		}
		public RadarResult RadarData
		{
			get { return _result; }
		}
		public long GridEntityID
		{
			get
			{
				if (Entity.Parent != null) return Entity.Parent.EntityId;
				return 0;
			}
		}
		public IMySensorBlock _IMySensorBlock
		{
			get { return Term; }
		}
		public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
		{
			return copy ? (MyObjectBuilder_EntityBase)objectBuilder.Clone() : objectBuilder;
		}
		public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{
			Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
			this.objectBuilder = objectBuilder;
			if (Entity.Storage == null) Entity.Storage = new MyModStorageComponent();
		}
		public override void UpdateOnceBeforeFrame()
		{
			try
			{
				Term = (IMySensorBlock)Entity;
				if (Term.BlockDefinition.SubtypeName.EndsWith("_DS_RADAR"))
				{
					CreateTerminalControls<IMySensorBlock>();
					valid = true;
					notifyCache = new Dictionary<Vector3D, IMySensorBlock>();
					CoreHolo.Register(Entity.EntityId, this);
					Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;//update the block.
				}
				if (MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE || MyAPIGateway.Multiplayer.IsServer) Read();
				else
					CoreHolo.RequestSetting(Entity.EntityId);
			}
			catch (Exception ex)
			{
				Log.DebugWrite(DebugLevel.Error, ex);
			}
		}
		public override void UpdateBeforeSimulation10()
		{
			//radarinfo
			if (Entity.Closed || Entity.MarkedForClose) return;
			if (!valid) return;
			if (CoreHolo.instance == null) return;
			if (CoreHolo.instance.isDedicated)
			{
				//Entity.NeedsUpdate &= ~MyEntityUpdateEnum.EACH_10TH_FRAME;
				return;
			}
			//MyAPIGateway.Utilities.ShowMessage("Radar", Term.IsRadar().ToString());
			if (!CoreHolo.ClosetoPlayer(Entity.WorldMatrix.Translation, Range * 50 * _resMult)) return;
			Term.SetValueBool("Detect Enemy", false);
			Term.SetValueBool("Detect Neutral", false);
			Term.SetValueBool("Detect Friendly", false);
			Term.SetValueBool("Detect Owner", false);
			Term.SetValueBool("Detect Asteroids", false);
			Term.SetValueBool("Detect Stations", false);
			Term.SetValueBool("Detect Large Ships", false);
			Term.SetValueBool("Detect Small Ships", false);
			Term.SetValueBool("Detect Floating Objects", false);
			Term.SetValueBool("Detect Players", false);
			setResolution();
			activeMode = Term.GetValueBool("OnOff");
			tick++;
			if (tick < 5) return;
			else
				tick = 0;//slow updates for passive.
			if (activeMode == false)
			{
			}
			if (Term.IsFunctional && ( UpdateTask == null || UpdateTask.Added == false || UpdateTask.IsComplete == true) )
			{
				var _data = new Dictionary<Vector3D, IMySensorBlock>(notifyCache);
				notifyCache.Clear();
				//enqueue task on a single thread using the thread manager.
				if (CoreHolo.instance?.TManager != null) UpdateTask = CoreHolo.instance.TManager.Add(() => refreshRadar(_data, activeMode), calcComplete);
			}
		}
		protected static List<Type> m_ControlsInited = new List<Type>();
		protected static IMyTerminalControlSeparator Seperator, Seperator2;
		//protected static IMyTerminalControlCheckbox YawCheck, PitchCheck, RollCheck, InvertCheck, InvertRollCheck;
		protected static IMyTerminalControlSlider RangeControl;
		//protected static IMyTerminalControlButton SaveButton;
		protected void CreateTerminalControls<T>()
		{
			if (m_ControlsInited.Contains(typeof(T))) return;
			m_ControlsInited.Add(typeof(T));
			if (Seperator == null)
			{
				Seperator = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, T>(string.Empty);
				Seperator.Visible = (b) => b.IsRadar();
			}
			MyAPIGateway.TerminalControls.AddControl<T>(Seperator);
			if (RangeControl == null)
			{
				RangeControl = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>("Draygo.Radar.Range");
				RangeControl.Visible = (b) => b.IsRadar();// b.IsRadar();
				RangeControl.Enabled = (b) => b.IsRadar();
				RangeControl.Getter = (b) => b.GameLogic.GetAs<Radar>().Range;
				RangeControl.Writer = (b, v) => v.AppendFormat("{0:N1} {1}", b.GameLogic.GetAs<Radar>().Range, MyStringId.GetOrCompute("km"));
				RangeControl.Setter = (b, v) => b.GameLogic.GetAs<Radar>().Range = v;
				RangeControl.Title = MyStringId.GetOrCompute("Range");
				RangeControl.Tooltip = MyStringId.GetOrCompute("Range in KM");
				RangeControl.SupportsMultipleBlocks = true;
				RangeControl.SetLimits(0.001f, 50.0f);
			}
			MyAPIGateway.TerminalControls.AddControl<T>(RangeControl);
			if (Seperator2 == null)
			{
				Seperator2 = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, T>(string.Empty);
				Seperator2.Visible = (b) => b.IsRadar();
			}
			MyAPIGateway.TerminalControls.AddControl<T>(Seperator2);
			//MyAPIGateway.TerminalControls.AddControl<T>(RangeControl);
			//if (PitchCheck == null)
			//{
			//	PitchCheck = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyTerminalBlock>("Draygo.ControlSurface.Pitch");
			//	PitchCheck.Visible = (b) => b.IsControlSurface();
			//	PitchCheck.Enabled = (b) => b.IsControlSurface() && b.IsWorking;
			//	PitchCheck.Getter = (b) => b.GameLogic.GetAs<Holo>().Control.EnablePitch;
			//	PitchCheck.Setter = (b, v) => b.GameLogic.GetAs<Holo>().Control.EnablePitch = v;
			//	PitchCheck.Title = MyStringId.GetOrCompute("Pitch");
			//	PitchCheck.Tooltip = MyStringId.GetOrCompute("Enable Pitch Control");
			//	PitchCheck.SupportsMultipleBlocks = true;
			//}
			//MyAPIGateway.TerminalControls.AddControl<T>(PitchCheck);
			var RangeProperty = MyAPIGateway.TerminalControls.CreateProperty<float, T>("Radar.Range");
			if (RangeProperty != null)
			{
				RangeProperty.Enabled = (b) => b.IsRadar();
				RangeProperty.Getter = (b) => b.GameLogic.GetAs<Radar>().Range;
				RangeProperty.Setter = (b, v) => b.GameLogic.GetAs<Radar>().Range = v;
				MyAPIGateway.TerminalControls.AddControl<T>(RangeProperty);
			}
		}
		internal static void RedrawControls()
		{
			if (RangeControl != null) RangeControl.UpdateVisual();
		}
		private void setResolution()
		{
			updateResolution();
		}
		private void updateResolution()
		{
			//resolution = value;
			Term.SetValueFloat("Bottom", 0.0f);
			Term.SetValueFloat("Right", 0.0f);
			Term.SetValueFloat("Back", 0.0f);
			Term.SetValueFloat("Front", 0.0f);
			Term.SetValueFloat("Left", 0.0f);
			Term.SetValueFloat("Top", 0.0f);
		}
		private void calcComplete()
		{
			//MyAPIGateway.Utilities.ShowMessage("CalcDone", T_result.ColorData.Count.ToString());
			_result = new RadarResult(T_result);
			T_result.Clear();
			if (!Entity.MarkedForClose || Entity.Closed)
			{
				foreach (KeyValuePair<Vector3I, IMySensorBlock> pingblocks in pingResult)
				{
					var radar = CoreHolo.GetRadar(pingblocks.Value.EntityId);
					if (radar != null) radar.Notify(Term.WorldMatrix.Translation, Term);
				}
				pingResult.Clear();
				//projection = p_grid;
			}
		}
		internal void UpdateRange(float range)
		{
			m_range = MathHelper.Clamp(range, 0.001f, 50.0f);//update it without triggering a sync.
		}
		private void Store(float new_value)
		{
			if (Entity.Storage != null) Entity.Storage[CoreHolo.ModGuid] = new_value.ToString();
		}
		private void Read()
		{
			try
			{
				if (float.TryParse(Entity.Storage[CoreHolo.ModGuid], out m_range)) return;
				else
					m_range = 5.0f;
			}
			catch
			{
				m_range = 5.0f;
			}
		}
		Vector3D WorldtoGrid(Vector3D coords, MatrixD worldMatrixNormalizedInv)
		{
			Vector3D localCoords = Vector3D.Transform(coords, worldMatrixNormalizedInv);
			localCoords /= (Range * _resMult);
			return localCoords;
		}
		private Vector3D GridToWorld(Vector3I voxPos, MatrixD worldMatrix)
		{
			Vector3D retval = voxPos;
			retval *= (Range * _resMult);
			return Vector3D.Transform(retval, worldMatrix);
		}
		private void refreshRadar(Dictionary<Vector3D, IMySensorBlock> cache, bool _activeMode)
		{
			//if (CoreHolo.instance.settings.new_scan_method)
			//{
			//	newRefreshRadar(cache, _activeMode);
			//	return;
			//}
			Random m_rand = new Random();
			if (!valid) return;
			if (Term == null) return;
			var shipgrid = (IMyCubeGrid)Term.CubeGrid;
			MatrixD panelWorld = MatrixD.CreateFromQuaternion(Base6Directions.GetOrientation(Base6Directions.Direction.Forward, Base6Directions.Direction.Up));
			panelWorld.Translation = Term.WorldMatrix.Translation;
			MatrixD normalizedWorld = MatrixD.Normalize(panelWorld);
			MatrixD panelMatrixNormalizedInv = MatrixD.Invert(normalizedWorld);
			HashSet<Vector3I> check = new HashSet<Vector3I>();
			HashSet<MyPlanet> planets = new HashSet<MyPlanet>();
			HashSet<IMyVoxelBase> voxels = new HashSet<IMyVoxelBase>();
			HashSet<Vector3I> absolute = new HashSet<Vector3I>();
			Dictionary<Vector3I, Vector3D> toWorldCache = new Dictionary<Vector3I, Vector3D>();
			Dictionary<Vector3I, ResultType> blockCache = new Dictionary<Vector3I, ResultType>();
			Dictionary<Vector3I, IMySensorBlock> pingCache = new Dictionary<Vector3I, IMySensorBlock>();
			//pingResult.Clear();
			Dictionary<Vector3I, ResultType> m_ColorData = new Dictionary<Vector3I, ResultType>();
			ResultType blockcolor = ResultType.Self_Point_Alt;
			try
			{
				if (blue && !CoreHolo.instance.settings.show_ship)
				{
					blockcolor = ResultType.Self_Point;
					blue = false;
				}
				else
					blue = true;
				Color SelfColor = RadarResult.getColor(blockcolor);
				//int NewEntityId = 1;
				check.Add(new Vector3I(0));
				m_ColorData.Add(new Vector3I(0), blockcolor);
				//p_grid.CubeBlocks.Add(whiteblock);
				//if (!_activeMode) Log.DebugWrite(DebugLevel.Info, cache.Count);
				foreach (KeyValuePair<Vector3D, IMySensorBlock> pl in cache)
				{
					Vector3I plloc = new Vector3I(WorldtoGrid(pl.Value.WorldMatrix.Translation, panelMatrixNormalizedInv));
					//Log.DebugWrite(DebugLevel.Info, plloc);
					var owner = pl.Value.OwnerId;
					blockcolor = ResultType.Enemy;
					if (owner == Term.OwnerId) blockcolor = ResultType.Self;
					else
					{
						var ownerF = MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner);
						var Tfac = MyAPIGateway.Session.Factions.TryGetPlayerFaction(Term.OwnerId);
						if (ownerF != null && Tfac != null)
						{
							if (ownerF == Tfac) blockcolor = ResultType.Faction;//same faction
							else
							{
								var relation = MyAPIGateway.Session.Factions.GetRelationBetweenFactions(ownerF.FactionId, Tfac.FactionId);
								if (relation == MyRelationsBetweenFactions.Neutral) blockcolor = ResultType.Friend;//allied
							}
						}
						else
							blockcolor = ResultType.Neutral;
					}
					if (!check.Contains(plloc))
					{
						check.Add(plloc);
						if (blockCache.ContainsKey(plloc)) blockCache.Remove(plloc);
						blockCache.Add(plloc, blockcolor);
						if (!absolute.Contains(plloc)) absolute.Add(plloc);
					}
					else
					{
						if (blockCache.ContainsKey(plloc)) blockCache.Remove(plloc);
						blockCache.Add(plloc, blockcolor);
						if (!absolute.Contains(plloc)) absolute.Add(plloc);
					}
				}
				BoundingSphereD sphere = new BoundingSphereD(panelWorld.Translation, 50 * Range * _resMult);
				//var ents = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
				List<MyEntity> ents = new List<MyEntity>();
				MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, ents);
				List<IMyEntity> planetcheck = new List<IMyEntity>(ents);
				if (_activeMode)
				{
					#region planetcache
					foreach (MyEntity ent in planetcheck)
					{
						if (ent is IMyVoxelBase)
						{
							var asteroid = (IMyVoxelBase)ent;
							if (asteroid.LocalAABB.Extents.Length() < Range * _resMult)
							{
							}
							else
								if (!voxels.Contains(asteroid)) voxels.Add(asteroid);
						}
					}
					if (voxels.Count > 0)
					{
						for (int xmin = -50; xmin <= 50; xmin++)
						{
							for (int ymin = -50; ymin <= 50; ymin++)
							{
								for (int zmin = -50; zmin < 50; zmin++)
								{
									Vector3I voxPos = new Vector3I(xmin, ymin, zmin);
									Vector3D world = new Vector3D(GridToWorld(voxPos, panelWorld));
									bool ex = excludeVoxelBase(world, ref voxels);
									if (!check.Contains(voxPos) && ex) check.Add(voxPos);
								}
							}
						}
						foreach (Vector3I vpos in check)
						{
							Vector3I px, py, pz, mx, my, mz;
							px = py = pz = mx = my = mz = vpos;
							px.X++;
							mx.X--;
							py.Y++;
							my.Y--;
							pz.Z++;
							mz.Z--;
							int found = 0;
							if (check.Contains(px)) found++;
							if (check.Contains(mx)) found++;
							if (check.Contains(py)) found++;
							if (check.Contains(my)) found++;
							if (check.Contains(pz)) found++;
							if (check.Contains(mz)) found++;
							if (found <= 5)
							{
								Vector3D world = GridToWorld(vpos, panelWorld);
								if (sphere.Contains(world) == ContainmentType.Contains)
									if (!blockCache.ContainsKey(vpos)) blockCache.Add(vpos, ResultType.Voxel);
							}
						}
					}
					#endregion
					#region entityscan
					Vector3I pos;
					foreach (MyEntity ent in ents)
					{
						bool small = false;
						//Log.DebugWrite(DebugLevel.Info, ent);
						if (!ent.Flags.HasFlag(EntityFlags.Save))
						{
							//ignore items that dont save.
							//Log.DebugWrite(DebugLevel.Info, ent);
							//Log.DebugWrite(DebugLevel.Info, "Has no save flag");
							continue;
						}
						if (ent.Parent != null)
							if (ent == Term.Parent) continue;
						Vector3D worldCenter = ent.WorldMatrix.Translation;
						Vector3D center = WorldtoGrid(worldCenter, panelMatrixNormalizedInv);
						if (ent is MyCubeBlock) continue;
						if (ent is IMySlimBlock) continue;
						blockcolor = ResultType.Unknown;
						if (ent is IMyVoxelBase)
						{
							blockcolor = ResultType.Voxel;//add
						//	continue;
						}
						if (ent is MyFloatingObject || ent is MyInventoryBagEntity)
						{
							small = true;
							blockcolor = ResultType.FloatingObject;
						}
						if (ent is IMyCharacter)
						{
							small = true;
							//var character = ent as IMyCharacter;
							//character.
							var playa = MyAPIGateway.Multiplayer.Players.GetPlayerControllingEntity(ent);
							if (playa == null) continue;
							if (playa.Controller == null) continue;
							if (playa.Controller.ControlledEntity == null) continue;
							if (playa.Controller.ControlledEntity.Entity is IMyCharacter) blockcolor = ResultType.Engineer;
						}
						if (ent is IMyMeteor)
						{
							small = true;
							blockcolor = ResultType.Meteor;
						}
						if (ent is IMyCubeGrid)
						{
							blockcolor = ResultType.Enemy;
							var grid = (IMyCubeGrid)ent;
							if (grid.EntityId == Entity.Parent.EntityId) continue;
							if (grid.BigOwners.Count > 0)
							{
								var idents = grid.BigOwners.GetInternalArray<long>();
								var ident = idents[0];
								if (ident == 0) blockcolor = ResultType.Neutral;
								if (ident == Term.OwnerId) blockcolor = ResultType.Self;
								else
								{
									var ownerF = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ident);
									var Tfac = MyAPIGateway.Session.Factions.TryGetPlayerFaction(Term.OwnerId);
									if (ownerF != null && Tfac != null)
									{
										if (ownerF == Tfac) blockcolor = ResultType.Faction;//same faction
										else
										{
											var relation = MyAPIGateway.Session.Factions.GetRelationBetweenFactions(ownerF.FactionId, Tfac.FactionId);
											if (relation == MyRelationsBetweenFactions.Neutral) blockcolor = ResultType.Friend;//allied
										}
									}
								}
							}
							else
								blockcolor = ResultType.Neutral;
							worldCenter = grid.WorldAABB.Center;
							center = WorldtoGrid(worldCenter, panelMatrixNormalizedInv);
							//disable for now
							//var attachedradar = CoreHolo.GetRadarAttachedToGrid(grid.EntityId);
							//foreach (Radar retRadar in attachedradar)
							//{
							//	Vector3D blockWorldCenter = retRadar.Term.WorldMatrix.Translation;
							//	Vector3D blockCenter = WorldtoGrid(blockWorldCenter, panelMatrixNormalizedInv);
							//	var centerPing = new Vector3I(blockCenter);
							//	if (!pingCache.ContainsKey(centerPing)) pingCache.Add(centerPing, retRadar.Term);
							//}
						}
						//if (blockcolor == Color.Gray.ColorToHSV())
						//{
						//	Log.DebugWrite( DebugLevel.Info,"Unknown object");
						//	Log.DebugWrite(DebugLevel.Info, ent);
						//}
						pos = new Vector3I(center);
						if (!check.Contains(pos) && sphere.Contains(worldCenter) == ContainmentType.Contains)
						{
							if (!small) check.Add(pos);
							if (!blockCache.ContainsKey(pos)) blockCache.Add(pos, blockcolor);
						}
					}
					#endregion
					#region shipdraw
					if (CoreHolo.instance.settings.show_ship)
					{
						List<IMySlimBlock> children = new List<IMySlimBlock>();
						shipgrid.GetBlocks(children, delegate (IMySlimBlock a)
						{
							Vector3I plloc = new Vector3I(WorldtoGrid(shipgrid.GridIntegerToWorld(a.Position), panelMatrixNormalizedInv));
							if (check.Contains(plloc)) return false;
							if (blockCache.ContainsKey(plloc)) return false;
							if (absolute.Contains(plloc)) return false;
							absolute.Add(plloc);
							blockCache.Add(plloc, ResultType.Self_Point_Alt);
							return false;
						});
					}
					#endregion
				}
				check.Remove(Vector3I.Zero);
				int cnt = 0;
				int mod = m_rand.Next(CoreHolo.instance.settings.vox_cnt);
				int hits = 0;
				foreach (KeyValuePair<Vector3I, ResultType> kpair in blockCache)
				{
					cnt++;
					bool skip = false;
					Vector3I spos = kpair.Key;
					if (spos == Vector3I.Zero) continue;
					Vector3D trail = new Vector3D(spos);
					double dist = trail.Length();
					double x = 0.5, y = 0.5, z = 0.5;
					if (!absolute.Contains(spos))
					{
						for (int step = 0; step < dist; step++)
						{
							x += trail.X / dist;
							y += trail.Y / dist;
							z += trail.Z / dist;
							Vector3I checkPos = new Vector3I((int)x, (int)y, (int)z);
							Vector3I checkPosXp = new Vector3I((int)x + 1, (int)y, (int)z);
							Vector3I checkPosXm = new Vector3I((int)x - 1, (int)y, (int)z);
							Vector3I checkPosYp = new Vector3I((int)x, (int)y + 1, (int)z);
							Vector3I checkPosYm = new Vector3I((int)x, (int)y - 1, (int)z);
							Vector3I checkPosZp = new Vector3I((int)x, (int)y, (int)z + 1);
							Vector3I checkPosZm = new Vector3I((int)x, (int)y, (int)z - 1);
							if (check.Contains(checkPos))
							{
								if (checkPos != spos)
								{
									if (x > 0)
										if (!check.Contains(checkPosXp)) break;
									if (x < 0)
										if (!check.Contains(checkPosXm)) break;
									if (y > 0)
										if (!check.Contains(checkPosYp)) break;
									if (y < 0)
										if (!check.Contains(checkPosYm)) break;
									if (z > 0)
										if (!check.Contains(checkPosZp)) break;
									if (z < 0)
										if (!check.Contains(checkPosZm)) break;
									skip = true;
								}
								break;
							}
						}
						if (skip) continue;
					}
					blockcolor = kpair.Value;
					if (blockcolor == ResultType.Voxel)
					{
						hits++;
						if (hits > 10)
							if (cnt % CoreHolo.instance.settings.vox_cnt != mod) continue;
					}
					IMySensorBlock result;
					if (pingCache.TryGetValue(spos, out result)) pingResult.Add(spos, result);
					/*MyObjectBuilder_CubeBlock bl = new MyObjectBuilder_CubeBlock()
					{
						EntityId = NewEntityId++,
						SubtypeName = "SC_RadarBlip",
						Min = spos,
						ColorMaskHSV = RadarResult.getColor(blockcolor).ColorToHSVDX11(),
						BlockOrientation = new SerializableBlockOrientation(Base6Directions.Direction.Forward, Base6Directions.Direction.Up)
					};*/
					//if(!p_grid.CubeBlocks.Contains(bl)) p_grid.CubeBlocks.Add(bl);
					if (!m_ColorData.ContainsKey(spos)) m_ColorData.Add(spos, blockcolor);
				}
				//_result.AddCompleteGrid(p_grid);
				T_result.ColorData = m_ColorData;
			}
			catch (Exception ex)
			{
				//MyAPIGateway.Utilities.ShowMessage("Error", ex.ToString());
				//Log.DebugWrite(DebugLevel.Error, ex);
			}
		}
		private void Notify(Vector3D vec, IMySensorBlock value)
		{
			if (!notifyCache.ContainsKey(vec)) notifyCache.Add(vec, value);
		}
		private bool excludeVoxelBase(Vector3D center, ref HashSet<IMyVoxelBase> asteroids)
		{
			MyStorageData cache = new MyStorageData();
			cache.Resize(Vector3I.One);
			Vector3I voxelCoord;
			foreach (IMyVoxelBase asteroid in asteroids)
			{
				if (asteroid.WorldAABB.Contains(center) != ContainmentType.Contains) continue;
				MyVoxelCoordSystems.WorldPositionToVoxelCoord(asteroid.PositionLeftBottomCorner, ref center, out voxelCoord);
				asteroid.Storage.ReadRange(cache, MyStorageDataTypeFlags.Content, 0, voxelCoord, voxelCoord);
				if (cache.Content(ref Vector3I.Zero) != (byte)0) return true;
			}
			return false;
		}
		public override void Close()
		{
			CoreHolo.UnRegister(Entity.EntityId);
		}
	}
}
