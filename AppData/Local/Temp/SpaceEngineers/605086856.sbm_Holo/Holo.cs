using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRageMath;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using Sandbox.Definitions;
using ParallelTasks;
using System.Text.RegularExpressions;
using System.Globalization;
using VRage.Game;
using VRage.Game.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Utils;
namespace Hologram
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_TextPanel), true)]
	class Holo : MyGameLogicComponent
	{
		private MyObjectBuilder_EntityBase objectBuilder;
		private HashSet<IMyEntity> deletecache = new HashSet<IMyEntity>();
		private IMyTextPanel panel;
		private string privateradarid = "";
		private string publicradarid = "";
		private bool valid = false;
		private bool readyPing = false;
		private Task task;
		private double m_scale = 0.05;
		private double m_adj_u = 0d;
		private double m_adj_d = 0d;
		private double m_adj_l = 0d;
		private double m_adj_r = 0d;
		private double m_adj_f = 0d;
		private double m_adj_b = 0d;
		private bool initDirty = true;
		private bool updateHook = false;
		//private Dictionary<RadarResult.SweepLocation, IMyEntity> projections = new Dictionary<RadarResult.SweepLocation, IMyEntity>();
		private Dictionary<Vector3I, ResultType> color = new Dictionary<Vector3I, ResultType>();
		HoloSetting PrivateAdjust = new HoloSetting();
		HoloSetting PublicAdjust = new HoloSetting();
		HoloSetting ActiveAdjust = new HoloSetting();
		IMyGridTerminalSystem system;
		//RadarResult.SweepLocation currentLocation = 0;
		private double Scale
		{
			get { return m_scale * ActiveAdjust.S; }
			set
			{
				if (m_scale > 0.001) m_scale = value;
			}
		}
		private int lastid = 65555;
		private long lastEntity = 0;
		private string lastTitle = "";
		private int sweepcount = 0;
		public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
		{
			return copy ? (MyObjectBuilder_EntityBase)objectBuilder.Clone() : objectBuilder;
		}
		public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{
			this.objectBuilder = objectBuilder;
			Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
		}
		public override void UpdateOnceBeforeFrame()
		{
			try
			{
				panel = (IMyTextPanel)Entity;
				initBlockSettings();
				if (panel.BlockDefinition.SubtypeName.EndsWith("_DS_HOLO") || valid)
				{
					valid = true;
					Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;//update the block.
					CreateTerminalControls<IMyTextPanel>();
					updateHook = true;
					CoreHolo.UpdateHook += Update;//constrain its position
				}
			}
			catch (Exception ex)
			{
				Log.DebugWrite(DebugLevel.Error, ex);
			}
		}
		protected static List<Type> m_ControlsInited = new List<Type>();
		protected static IMyTerminalControlSeparator Seperator;
		//protected static IMyTerminalControlCheckbox YawCheck, PitchCheck, RollCheck, InvertCheck, InvertRollCheck;
		protected static IMyTerminalControlSlider RangeControl;
		//protected static IMyTerminalControlButton SaveButton;
		protected void CreateTerminalControls<T>()
		{
			if (m_ControlsInited.Contains(typeof(T))) return;
			m_ControlsInited.Add(typeof(T));
			//if (Seperator == null)
			//{
			//	Seperator = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyTerminalBlock>(string.Empty);
			//	Seperator.Visible = (b) => b.IsHoloTable();
			//}
			//MyAPIGateway.TerminalControls.AddControl<T>(Seperator);
			//if (RangeControl == null)
			//{
			//	RangeControl = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyTerminalBlock>("Draygo.ControlSurface.Trim");
			//	RangeControl.Visible = (b) => b.IsHoloTable();
			//	RangeControl.Enabled = (b) => b.IsHoloTable() && b.IsWorking;
			//	RangeControl.Getter = (b) => b.GameLogic.GetAs<Holo>().Control.Trim;
			//	RangeControl.Writer = (b, v) => v.Append(string.Format("{0:N1} {1}", b.GameLogic.GetAs<Holo>().Control.Trim, MyStringId.GetOrCompute("Degrees")));
			//	RangeControl.Setter = (b, v) => b.GameLogic.GetAs<Holo>().Control.TrimSet(v);
			//	RangeControl.Title = MyStringId.GetOrCompute("Range");
			//	RangeControl.Tooltip = MyStringId.GetOrCompute("Range in KM");
			//	RangeControl.SupportsMultipleBlocks = true;
			//	RangeControl.SetLimits(0.0f, 50.0f);
			//}
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
			//var TrimProperty = MyAPIGateway.TerminalControls.CreateProperty<float, T>("Range");
			//if (TrimProperty != null)
			//{
			//	TrimProperty.Enabled = (b) => b.IsHoloTable() && b.IsWorking;
			//	TrimProperty.Getter = (b) => b.GameLogic.GetAs<Holo>().Control.Trim;
			//	TrimProperty.Setter = (b, v) => b.GameLogic.GetAs<Holo>().Control.TrimSet(v);
			//	MyAPIGateway.TerminalControls.AddControl<T>(TrimProperty);
			//}
		}
		internal static void RedrawControls()
		{
			if (RangeControl != null) RangeControl.UpdateVisual();
		}
		private void initBlockSettings()
		{
			try
			{
				MyCubeBlockDefinition blockDefinition = null;
				if (MyDefinitionManager.Static.TryGetCubeBlockDefinition(panel.BlockDefinition, out blockDefinition))
				{
					var descriptionStr = blockDefinition.DescriptionString;
					initDirty = false;
					Regex reg = new Regex("holo{(.*?)}");
					Regex regcom = new Regex(";");
					Regex regeq = new Regex("=");
					if (descriptionStr == null || descriptionStr.Length == 0) return;
					var res = reg.Split(descriptionStr);
					if (res.Length > 1)
					{
						var search = regcom.Split(res[1]);
						if (search == null) return;
						foreach (string parts in search)
						{
							var dataeq = regeq.Split(parts);
							if (dataeq.Length == 0) continue;
							switch (dataeq[0].ToLower())
							{
								case "holo":
									if (dataeq[1].ToLowerInvariant() == "y") valid = true;
									if (dataeq[1].ToLowerInvariant() == "yes") valid = true;
									if (dataeq[1].ToLowerInvariant() == "t") valid = true;
									if (dataeq[1].ToLowerInvariant() == "tru") valid = true;
									if (dataeq[1].ToLowerInvariant() == "true") valid = true;
									break;
								case "u":
								case "up":
								case "offsetup":
									m_adj_u = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
									break;
								case "d":
								case "down":
								case "offsetdown":
									m_adj_d = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
									break;
								case "l":
								case "left":
								case "offsetleft":
									m_adj_l = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
									break;
								case "r":
								case "right":
								case "offsetright":
									m_adj_r = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
									break;
								case "f":
								case "forward":
								case "offsetforward":
									m_adj_f = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
									break;
								case "b":
								case "backward":
								case "offsetback":
								case "offsetbackward":
									m_adj_b = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
									break;
								case "scale":
									Scale = Convert.ToDouble(dataeq[1], new CultureInfo("en-US")) * 0.05d;
									break;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				initDirty = false;
				Scale = 0.05;
				m_adj_r = 0;
				m_adj_l = 0;
				m_adj_d = 0;
				m_adj_u = 0;
				m_adj_f = 0;
				m_adj_b = 0;
				Log.DebugWrite(DebugLevel.Error, "ERROR in definition, could not init check definition description for correct format. " + ex.ToString());
			}
		}
		public void Update()
		{
			if (Entity == null || Entity.Closed || Entity.MarkedForClose)
			{
				if (updateHook)
				{
					updateHook = false;
					if (CoreHolo.UpdateHook != null) CoreHolo.UpdateHook -= Update;
					return;
				}
			}
			if (!valid) return;
			if (initDirty) initBlockSettings();
			if (CoreHolo.instance == null) return;
			if (CoreHolo.instance.isDedicated)
			{
				//Entity.NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;
				if (updateHook)
				{
					updateHook = false;
					if (CoreHolo.UpdateHook != null) CoreHolo.UpdateHook -= Update;
					return;
				}
				return;
			}
			if (panel.IsWorking) moveProjection();
		}
		private void getRadarValues()
		{
			var privatetitle = panel.CustomData;
			var publictitle = panel.GetPublicTitle();
			try
			{
				HoloSetting PlayerAdjust;
				privateradarid = parseSettingsString(privatetitle, out PlayerAdjust);
				PrivateAdjust = PlayerAdjust;
				publicradarid = parseSettingsString(publictitle, out PlayerAdjust);
				PublicAdjust = PlayerAdjust;
			}
			catch
			{
				//do nothing.
			}
		}
		private string parseSettingsString(string title, out HoloSetting Adjust)
		{
			//Log.DebugWrite(DebugLevel.Info, "parseSettingsString");
			HoloSetting adj = new HoloSetting();
			Regex reg = new Regex("(.*?)\\[(.*?)\\]");
			Regex regcom = new Regex(";");
			Regex regeq = new Regex("=");
			string foundtitle = "";
			Adjust = adj;
			if (title == null || title.Length == 0) return foundtitle;
			var res = reg.Split(title);
			//Log.DebugWrite(DebugLevel.Info, res.Length);
			if (res.Length > 2)
			{
				foreach (var word in res)
				{
					Log.DebugWrite(DebugLevel.Info, word);
				}
				foundtitle = res[1];
				//Log.DebugWrite(DebugLevel.Info, "settings:" + res[2]);
				var search = regcom.Split(res[2]);
				if (search == null) return foundtitle.ToLowerInvariant().Trim();
				foreach (string parts in search)
				{
					var dataeq = regeq.Split(parts);
					if (dataeq.Length == 0) continue;
					try
					{
						switch (dataeq[0].ToLower())
						{
							case "u":
							case "up":
							case "offsetup":
								Adjust.U = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
								break;
							case "d":
							case "down":
							case "offsetdown":
								Adjust.D = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
								break;
							case "l":
							case "left":
							case "offsetleft":
								Adjust.L = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
								break;
							case "r":
							case "right":
							case "offsetright":
								Adjust.R = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
								break;
							case "f":
							case "forward":
							case "offsetforward":
								Adjust.F = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
								break;
							case "b":
							case "backward":
							case "offsetbackward":
								Adjust.B = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
								break;
							case "s":
							case "scale":
								Adjust.S = Convert.ToDouble(dataeq[1], new CultureInfo("en-US"));
								break;
						}
					}
					catch
					{
						//catching stupid.
					}
				}
			}
			else
				if (res.Length >= 1) foundtitle = res[0];
			return foundtitle.ToLowerInvariant().Trim();
		}
		public override void UpdateBeforeSimulation10()
		{
			if (Entity == null) return;
			if (panel == null) return;
			if (CoreHolo.instance == null) return;
			if (!valid) return;
			if (CoreHolo.instance.isDedicated)
			{
				//cleanProjections();
				return;
			}
			if (!panel.IsWorking)
			{
				//cleanProjections();
				return;
			}
			if (!CoreHolo.ClosetoPlayer(Entity.WorldMatrix.Translation, 30))
			{
				//cleanProjections();
				return;
			}
			long ent = 0;
			//refresh
			system = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid((IMyCubeGrid)panel.CubeGrid);
			List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
			system.GetBlocksOfType<IMySensorBlock>(blocks, null);
			getRadarValues();
			foreach (var block in blocks)
			{
				var sens = (IMySensorBlock)block;
				if (sens.OwnerId == panel.OwnerId)
				{
					if (sens.CustomName.ToLowerInvariant().Trim() == privateradarid)
					{
						ent = sens.EntityId;
						ActiveAdjust = PrivateAdjust;
						break;
					}
					if (sens.CustomName.ToLowerInvariant().Trim() == publicradarid)
					{
						//allow public title to get the entity id as well.
						ActiveAdjust = PublicAdjust;
						ent = sens.EntityId;
					}
				}
			}
			Log.DebugWrite(DebugLevel.Verbose, "Entityid " + ent.ToString());
			if (ent != 0)
			{
				var data = CoreHolo.GetRadar(ent);
				//MyAPIGateway.Utilities.ShowMessage("Radar?", (data?.RadarData == null).ToString());
				if (data != null && data.RadarData != null)
				{
					color = data.RadarData.ColorData;
					//MyAPIGateway.Utilities.ShowMessage("Count", color.Count.ToString());
				}
				else
					cleanDraws();
			}
			else
				cleanDraws();
		}
		private Vector3D adjustvector(Vector3D vec, MatrixD worldMatrix)
		{
			if (m_adj_f + ActiveAdjust.F != 0) vec = vec + Vector3D.Multiply(worldMatrix.Forward, m_adj_f + ActiveAdjust.F);
			if (m_adj_b + ActiveAdjust.B != 0) vec = vec + -Vector3D.Multiply(worldMatrix.Forward, m_adj_b + ActiveAdjust.B);
			if (m_adj_u + ActiveAdjust.U != 0) vec = vec + Vector3D.Multiply(worldMatrix.Up, m_adj_u + ActiveAdjust.U);
			if (m_adj_d + ActiveAdjust.D != 0) vec = vec + -Vector3D.Multiply(worldMatrix.Up, m_adj_d + ActiveAdjust.D);
			if (m_adj_l + ActiveAdjust.L != 0) vec = vec + Vector3D.Multiply(worldMatrix.Left, m_adj_l + ActiveAdjust.L);
			if (m_adj_r + ActiveAdjust.R != 0) vec = vec + -Vector3D.Multiply(worldMatrix.Left, m_adj_r + ActiveAdjust.R);
			return vec;
		}
		private void moveProjection()
		{
			Log.DebugWrite(DebugLevel.Verbose, "MP getting scales: {0}" + Scale.ToString());
			//if (scale == 0.5d) initBlockSettings();//try again!?
			advDraw();
		}
		private void cleanDraws()
		{
			color.Clear();
		}
		private void advDraw()
		{
			if (MyAPIGateway.Session == null) return;
			if (MyAPIGateway.Session.Player == null) return;
			MatrixD mat = MatrixD.CreateWorld(adjustvector(panel.WorldMatrix.Translation, panel.WorldMatrix), new Vector3(0, 0, 1), new Vector3(0, 1, 0));
			Vector3D trans = mat.Translation;
			BoundingBoxD bb = new BoundingBoxD(new Vector3D(0), new Vector3D(Scale / 2));
			bb = new BoundingBoxD(-bb.Center, bb.Center);
			SortedSet<PointStruct> set = new SortedSet<PointStruct>(new PointComparer());
			Vector3D playerpos = MyAPIGateway.Session.Player.GetPosition();
			foreach (KeyValuePair<Vector3I, ResultType> kvp in color)
			{
				PointStruct point;
				point.Value = trans + ((Vector3D)kvp.Key * (Scale / 2));
				point.Key = Vector3D.Distance(point.Value, playerpos);
				point.Color = RadarResult.getColor(kvp.Value);
				set.Add(point);
			}
			//MyAPIGateway.Utilities.ShowMessage("Draw Call", color.Count.ToString());
			foreach (var value in set.Reverse())
			{
				mat.Translation = value.Value;
				Color _color = value.Color;
				MySimpleObjectDraw.DrawTransparentBox(ref mat, ref bb, ref _color, MySimpleObjectRasterizer.Solid, 0, 0, null, null, false, -1);
			}
		}
		public override void Close()
		{
			//cleanProjections();
			if (updateHook)
			{
				updateHook = false;
				if (CoreHolo.UpdateHook != null) CoreHolo.UpdateHook -= Update;
			}
		}
	}
	struct PointStruct
	{
		internal const double increment = 0.001d;
		internal Color Color;
		internal double Key;
		internal Vector3D Value;
	}
	class PointComparer : Comparer<PointStruct>
	{
		public override int Compare(PointStruct x, PointStruct y)
		{
			int retVal = 0;
			retVal = Comparer<double>.Default.Compare(x.Key, y.Key);
			while (retVal == 0)
			{
				y.Key += PointStruct.increment;
				retVal = Comparer<double>.Default.Compare(x.Key, y.Key);
			}
			return retVal;
		}
	}
}
