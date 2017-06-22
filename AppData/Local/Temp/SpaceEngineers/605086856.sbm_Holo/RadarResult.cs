using System.Collections.Generic;
using VRage.Game;
using VRageMath;
namespace Hologram
{
	public enum ResultType
	{
		Self = 0,
		Self_Alt,
		Faction,
		Enemy,
		FloatingObject,
		Voxel,
		Meteor,
		Unknown,
		Engineer,
		Neutral,
		Friend,
		Self_Point,
		Self_Point_Alt
	}
	public class RadarResult
	{
		public int id = 0;
		public Radar _radar;
		private Dictionary<Vector3I, ResultType> m_ColorData = new Dictionary<Vector3I, ResultType>();
		public RadarResult(RadarResult t_result)
		{
			m_ColorData = new Dictionary<Vector3I, ResultType>(t_result.m_ColorData);
		}
		public RadarResult()
		{
		}
		public void Clear()
		{
			m_ColorData.Clear();
		}
		public static Color getColor(ResultType en)
		{
			switch (en)
			{
				case ResultType.Self:
				case ResultType.Self_Point:
					return CoreHolo.Color_Self;
				case ResultType.Self_Alt:
				case ResultType.Self_Point_Alt:
					return CoreHolo.Color_Self_Alt;
				case ResultType.Enemy:
					return CoreHolo.Color_Enemy;
				case ResultType.Engineer:
					return CoreHolo.Color_Engineer;
				case ResultType.Faction:
					return CoreHolo.Color_Faction;
				case ResultType.FloatingObject:
					return CoreHolo.Color_FloatingObject;
				case ResultType.Friend:
					return CoreHolo.Color_Friend;
				case ResultType.Meteor:
					return CoreHolo.Color_Meteor;
				case ResultType.Neutral:
					return CoreHolo.Color_Neutral;
				case ResultType.Voxel:
					return CoreHolo.Color_Voxel;
			}
			return CoreHolo.Color_Unknown;
		}
		public Dictionary<Vector3I, ResultType> ColorData
		{
			get { return m_ColorData; }
			set { m_ColorData = value; }
		}
	}
}
