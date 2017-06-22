using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using Draygo.API;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
using VRage.Utils;

namespace DockingAssist
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_ShipConnector), true)]
    public class ConnectorAssist : MyGameLogicComponent
    {
		private MyObjectBuilder_EntityBase objectBuilder;
		IMyShipConnector connector;
		static Dictionary<Vector3I, EntityCache> BlockCache = new Dictionary<Vector3I, EntityCache>();

		static double scale = 100;
		static bool isdirty = false;
		static IMyShipConnector dirtyblock;
		IMyFunctionalBlock target;
		Vector3I lastpos = new Vector3I(0, 0, 0);
		bool alive = true;
		bool updating = false;
		//HUDTextNI.EntityMessage EntMsg;
		int index = 0;


		public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{
			this.objectBuilder = objectBuilder;

			

			this.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
			connector = Entity as IMyShipConnector;


		}


		public override void UpdateBeforeSimulation100()
		{
			if (DockCore.instance?.TextAPI == null)
				return;
			if (DockCore.instance.isDedicated)
				return;
			//not init
			if (connector == null || connector.MarkedForClose || connector.Closed)
				return;
			if (dirtyblock == null || dirtyblock.MarkedForClose || dirtyblock.Closed)
				isdirty = false;
			if (MyAPIGateway.Session?.Player?.Controller?.ControlledEntity == null)
				return;
			if (!alive)
				return;
			if (updating)
			{
				DockCore.OnDraw -= Draw;
				updating = false;
			}
			if (MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity.GetTopMostParent() == connector.GetTopMostParent())
			{
				index = DockCore.instance.indexer.Add((connector));

			}
			if (!connector.IsWorking)
				return;
			//add to cache
			string debug = "";
			Vector3D Coordinate = connector.WorldMatrix.Translation / scale;
			if (Coordinate.AbsMax() >= Vector3I.MaxValue.AbsMax())
			{
				scale = (Coordinate.AbsMax() * 2) / Vector3I.MaxValue.AbsMax();
				isdirty = true;
				dirtyblock = connector;
				BlockCache.Clear(); //BOOM
									//return;
			}
			debug += scale.ToString() + '\n';
			EntityCache Cache;
			BlockCache.TryGetValue(lastpos, out Cache);
			if (Cache != null)
			{
				Cache.Remove(connector);
			}

			lastpos = new Vector3I(Coordinate);
			EntityCache Search = new EntityCache();
			BlockCache.TryGetValue(lastpos, out Cache);
			if (Cache != null)
			{
				Search.Copy(Cache);
				Cache.Add(connector);

			}
			else
			{
				Cache = new EntityCache();
				Cache.Add(connector);
				BlockCache.Add(lastpos, Cache);
			}
			IMyFunctionalBlock Closest;
			if (TryGetClosestMergeblock(connector, ref Search, out Closest))
			{
				debug += Closest.EntityId.ToString() + '\n';
				if (MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity.GetTopMostParent() == connector.GetTopMostParent())
				{
					DockCore.instance.CanDraw = true;
					if (DockCore.instance.idx % DockCore.instance.indexer.Count() == index)
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
			if (connector == null)
				return;
			if (MyAPIGateway.Session?.Player?.Controller?.ControlledEntity?.Entity == null)
				return;
			if (MyAPIGateway.Session.Player.Controller.ControlledEntity.Entity.GetTopMostParent() != connector.GetTopMostParent())
				return;
			if (DockCore.instance.DrawHud != true) return;

			DockCore.instance.DrawCenterDot();//draw the center dot
			MatrixD mergepoint = new MatrixD(connector.WorldMatrix);

			mergepoint.Translation = target.WorldMatrix.Translation + (MathHelper.Clamp(Vector3D.Distance(connector.WorldMatrix.Translation, target.WorldMatrix.Translation), 5d, double.MaxValue) * target.WorldMatrix.Forward);
			MyTransparentGeometry.AddPointBillboard(DockCore.WHITEDOT, Color.Green.ToVector4(), connector.WorldMatrix.Translation + (connector.WorldMatrix.Forward * connector.CubeGrid.GridSize / 2), 1, 0.0f);

			Vector2D dotpos = DockCore.instance.DrawOtherDot(CorrectRot(mergepoint), target.WorldAABB.Center, DockCore.DotObject.Rotate);

			DockCore.instance.SetDistanceMessage(string.Format("    <color=teal>Distance: {0:N}", Vector3D.Distance(target.WorldMatrix.Translation, connector.WorldMatrix.Translation)));
			
			MatrixD targetpoint = new MatrixD(target.WorldMatrix);
			targetpoint.Translation += targetpoint.Forward * connector.CubeGrid.GridSize / 2;
			MyTransparentGeometry.AddPointBillboard(DockCore.WHITEDOT, Color.Purple.ToVector4(), targetpoint.Translation, 1, 0.0f);

			Vector2D anglepos = DockCore.instance.DrawOtherDot(CorrectRot(targetpoint), connector.WorldAABB.Center, DockCore.DotObject.Translate);
			//var dot = Vector3D.Dot(-target.WorldMatrix.Right, connector.WorldMatrix.Right) - 1;

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
			//DockCore.instance.TextAPI.Send(new HUDTextNI.HUDMessage(4, 20, new Vector2D(-0.4, -0.4),1,true, true, Color.Black, string.Format("{0}<color=teal>", instruction)));
		}

		private MatrixD CorrectRot(MatrixD _Matrix)
		{
			var Forward = _Matrix.Forward;
			var Right = _Matrix.Right;
			var Up = _Matrix.Up;
			_Matrix.Right = -Up;
			_Matrix.Up = -Right;
			return _Matrix;
		}

		private bool TryGetClosestMergeblock(IMyShipConnector connector, ref EntityCache search, out IMyFunctionalBlock closest)
		{
			int x = 0;
			int y = 0;
			int z = 0;
			for (x = -1; x < 2; x++)
			{
				for (y = -1; y < 2; y++)
				{
					for (z = -1; z < 2; z++)
					{
						if (x == 0 && y == 0 && z == 0)
							continue;
						EntityCache Cache;
						BlockCache.TryGetValue(lastpos + new Vector3I(x, y, z), out Cache);
						if (Cache != null)
						{
							search.Copy(Cache);
						}
					}
				}
			}
			double dist = double.MaxValue;
			closest = null;
			foreach (var block in search)
			{
				if (block == null || block.MarkedForClose || block.Closed)
					continue;

				if (block.CubeGrid == connector.CubeGrid)
					continue;
				if (Vector3D.Distance(block.WorldMatrix.Translation, connector.WorldMatrix.Translation) < dist)
				{
					dist = Vector3D.Distance(block.WorldMatrix.Translation, connector.WorldMatrix.Translation);
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
				Cache.Remove(connector);
			}
			alive = false;
			if (DockCore.instance.indexer.Contains(connector)) 
				DockCore.instance.indexer.Remove(connector);
			base.MarkForClose();
		}
		public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
		{
			return copy ? (MyObjectBuilder_EntityBase)objectBuilder.Clone() : objectBuilder;
		}
	}
}