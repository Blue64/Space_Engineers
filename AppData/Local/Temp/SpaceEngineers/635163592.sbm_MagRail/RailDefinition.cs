using System.Collections.Generic;
using VRageMath;
namespace MagRails
{
	public class RailDictionary
	{
		private Dictionary<string, RailDefinition> def = new Dictionary<string, RailDefinition>();
		public bool TryAdd( string subtypename, RailDefinition definition )
		{
			if(def.ContainsKey(subtypename))
				return false;
			def.Add(subtypename, definition);
			return true;
		}
		public bool TryGetDef ( string subtypename, out RailDefinition definition)
		{
			return def.TryGetValue(subtypename, out definition);
		}
	}
	public class RailDefinition
	{
		private Vector3D m_min = Vector3D.Zero;

		public double radius = 0.0;
		public RailType type = RailType.none;

		public double X = 0.0;
		public double Y = 0.0;
		public double Z = 0.0;

		public Vector3I size = Vector3I.Zero;
		public double sizeenum = 2.5;

		public bool valid = false;
		public Vector3D pos
		{
			get { return new Vector3D(X, Y, Z); }
		}
		public Vector3D min
		{
			get { return m_min; }
			internal set {m_min = value;}
		}
	}
	public enum RailType
	{
		none = 0,
		straight,
		curve,
		slant,
		cross
	}
}