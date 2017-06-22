using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRageMath;
using VRage.Game.ModAPI;
using System.Text.RegularExpressions;
using VRage.Game.ModAPI.Interfaces;
using VRage.Utils;
namespace Hologram
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_Cockpit), true)]
	class CockpitHUD : MyGameLogicComponent
	{
		private MyObjectBuilder_EntityBase objectBuilder;
		private bool updateHook = false;
		IMyCockpit cockpit;
		private Dictionary<Vector3I, ResultType> color = new Dictionary<Vector3I, ResultType>();
		int counter = 0;
		bool controlled = false;
		private IMyGridTerminalSystem system;
		private long ent;
		private string radarname;
		Radar m_radar;
		public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
		{
			return copy ? (MyObjectBuilder_EntityBase)objectBuilder.Clone() : objectBuilder;
		}
		public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{
			this.objectBuilder = objectBuilder;
			try
			{
				cockpit = (IMyCockpit)Entity;
				updateHook = true;
				CoreHolo.UpdateHook += Update;//constrain its position
			}
			catch (Exception ex)
			{
				Log.DebugWrite(DebugLevel.Error, ex);
			}
		}
		private void Update()
		{
			if (!CoreHolo.running) return;
			if (CoreHolo.instance.isDedicated)
			{
				unload();
				return;//do not run on dedicated servers.
			}
			if (MyAPIGateway.Session == null) return;
			if (MyAPIGateway.Session.ControlledObject == null) return;
			if (MyAPIGateway.Session.ControlledObject.Entity == null) return;
			if (MyAPIGateway.Session.ControlledObject.Entity.EntityId == Entity.EntityId)
			{
				if (!controlled) updateRadar();
				else
				{
					counter++;
					if (counter % 200 == 0) updateRadar();//get Radar block
				}
				controlled = true;
				if (m_radar != null)
					if (m_radar.RadarData != null)
						if (m_radar.RadarData.ColorData != null)
						{
							color = m_radar.RadarData.ColorData;
							advDraw();
						}
			}
			else
				controlled = false;
		}
		private void updateRadar()
		{
			m_radar = null;
			if (cockpit.CustomName == null) return;
			string title = cockpit.CustomName;
			Regex reg = new Regex("(.*?)!(.*)");
			if (title == null || title.Length == 0) return;
			var res = reg.Split(title);
			if (res.Length > 2)
			{
				foreach (var word in res)
				{
					Log.DebugWrite(DebugLevel.Info, word);
				}
				radarname = res[2].ToLowerInvariant().Trim();
			}
			else
				return;//nothing
			system = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid((IMyCubeGrid)cockpit.CubeGrid);
			List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
			system.GetBlocksOfType<IMySensorBlock>(blocks, null);
			//getRadarValues();
			foreach (var block in blocks)
			{
				var sens = (IMySensorBlock)block;
				if (sens.OwnerId == cockpit.OwnerId)
					if (sens.CustomName.ToLowerInvariant().Trim() == radarname)
					{
						ent = sens.EntityId;
						//ActiveAdjust = PrivateAdjust;
						break;
					}
			}
			Log.DebugWrite(DebugLevel.Verbose, "Entityid " + ent.ToString());
			if (ent != 0) m_radar = CoreHolo.GetRadar(ent);
		}
		private void advDraw()
		{
			MatrixD headmatrix = MyAPIGateway.Session.Player.Controller.ControlledEntity.GetHeadMatrix(true);
			Vector3D playerpos = headmatrix.Translation;
			MatrixD forwardtrans = new MatrixD(cockpit.WorldMatrix);
			Vector3D playerforward = forwardtrans.Forward;
			SortedSet<PointStruct> set = new SortedSet<PointStruct>(new PointComparer());
			foreach (KeyValuePair<Vector3I, ResultType> kvp in color)
			{
				if (kvp.Value == ResultType.Self_Point || kvp.Value == ResultType.Self_Point_Alt /*|| kvp.Value == ResultType.Voxel*/)
				{
				//	PointStruct point2;
				//	point2.Value = playerpos + ((Vector3D)kvp.Key * (0.01d / 4)) + cockpit.WorldMatrix.Forward * 0.25 + cockpit.WorldMatrix.Left * 0.1 + cockpit.WorldMatrix.Up * 0.025;
				//	point2.Key = Vector3D.Distance(point2.Value, playerpos);
				//	point2.Color = RadarResult.getColor(kvp.Value);
				//	set.Add(point2);
				//	point2.Value = playerpos + ((Vector3D)kvp.Key * (0.01d / 4)) + cockpit.WorldMatrix.Forward * 0.25 + cockpit.WorldMatrix.Right * 0.1 + cockpit.WorldMatrix.Up * 0.025;
				//	point2.Key = Vector3D.Distance(point2.Value, playerpos);
				//	point2.Color = RadarResult.getColor(kvp.Value);
				//	set.Add(point2);
					continue;
				}
				PointStruct point;
				//Vector3D dotvec = new Vector3D(kvp.Key);
				//if(Vector3D.Dot(playerforward, dotvec) > 0)
				//{
				//	point.Value = playerpos + Vector3D.Transform(((Vector3D)kvp.Key * (0.01d / 4)));
				//	point.Value = playerpos + ((Vector3D)kvp.Key * (0.01d / 4)) + cockpit.WorldMatrix.Forward * 0.25 + cockpit.WorldMatrix.Left * 0.1 + cockpit.WorldMatrix.Up * 0.025;
				//}
				//else
				//	point.Value = playerpos + ((Vector3D)kvp.Key * (0.01d / 4)) + cockpit.WorldMatrix.Forward * 0.25 + cockpit.WorldMatrix.Right * 0.1 + cockpit.WorldMatrix.Up * 0.025;
				point.Value = playerpos + ((Vector3D)kvp.Key * (0.025d / 4));
				point.Key = Vector3D.Distance(point.Value, playerpos);
				point.Color = RadarResult.getColor(kvp.Value);
				//if(kvp.Value != ResultType.Voxel)
				set.Add(point);
			}
			//var rev = set.Reverse();
			foreach (var value in set)
			{
				//mat.Translation = value.Value;
				Color _color = value.Color;
				_color.A = Convert.ToByte((int)(((double)Convert.ToInt32(_color.A)) / 2d));
				//float size = 0.0008f;
				MyTransparentGeometry.AddPointBillboard(MyStringId.GetOrCompute("WhiteDot"), _color.ToVector4(), value.Value, 0.0008f, 0.0f);
				//MySimpleObjectDraw.DrawTransparentSphere(ref mat, 0.0008f, ref _color, MySimpleObjectRasterizer.Solid, 8, "Square");
			}
		}
		private Vector3D Down(double v)
		{
			return Vector3D.Multiply(Entity.WorldMatrix.Down, v);
		}
		private Vector3D Up(double v)
		{
			return Vector3D.Multiply(Entity.WorldMatrix.Up, v);
		}
		public override void Close()
		{
			unload();
		}
		private void unload()
		{
			if (updateHook) CoreHolo.UpdateHook -= Update;
		}
	}
}
