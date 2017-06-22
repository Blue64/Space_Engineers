using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using VRage.Game.Entity;
using VRage.Voxels;
using Draygo.API;
using Sandbox.Game;
using Sandbox.Game.Entities.Blocks;
using System;
using Entities.Blocks;
using Sandbox.ModAPI.Interfaces.Terminal;
using System.Collections.Generic;
using Sandbox.Game.Gui;
using System.Text;
using System.Linq;
using Sandbox.Game.Localization;
using System.Globalization;
using System.Text.RegularExpressions;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Utils;

namespace DockingAssist
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_MergeBlock), true)]
    public class MergeAssist : MyGameLogicComponent
    {
        private MyObjectBuilder_EntityBase objectBuilder;
		IMyShipMergeBlock mergeblock;
		static Dictionary<Vector3I, EntityCache> BlockCache = new Dictionary<Vector3I, EntityCache>();
		static double scale = 100;
		static bool isdirty = false;
		static IMyShipMergeBlock dirtyblock;
		IMyFunctionalBlock target;
		Vector3I lastpos = new Vector3I(0, 0, 0);
		bool alive = true;
		bool updating = false;
		int index = 0;
		
		public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            this.objectBuilder = objectBuilder;

			this.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
			mergeblock = Entity as IMyShipMergeBlock;


        }




		public override void UpdateBeforeSimulation100()
        {
			if (DockCore.instance?.TextAPI == null)
				return;
			if (DockCore.instance.isDedicated)
				return;
			//not init
			if (mergeblock == null || mergeblock.MarkedForClose || mergeblock.Closed)
				return;
			if (dirtyblock == null || dirtyblock.MarkedForClose || dirtyblock.Closed)
				isdirty = false;
			if (MyAPIGateway.Session?.Player?.Controller?.ControlledEntity?.Entity == null)
				return;
			if (!alive)
				return;
			if (updating)
			{
				DockCore.OnDraw -= Draw;
				updating = false;
			}
			if (MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity.GetTopMostParent() == mergeblock.GetTopMostParent())
			{
				index = DockCore.instance.indexer.Add((mergeblock));
			
			}
			if (!mergeblock.IsWorking)
				return;
			//add to cache
			string debug = "";
			Vector3D Coordinate = mergeblock.WorldMatrix.Translation / scale;
			if ( Coordinate.AbsMax() >= Vector3I.MaxValue.AbsMax())
			{
				scale = (Coordinate.AbsMax() * 2) / Vector3I.MaxValue.AbsMax();
				isdirty = true;
				dirtyblock = mergeblock;
				BlockCache.Clear(); //BOOM
				//return;
            }
			debug += scale.ToString() + '\n';
			EntityCache Cache;
			BlockCache.TryGetValue(lastpos, out Cache);
			if(Cache != null)
			{
				Cache.Remove(mergeblock);
			}

			lastpos = new Vector3I(Coordinate);
			EntityCache Search = new EntityCache();
			BlockCache.TryGetValue(lastpos, out Cache);
			if (Cache != null)
			{
				Search.Copy(Cache);
				Cache.Add(mergeblock);

			}
			else
			{
				Cache = new EntityCache();
				Cache.Add(mergeblock);
				BlockCache.Add(lastpos, Cache);
			}
			IMyFunctionalBlock Closest;
			if(TryGetClosestMergeblock(mergeblock, ref Search, out Closest))
			{
				debug += Closest.EntityId.ToString() + '\n';
				if(MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity.GetTopMostParent() == mergeblock.GetTopMostParent() )
				{
					DockCore.instance.CanDraw = true;
					
					if(DockCore.instance.idx % DockCore.instance.indexer.Count() == index)
					{
						target = Closest;
						DockCore.OnDraw += Draw;
						updating = true;
					}

				}
			}
			debug += Search.Count().ToString();
			
		}

		private void Draw()
		{
			if (target == null)
				return;
			if (mergeblock == null)
				return;
			if (MyAPIGateway.Session?.Player?.Controller?.ControlledEntity?.Entity == null)
				return;
			if (MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity.GetTopMostParent() != mergeblock.GetTopMostParent())
				return;
			if (DockCore.instance.DrawHud != true) return;
				
			DockCore.instance.DrawCenterDot();//draw the center dot
			MatrixD mergepoint = new MatrixD(mergeblock.WorldMatrix);
			
			mergepoint.Translation = target.WorldMatrix.Translation + (MathHelper.Clamp(Vector3D.Distance(mergeblock.WorldMatrix.Translation, target.WorldMatrix.Translation), 5d, double.MaxValue) * target.WorldMatrix.Right);

			MyTransparentGeometry.AddPointBillboard(MyStringId.GetOrCompute("WhiteDot"), Color.Green.ToVector4(), mergeblock.WorldMatrix.Translation + mergeblock.WorldMatrix.Right * mergeblock.CubeGrid.GridSize / 2, 1, 0.0f);
			
			Vector2D dotpos = DockCore.instance.DrawOtherDot(CorrectRot(mergepoint), target.WorldAABB.Center, DockCore.DotObject.Rotate);
			
			MatrixD targetpoint = new MatrixD(target.WorldMatrix);
			targetpoint.Translation += targetpoint.Right * mergeblock.CubeGrid.GridSize / 2;
			MyTransparentGeometry.AddPointBillboard(MyStringId.GetOrCompute("WhiteDot"), Color.Purple.ToVector4(), targetpoint.Translation, 1, 0.0f);
			
			Vector2D anglepos = DockCore.instance.DrawOtherDot(CorrectRot(targetpoint), mergeblock.WorldAABB.Center, DockCore.DotObject.Translate);
			
			var roll = Vector3D.Dot(-target.WorldMatrix.Forward, mergeblock.WorldMatrix.Forward) - 1;
			roll *= -180;
			int i_roll = (int)roll;
			i_roll %= 90;
			if (i_roll > 45)
				i_roll = 90 - i_roll;
			DockCore.instance.SetDistanceMessage(string.Format("    <color=teal>Distance: {0:N}\n    <color=teal>Roll Alignment:{1:N0}", Vector3D.Distance(target.WorldMatrix.Translation, mergeblock.WorldMatrix.Translation), i_roll));
			if (dotpos.Length() > 0.05)
			{

				DockCore.instance.DrawArrow(dotpos);
				DockCore.instance.SetAngleMessage();

			}
			else
			{
				if (anglepos.Length() > 0.05)
				{
					DockCore.instance.DrawArrow(anglepos);
					DockCore.instance.SetTranslationMessage();

				}
				else
				{
					DockCore.instance.HideArrow();
					DockCore.instance.SetApproachMessage();

				}

			}
			//DockCore.instance.TextAPI.Send(new HUDTextNI.HUDMessage(4, 20, new Vector2D(-0.4,-0.4),1,true, true, Color.Black, string.Format("{0}<color=teal>Roll Alignment:{1:N0}", instruction, i_roll)));
		}

		private MatrixD CorrectRot(MatrixD _Matrix)
		{
			var Up = _Matrix.Up;
			var Forward = _Matrix.Forward;
			var Right = _Matrix.Right;
			_Matrix.Forward = Right;
			_Matrix.Up = Forward;
			_Matrix.Left = Up;
			return _Matrix;
		}

		private bool TryGetClosestMergeblock(IMyShipMergeBlock mergeblock, ref EntityCache search, out IMyFunctionalBlock closest)
		{
			int x = 0;
			int y = 0;
			int z = 0;
			for(x = -1; x < 2; x++)
			{
				for(y=-1; y < 2; y++)
				{
					for (z = -1; z < 2; z++)
					{
						if (x == 0 && y == 0 && z == 0)
							continue;
						EntityCache Cache;
						BlockCache.TryGetValue(lastpos + new Vector3I(x, y, z), out Cache);
						if(Cache != null)
						{
							search.Copy(Cache);
						}
					}
				}
			}
			double dist = double.MaxValue;
			closest = null;
			foreach(var block in search)
			{
				if (block == null || block.MarkedForClose || block.Closed)
					continue;
				
				if (block.CubeGrid == mergeblock.CubeGrid)
					continue;
				if(Vector3D.Distance(block.WorldMatrix.Translation, mergeblock.WorldMatrix.Translation) < dist)
				{
					dist = Vector3D.Distance(block.WorldMatrix.Translation, mergeblock.WorldMatrix.Translation);
                    closest = block;
				}
			}
			if (closest != null)
				return true;
			return false;
			
		}
		public override void MarkForClose()
		{
			if (DockCore.instance == null || DockCore.instance.isDedicated) return;
			EntityCache Cache;
			BlockCache.TryGetValue(lastpos, out Cache);
			if (Cache != null)
			{
				Cache.Remove(mergeblock);
			}
			alive = false;
			DockCore.instance.indexer.Remove(mergeblock);
			base.MarkForClose();
		}
		public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
		{
			return copy ? (MyObjectBuilder_EntityBase)objectBuilder.Clone() : objectBuilder;
		}
	}
}