using System.Collections.Generic;
using VRage.ModAPI;
using Sandbox.ModAPI;
using System;
using VRage.Game;
using VRage.Game.Components;
using VRageMath;
using VRage.Game.ModAPI;
using System.Collections.Concurrent;
using ParallelTasks;
using VRage.Library;
using Draygo.Utils;
using ProtoBuf;
namespace Hologram
{
	public struct VoxelCoordPoint : IEquatable<VoxelCoordPoint>
	{
		long X;
		long Y;
		long Z;
		public VoxelCoordPoint(Vector3D position) : this()
		{
			X = (long)position.X;
			Y = (long)position.Y;
			Z = (long)position.Z;
		}
		public override bool Equals(object other)
		{
			return other is VoxelCoordPoint ? Equals((VoxelCoordPoint)other) : false;
		}
		public bool Equals(VoxelCoordPoint other)
		{
			return other.X == X && other.Y == Y && other.Z == Z;
		}
		public override int GetHashCode()
		{
			return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
		}
	}
	[MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
	public class CoreHolo : MySessionComponentBase
	{
		public static CoreHolo instance;
		public bool init = false;
		public bool isServer = false;
		public bool isMultiplayer = false;
		public bool isDedicated = false;
		static Dictionary<long, Radar> cache = new Dictionary<long, Radar>();
		public static DebugLevel debug = DebugLevel.Info;
		internal static Action UpdateHook;
		public static bool running = false;
		private static HashSet<long> m_HoloRegistry = new HashSet<long>();
		public ColorSettings settings = new ColorSettings();
		public Dictionary<Vector3I, bool> VoxelCache = new Dictionary<Vector3I, bool>();
		private const string FILE = "colorsettings.xml";
		private const string MOD_NAME = "Holo";
		private const int MOD_VERSION = 2;
		internal ThreadManager TManager = null;
		internal static readonly Guid ModGuid = new Guid("ab11e586-9855-49bc-ade3-8ae820f16a8c");
		#region radar registry
		/// <summary>
		/// Add Radar to session registry
		/// </summary>
		/// <param name="ent">EntityID</param>
		/// <param name="radar">Radar block</param>
		public static void Register(long ent, Radar radar)
		{
			cache.Add(ent, radar);
		}
		/// <summary>
		/// Returns registered radar from EntityId, returns null if not found.
		/// </summary>
		/// <param name="EntityId">EntityId</param>
		/// <returns></returns>
		public static Radar GetRadar(long EntityId)
		{
			Radar rad;
			if (cache.TryGetValue(EntityId, out rad))
			{
				//Log.DebugWrite(DebugLevel.Info, "Found");
				return rad;
			}
			return null;
		}
		/// <summary>
		/// Gets a list of radar blocks attached to a grid.
		/// </summary>
		/// <param name="EntityId">Grid EntityId</param>
		/// <returns></returns>
		public static List<Radar> GetRadarAttachedToGrid(long EntityId)
		{
			List<Radar> retVal = new List<Radar>();
			if (cache == null) return retVal;
			foreach (KeyValuePair<long, Radar> kvp in cache)
			{
				if (kvp.Value.GridEntityID == EntityId) retVal.Add(kvp.Value);
			}
			return retVal;
		}
		/// <summary>
		/// Removes Radar from registry.
		/// </summary>
		/// <param name="EntityID"></param>
		public static void UnRegister(long EntityID)
		{
			if (cache == null) return;
			if (cache.ContainsKey(EntityID)) cache.Remove(EntityID);
		}
		#endregion
		#region colors
		/// <summary>
		/// Default Blue.
		/// </summary>
		public static Color Color_Self
		{
			get
			{
				if (instance == null) return Color.Blue;
				else
					return instance.settings.Color_Self;
			}
		}
		/// <summary>
		/// Default White
		/// </summary>
		public static Color Color_Self_Alt
		{
			get
			{
				if (instance == null) return Color.White;
				else
					return instance.settings.Color_Self_Alt;
			}
		}
		/// <summary>
		/// Default Cyan
		/// </summary>
		public static Color Color_Faction
		{
			get
			{
				if (instance == null) return Color.Cyan;
				else
					return instance.settings.Color_Faction;
			}
		}
		/// <summary>
		/// Default Red
		/// </summary>
		public static Color Color_Enemy
		{
			get
			{
				if (instance == null) return Color.Red;
				else
					return instance.settings.Color_Enemy;
			}
		}
		/// <summary>
		/// Default Yellow
		/// </summary>
		public static Color Color_FloatingObject
		{
			get
			{
				if (instance == null) return Color.Yellow;
				else
					return instance.settings.Color_FloatingObject;
			}
		}
		/// <summary>
		/// Default Black
		/// </summary>
		public static Color Color_Voxel
		{
			get
			{
				if (instance == null) return Color.Black;
				else
					return instance.settings.Color_Voxel;
			}
		}
		/// <summary>
		/// Default Brown
		/// </summary>
		public static Color Color_Meteor
		{
			get
			{
				if (instance == null) return Color.Brown;
				else
					return instance.settings.Color_Meteor;
			}
		}
		/// <summary>
		/// Default Brown
		/// </summary>
		public static Color Color_Unknown
		{
			get
			{
				if (instance == null) return Color.Gray;
				else
					return instance.settings.Color_Unknown;
			}
		}
		/// <summary>
		/// Default Purple
		/// </summary>
		public static Color Color_Engineer
		{
			get
			{
				if (instance == null) return Color.Purple;
				else
					return instance.settings.Color_Engineer;
			}
		}
		/// <summary>
		/// Default Orange
		/// </summary>
		public static Color Color_Neutral
		{
			get
			{
				if (instance == null) return Color.Orange;
				else
					return instance.settings.Color_Neutral;
			}
		}
		/// <summary>
		/// Default Green
		/// </summary>
		public static Color Color_Friend
		{
			get
			{
				if (instance == null) return Color.Green;
				else
					return instance.settings.Color_Friend;
			}
		}
		#endregion
		public static bool UseAdvDraw
		{
			get { return instance.settings.advDraw; }
			internal set { instance.settings.advDraw = value; }
		}
		/// <summary>
		/// Called every game update.
		/// </summary>
		public override void UpdateAfterSimulation()
		{
			Update();
		}
		/// <summary>
		/// Called when game shuts down, cleans up handlers.
		/// </summary>
		protected override void UnloadData()
		{
			unload();
		}
		/// <summary>
		/// Update method, called through UpdateAfterSimulation
		/// </summary>
		private void Update()
		{
			if (!init)
			{
				//if(instance == null) instance = this;
				if (MyAPIGateway.Session == null) return;
				if (MyAPIGateway.Multiplayer == null && MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE) return;
				Init();
			}
			if (MyAPIGateway.Session == null) unload();
			if (TManager != null) TManager.Update();
			if (UpdateHook != null) UpdateHook();//Update the position of radar blocks, some reason doing the work here syncs up with the game rendering. Who knew.
		}
		private static readonly ushort RADARMESSAGE = 20938;
		private static readonly ushort REQUESTMESSAGE = 20939;
		/// <summary>
		/// init method
		/// </summary>
		public void Init()
		{
			if (init) return;//script already initialized, abort.
			else
				init = true;
			instance = this;
			settings = new ColorSettings();
			running = true;
			Log.Info("Initialized");
			isMultiplayer = MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE;
			isServer = MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE || MyAPIGateway.Multiplayer.IsServer;
			isDedicated = (MyAPIGateway.Utilities.IsDedicated && isServer);
			MyAPIGateway.Multiplayer.RegisterMessageHandler(RADARMESSAGE, RangeUpdateHandler);
			if (isServer) MyAPIGateway.Multiplayer.RegisterMessageHandler(REQUESTMESSAGE, RequestHandler);
			TManager = new ThreadManager();
			if (isDedicated) return;
			MyAPIGateway.Utilities.MessageEntered += Utilities_MessageEntered;
			loadXML();
		}
		[ProtoContract]
		public struct RangeUpdate
		{
			[ProtoMember]
			public long EntityId;
			[ProtoMember]
			public float Range;
			public RangeUpdate(long EntId, float R)
			{
				EntityId = EntId;
				Range = R;
			}
		}
		internal void SendUpdate(long entityId, float range)
		{
			if (isMultiplayer)
			{
				RangeUpdate UpdatePacket = new RangeUpdate(entityId, range);
				MyAPIGateway.Multiplayer.SendMessageToOthers(RADARMESSAGE, MyAPIGateway.Utilities.SerializeToBinary(UpdatePacket));
			}
		}
		private void RangeUpdateHandler(byte[] obj)
		{
			try
			{
				RangeUpdate UpdatePacket = MyAPIGateway.Utilities.SerializeFromBinary<RangeUpdate>(obj);
				GetRadar(UpdatePacket.EntityId)?.UpdateRange(UpdatePacket.Range);
			}
			catch
			{
				//nothing
			}
		}
		[ProtoContract]
		public struct RequestUpdate
		{
			[ProtoMember]
			public long EntityId;
			public RequestUpdate(long EntId)
			{
				EntityId = EntId;
			}
		}
		internal static void RequestSetting(long entityId)
		{
			RequestUpdate UpdateRequestPacket = new RequestUpdate(entityId);
			MyAPIGateway.Multiplayer.SendMessageToOthers(RADARMESSAGE, MyAPIGateway.Utilities.SerializeToBinary(UpdateRequestPacket));
		}
		private void RequestHandler(byte[] obj)
		{
			try
			{
				RequestUpdate UpdatePacket = MyAPIGateway.Utilities.SerializeFromBinary<RequestUpdate>(obj);
				Radar rad = GetRadar(UpdatePacket.EntityId);
				if (rad != null)
				{
					RangeUpdate Update = new RangeUpdate(UpdatePacket.EntityId, rad.Range);
					MyAPIGateway.Multiplayer.SendMessageToOthers(RADARMESSAGE, MyAPIGateway.Utilities.SerializeToBinary(UpdatePacket));
				}
			}
			catch
			{
				//failed to sync with server
			}
		}
		/// <summary>
		/// Chat command handler
		/// </summary>
		/// <param name="msg">message text</param>
		/// <param name="visible">hides the message if set to true</param>
		private void Utilities_MessageEntered(string msg, ref bool visible)
		{
			if (msg.Equals("/holo", StringComparison.InvariantCultureIgnoreCase))
			{
				MyAPIGateway.Utilities.ShowMessage(MOD_NAME, "Valid Commands: /holo-color /holo-vox /holo-show");
				visible = false;
			}
			if (!msg.StartsWith("/holo-", StringComparison.InvariantCultureIgnoreCase)) return;
			if (msg.StartsWith("/holo-vox", StringComparison.InvariantCultureIgnoreCase))
			{
				visible = false;
				string[] words = msg.Split(' ');
				if (words.Length > 1)
				{
					try
					{
						instance.settings.vox_cnt = Convert.ToInt32(words[1]);
						MyAPIGateway.Utilities.ShowMessage(MOD_NAME, string.Format("Voxel display set to 1 per {0}.", instance.settings.vox_cnt));
						saveXML();
					}
					catch
					{
						MyAPIGateway.Utilities.ShowMessage(MOD_NAME, "Invalid input, /holo-vox [#]");
					}
				}
				else
					MyAPIGateway.Utilities.ShowMessage(MOD_NAME, "/holo-vox [#]");
				return;
			}
			if (msg.StartsWith("/holo-show", StringComparison.InvariantCultureIgnoreCase))
			{
				visible = false;
				if (instance == null) return;
				instance.settings.show_ship = !instance.settings.show_ship;
				MyAPIGateway.Utilities.ShowMessage(MOD_NAME, string.Format("Show full piloted ship: {0}.", (instance.settings.show_ship ? "on (entire ship represented by a single color)" : "off (ship represented by an alternating color point)")));
				saveXML();
				return;
			}
			if (msg.StartsWith("/holo-scan", StringComparison.InvariantCultureIgnoreCase))
			{
				visible = false;
				if (instance == null) return;
				instance.settings.new_scan_method = !instance.settings.new_scan_method;
				instance.settings.new_scan_method = false;
				MyAPIGateway.Utilities.ShowMessage(MOD_NAME, string.Format("Scan method set to : {0}.", (instance.settings.new_scan_method ? "new" : "old")));
				saveXML();
				return;
			}
			/*else if (msg.StartsWith("/holo-display", StringComparison.InvariantCultureIgnoreCase))
			{
				visible = false;
				if (instance == null) return;
				instance.settings.advDraw = !instance.settings.advDraw;
				//instance.settings.new_scan_method = false;
				MyAPIGateway.Utilities.ShowMessage(MOD_NAME, string.Format("Draw method set to : {0}.", (instance.settings.advDraw ? "new" : "old")));
				saveXML();
				return;
			}*/
			else if (msg.StartsWith("/holo-color", StringComparison.InvariantCultureIgnoreCase))
			{
				visible = false;
				string[] words = msg.Split(' ');
				if (words.Length > 1)
				{
					switch (words[1].ToLowerInvariant())
					{
						case "self":
							instance.settings.Color_Self = ProcessColor(words, instance.settings.Color_Self);
							break;
						case "self2":
							instance.settings.Color_Self_Alt = ProcessColor(words, instance.settings.Color_Self_Alt);
							break;
						case "faction":
							instance.settings.Color_Faction = ProcessColor(words, instance.settings.Color_Faction);
							break;
						case "enemy":
							instance.settings.Color_Enemy = ProcessColor(words, instance.settings.Color_Enemy);
							break;
						case "fo":
						case "floatingobject":
							instance.settings.Color_FloatingObject = ProcessColor(words, instance.settings.Color_FloatingObject);
							break;
						case "voxel":
							instance.settings.Color_Voxel = ProcessColor(words, instance.settings.Color_Voxel);
							break;
						case "meteoriod":
						case "meteor":
							instance.settings.Color_Meteor = ProcessColor(words, instance.settings.Color_Meteor);
							break;
						case "unknown":
							instance.settings.Color_Unknown = ProcessColor(words, instance.settings.Color_Unknown);
							break;
						case "player":
						case "engineer":
							instance.settings.Color_Engineer = ProcessColor(words, instance.settings.Color_Engineer);
							break;
						case "neutral":
							instance.settings.Color_Neutral = ProcessColor(words, instance.settings.Color_Neutral);
							break;
						case "friend":
							instance.settings.Color_Friend = ProcessColor(words, instance.settings.Color_Friend);
							break;
						case "default":
							loadXML(true);
							MyAPIGateway.Utilities.ShowMessage(MOD_NAME, "Defaults loaded.");
							//saveXML();
							break;
						default:
							MyAPIGateway.Utilities.ShowMessage(MOD_NAME, "/holo-color [colortype] [colorname or R G B] - invalid colortype specified - possible types: [self, self2, faction, enemy, floatingobject, voxel, meteor, unknown, engineer, neutral, friend]");
							return;
					}
					saveXML();
				}
				else
					MyAPIGateway.Utilities.ShowMessage(MOD_NAME, "/holo-color [colortype] [colorname or R G B]");
			}
		}
		/// <summary>
		/// processes the color string from message entered to set a color.
		/// </summary>
		/// <param name="words">MessageEntered string</param>
		/// <param name="previous">previous value if color cannot be set</param>
		/// <returns></returns>
		private Vector3 ProcessColor(string[] words, Vector3 previous)
		{
			if (words.Length > 2)
			{
				Color foundColor;
				if (TryGetColor(words[2], out foundColor))
				{
					MyAPIGateway.Utilities.ShowMessage(MOD_NAME, string.Format("Color set to {0}", words[2]));
					return foundColor;
				}
				else
				{
					//this part is bugged
					if (words.Length > 4)
					{
						try
						{
							string _r = words[2];
							string _g = words[3];
							string _b = words[4];
							float s_r = Convert.ToSingle(_r);
							float s_g = Convert.ToSingle(_g);
							float s_b = Convert.ToSingle(_b);
							if (s_r < 0 || s_r > 255)
							{
								MyAPIGateway.Utilities.ShowMessage(MOD_NAME, string.Format("R G B values must be between 0 and 255"));
								return previous;
							}
							if (s_g < 0 || s_g > 255)
							{
								MyAPIGateway.Utilities.ShowMessage(MOD_NAME, string.Format("R G B values must be between 0 and 255"));
								return previous;
							}
							if (s_b < 0 || s_b > 255)
							{
								MyAPIGateway.Utilities.ShowMessage(MOD_NAME, string.Format("R G B values must be between 0 and 255"));
								return previous;
							}
							Color retVal = new Color(s_r, s_g, s_b);
							MyAPIGateway.Utilities.ShowMessage(MOD_NAME, string.Format("Color set to {0}", retVal));
							return retVal;
						}
						catch
						{
							MyAPIGateway.Utilities.ShowMessage(MOD_NAME, string.Format("R G B must be numbers."));
							return previous;
						}
					}
					else
					{
						MyAPIGateway.Utilities.ShowMessage(MOD_NAME, string.Format("Invalid color {0} {1} [colorname or R G B]", words[1].ToLowerInvariant(), words[2].ToLowerInvariant()));
						return previous;
					}
				}
			}
			else
			{
				MyAPIGateway.Utilities.ShowMessage(MOD_NAME, string.Format("/holo-color {0} [colorname or R G B]", words[1].ToLowerInvariant()));
				return previous;
			}
		}
		/// <summary>
		/// Tries to get the HSVDX11 value of the specified color.
		/// </summary>
		/// <param name="message">color string</param>
		/// <param name="foundColor">Returned color value if color is found</param>
		/// <returns></returns>
		private bool TryGetColor(string message, out Color foundColor)
		{
			foundColor = new Color();
			switch (message.ToLowerInvariant())
			{
				case "aliceblue":
					foundColor = Color.AliceBlue;
					return true;
				case "antiquewhite":
					foundColor = Color.AntiqueWhite;
					return true;
				case "aqua":
					foundColor = Color.Aqua;
					return true;
				case "aquamarine":
					foundColor = Color.Aquamarine;
					return true;
				case "azure":
					foundColor = Color.Azure;
					return true;
				case "beige":
					foundColor = Color.Beige;
					return true;
				case "bisque":
					foundColor = Color.Bisque;
					return true;
				case "black":
					foundColor = Color.Black;
					return true;
				case "blanchedalmond":
					foundColor = Color.BlanchedAlmond;
					return true;
				case "blue":
					foundColor = Color.Blue;
					return true;
				case "blueviolet":
					foundColor = Color.BlueViolet;
					return true;
				case "brown":
					foundColor = Color.Brown;
					return true;
				case "burlywood":
					foundColor = Color.BurlyWood;
					return true;
				case "badetblue":
					foundColor = Color.CadetBlue;
					return true;
				case "chartreuse":
					foundColor = Color.Chartreuse;
					return true;
				case "chocolate":
					foundColor = Color.Chocolate;
					return true;
				case "coral":
					foundColor = Color.Coral;
					return true;
				case "cornflowerblue":
					foundColor = Color.CornflowerBlue;
					return true;
				case "cornsilk":
					foundColor = Color.Cornsilk;
					return true;
				case "crimson":
					foundColor = Color.Crimson;
					return true;
				case "cyan":
					foundColor = Color.Cyan;
					return true;
				case "darkblue":
					foundColor = Color.DarkBlue;
					return true;
				case "darkcyan":
					foundColor = Color.DarkCyan;
					return true;
				case "darkgoldenrod":
					foundColor = Color.DarkGoldenrod;
					return true;
				case "darkgray":
					foundColor = Color.DarkGray;
					return true;
				case "darkgreen":
					foundColor = Color.DarkGreen;
					return true;
				case "darkkhaki":
					foundColor = Color.DarkKhaki;
					return true;
				case "darkmagenta":
					foundColor = Color.DarkMagenta;
					return true;
				case "darkoliveGreen":
					foundColor = Color.DarkOliveGreen;
					return true;
				case "darkorange":
					foundColor = Color.DarkOrange;
					return true;
				case "darkorchid":
					foundColor = Color.DarkOrchid;
					return true;
				case "darkred":
					foundColor = Color.DarkRed;
					return true;
				case "darksalmon":
					foundColor = Color.DarkSalmon;
					return true;
				case "darkseagreen":
					foundColor = Color.DarkSeaGreen;
					return true;
				case "darkslateblue":
					foundColor = Color.DarkSlateBlue;
					return true;
				case "darkslategray":
					foundColor = Color.DarkSlateGray;
					return true;
				case "darkturquoise":
					foundColor = Color.DarkTurquoise;
					return true;
				case "darkviolet":
					foundColor = Color.DarkViolet;
					return true;
				case "deeppink":
					foundColor = Color.DeepPink;
					return true;
				case "deepskyblue":
					foundColor = Color.DeepSkyBlue;
					return true;
				case "dimgray":
					foundColor = Color.DimGray;
					return true;
				case "dodgerblue":
					foundColor = Color.DodgerBlue;
					return true;
				case "firebrick":
					foundColor = Color.Firebrick;
					return true;
				case "floralwhite":
					foundColor = Color.FloralWhite;
					return true;
				case "forestgreen":
					foundColor = Color.ForestGreen;
					return true;
				case "fuchsia":
					foundColor = Color.Fuchsia;
					return true;
				case "gainsboro":
					foundColor = Color.Gainsboro;
					return true;
				case "ghostwhite":
					foundColor = Color.GhostWhite;
					return true;
				case "gold":
					foundColor = Color.Gold;
					return true;
				case "goldenrod":
					foundColor = Color.Goldenrod;
					return true;
				case "gray":
					foundColor = Color.Gray;
					return true;
				case "green":
					foundColor = Color.Green;
					return true;
				case "greenyellow":
					foundColor = Color.GreenYellow;
					return true;
				case "doneydew":
					foundColor = Color.Honeydew;
					return true;
				case "hotpink":
					foundColor = Color.HotPink;
					return true;
				case "indianred":
					foundColor = Color.IndianRed;
					return true;
				case "indigo":
					foundColor = Color.Indigo;
					return true;
				case "ivory":
					foundColor = Color.Ivory;
					return true;
				case "khaki":
					foundColor = Color.Khaki;
					return true;
				case "lavender":
					foundColor = Color.Lavender;
					return true;
				case "lavenderblush":
					foundColor = Color.LavenderBlush;
					return true;
				case "lawngreen":
					foundColor = Color.LawnGreen;
					return true;
				case "lemonchiffon":
					foundColor = Color.LemonChiffon;
					return true;
				case "lightblue":
					foundColor = Color.LightBlue;
					return true;
				case "lightcoral":
					foundColor = Color.LightCoral;
					return true;
				case "lightcyan":
					foundColor = Color.LightCyan;
					return true;
				case "lightgoldenrodyellow":
					foundColor = Color.LightGoldenrodYellow;
					return true;
				case "lightgray":
					foundColor = Color.LightGray;
					return true;
				case "lightgreen":
					foundColor = Color.LightGreen;
					return true;
				case "lightpink":
					foundColor = Color.LightPink;
					return true;
				case "lightsalmon":
					foundColor = Color.LightSalmon;
					return true;
				case "lightseagreen":
					foundColor = Color.LightSeaGreen;
					return true;
				case "lightskyblue":
					foundColor = Color.LightSkyBlue;
					return true;
				case "lightslategray":
					foundColor = Color.LightSlateGray;
					return true;
				case "lightsteelblue":
					foundColor = Color.LightSteelBlue;
					return true;
				case "lightyellow":
					foundColor = Color.LightYellow;
					return true;
				case "lime":
					foundColor = Color.Lime;
					return true;
				case "limegreen":
					foundColor = Color.LimeGreen;
					return true;
				case "linen":
					foundColor = Color.Linen;
					return true;
				case "magenta":
					foundColor = Color.Magenta;
					return true;
				case "maroon":
					foundColor = Color.Maroon;
					return true;
				case "mediumaquamarine":
					foundColor = Color.MediumAquamarine;
					return true;
				case "mediumblue":
					foundColor = Color.MediumBlue;
					return true;
				case "mediumorchid":
					foundColor = Color.MediumOrchid;
					return true;
				case "mediumpurple":
					foundColor = Color.MediumPurple;
					return true;
				case "mediumseagreen":
					foundColor = Color.MediumSeaGreen;
					return true;
				case "mediumslateblue":
					foundColor = Color.MediumSlateBlue;
					return true;
				case "mediumspringgreen":
					foundColor = Color.MediumSpringGreen;
					return true;
				case "mediumturquoise":
					foundColor = Color.MediumTurquoise;
					return true;
				case "mediumvioletred":
					foundColor = Color.MediumVioletRed;
					return true;
				case "midnightblue":
					foundColor = Color.MidnightBlue;
					return true;
				case "mintcream":
					foundColor = Color.MintCream;
					return true;
				case "mistyrose":
					foundColor = Color.MistyRose;
					return true;
				case "moccasin":
					foundColor = Color.Moccasin;
					return true;
				case "navajowhite":
					foundColor = Color.NavajoWhite;
					return true;
				case "navy":
					foundColor = Color.Navy;
					return true;
				case "oldlace":
					foundColor = Color.OldLace;
					return true;
				case "olive":
					foundColor = Color.Olive;
					return true;
				case "olivedrab":
					foundColor = Color.OliveDrab;
					return true;
				case "orange":
					foundColor = Color.Orange;
					return true;
				case "orangered":
					foundColor = Color.OrangeRed;
					return true;
				case "orchid":
					foundColor = Color.Orchid;
					return true;
				case "palegoldenrod":
					foundColor = Color.PaleGoldenrod;
					return true;
				case "palegreen":
					foundColor = Color.PaleGreen;
					return true;
				case "paleturquoise":
					foundColor = Color.PaleTurquoise;
					return true;
				case "palevioletred":
					foundColor = Color.PaleVioletRed;
					return true;
				case "papayawhip":
					foundColor = Color.PapayaWhip;
					return true;
				case "peachpuff":
					foundColor = Color.PeachPuff;
					return true;
				case "peru":
					foundColor = Color.Peru;
					return true;
				case "pink":
					foundColor = Color.Pink;
					return true;
				case "plum":
					foundColor = Color.Plum;
					return true;
				case "powderblue":
					foundColor = Color.PowderBlue;
					return true;
				case "purple":
					foundColor = Color.Purple;
					return true;
				case "red":
					foundColor = Color.Red;
					return true;
				case "rosybrown":
					foundColor = Color.RosyBrown;
					return true;
				case "royalblue":
					foundColor = Color.RoyalBlue;
					return true;
				case "saddlebrown":
					foundColor = Color.SaddleBrown;
					return true;
				case "salmon":
					foundColor = Color.Salmon;
					return true;
				case "sandybrown":
					foundColor = Color.SandyBrown;
					return true;
				case "seagreen":
					foundColor = Color.SeaGreen;
					return true;
				case "seashell":
					foundColor = Color.SeaShell;
					return true;
				case "sienna":
					foundColor = Color.Sienna;
					return true;
				case "silver":
					foundColor = Color.Silver;
					return true;
				case "skyblue":
					foundColor = Color.SkyBlue;
					return true;
				case "slateblue":
					foundColor = Color.SlateBlue;
					return true;
				case "slategray":
					foundColor = Color.SlateGray;
					return true;
				case "snow":
					foundColor = Color.Snow;
					return true;
				case "springgreen":
					foundColor = Color.SpringGreen;
					return true;
				case "steelblue":
					foundColor = Color.SteelBlue;
					return true;
				case "tan":
					foundColor = Color.Tan;
					return true;
				case "teal":
					foundColor = Color.Teal;
					return true;
				case "thistle":
					foundColor = Color.Thistle;
					return true;
				case "tomato":
					foundColor = Color.Tomato;
					return true;
				case "turquoise":
					foundColor = Color.Turquoise;
					return true;
				case "violet":
					foundColor = Color.Violet;
					return true;
				case "wheat":
					foundColor = Color.Wheat;
					return true;
				case "white":
					foundColor = Color.White;
					return true;
				case "whitesmoke":
					foundColor = Color.WhiteSmoke;
					return true;
				case "yellow":
					foundColor = Color.Yellow;
					return true;
				case "yellowgreen":
					foundColor = Color.YellowGreen;
					return true;
			}
			return false;
		}
		//called in multiple threads.
		/// <summary>
		/// Workaround for clients of nondedicated servers. :Keen:
		/// </summary>
		/// <param name="obj"></param>
		public void unload()
		{
			Log.Info("Closing Holo Mod.");
			if (init && !isDedicated)
			{
				MyAPIGateway.Utilities.MessageEntered -= Utilities_MessageEntered;
				//MyAPIGateway.Entities.OnEntityAdd -= Entities_OnEntityAdd;
				init = false;
			}
			isServer = false;
			isDedicated = false;
			//settings = null;
			UpdateHook = null; //dump;
			running = false;
			Log.Info("Closed.");
			Log.Close();
		}
		internal static bool ClosetoPlayer(Vector3D pos, double range)
		{
			if (MyAPIGateway.Session == null) return false;
			if (MyAPIGateway.Session.ControlledObject == null) return true;
			if (MyAPIGateway.Session.ControlledObject.Entity == null) return true;
			if (MyAPIGateway.Session.ControlledObject.Entity.WorldMatrix == null) return true;
			if (Vector3D.Distance(pos, MyAPIGateway.Session.Camera.ViewMatrix.Translation) < range) return true;
			if (Vector3D.Distance(pos, MyAPIGateway.Session.ControlledObject.Entity.WorldMatrix.Translation) < range) return true;
			return false;
		}
		public void saveXML()
		{
			Log.DebugWrite(DebugLevel.Info, "Saving XML");
			setCurrentVersion();
			var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(FILE, typeof(ColorSettings));
			writer.Write(MyAPIGateway.Utilities.SerializeToXML(instance.settings));
			writer.Flush();
			writer.Close();
			Log.DebugWrite(DebugLevel.Info, "Save Complete");
		}
		public void loadXML(bool l_default = false)
		{
			Log.DebugWrite(DebugLevel.Info, "Loading XML");
			if (instance == null) return;
			if (l_default)
			{
				settings = new ColorSettings();
				setCurrentVersion();
				return;
			}
			try
			{
				if (MyAPIGateway.Utilities.FileExistsInLocalStorage(FILE, typeof(ColorSettings)) && !l_default)
				{
					var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(FILE, typeof(ColorSettings));
					var xmlText = reader.ReadToEnd();
					reader.Close();
					instance.settings = MyAPIGateway.Utilities.SerializeFromXML<ColorSettings>(xmlText);
					checkVersion();
					return;
				}
			}
			catch (Exception ex)
			{
				Log.DebugWrite(DebugLevel.Error, ex);
			}
			try
			{
				if (MyAPIGateway.Utilities.FileExistsInGlobalStorage(FILE))
				{
					var reader = MyAPIGateway.Utilities.ReadFileInGlobalStorage(FILE);
					var xmlText = reader.ReadToEnd();
					reader.Close();
					instance.settings = MyAPIGateway.Utilities.SerializeFromXML<ColorSettings>(xmlText);
				}
			}
			catch (Exception ex)
			{
				settings = new ColorSettings();
				Log.DebugWrite(DebugLevel.Error, ex);
				//Log.Info("Could not load configuration: " + ex.ToString());
			}
			Log.DebugWrite(DebugLevel.Info, "Load Complete");
		}
		private void setCurrentVersion()
		{
			instance.settings.version = MOD_VERSION;
		}
		private void checkVersion()
		{
			if (instance.settings.version == 1) loadXML(true);//Reset settings
		}
	}
}
