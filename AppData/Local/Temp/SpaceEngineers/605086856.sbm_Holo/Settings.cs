using VRageMath;
namespace Hologram
{
	public class ColorSettings
	{
		#region defaults
		private Color m_Color_Self				= Color.Blue;
		private Color m_Color_Self_Alt			= Color.White;
		private Color m_Color_Faction			= Color.Cyan;
		private Color m_Color_Enemy				= Color.Red;
		private Color m_Color_FloatingObject	= Color.Yellow;
		private Color m_Color_Voxel				= Color.Gray;
		private Color m_Color_Meteor			= Color.Brown;
		private Color m_Color_Unknown			= Color.Brown;
		private Color m_Color_Engineer			= Color.Purple;
		private Color m_Color_Neutral			= Color.OrangeRed;
		private Color m_Color_Friend			= Color.Green;
		private int m_vox_cnt					= 6;
		private bool m_ship						= false;
		private bool m_scan_method				= false;
		private bool m_advDraw					= false;
		private int m_version					= 1;
		#endregion
		#region properties
		public Color Color_Self
		{
			get { return m_Color_Self; }
			set { m_Color_Self = value; }
		}
		public Color Color_Self_Alt
		{
			get { return m_Color_Self_Alt; }
			set { m_Color_Self_Alt = value; }
		}
		public Color Color_Faction
		{
			get { return m_Color_Faction; }
			set { m_Color_Faction = value; }
		}
		public Color Color_Enemy
		{
			get { return m_Color_Enemy; }
			set { m_Color_Enemy = value; }
		}
		public Color Color_FloatingObject
		{
			get { return m_Color_FloatingObject; }
			set { m_Color_FloatingObject = value; }
		}
		public Color Color_Voxel
		{
			get { return m_Color_Voxel; }
			set { m_Color_Voxel = value; }
		}
		public Color Color_Meteor
		{
			get { return m_Color_Meteor; }
			set { m_Color_Meteor = value; }
		}
		public Color Color_Unknown
		{
			get { return m_Color_Unknown; }
			set { m_Color_Unknown = value; }
		}
		public Color Color_Engineer
		{
			get { return m_Color_Engineer; }
			set { m_Color_Engineer = value; }
		}
		public Color Color_Neutral
		{
			get { return m_Color_Neutral; }
			set { m_Color_Neutral = value; }
		}
		public Color Color_Friend
		{
			get { return m_Color_Friend; }
			set { m_Color_Friend = value; }
		}
		public int vox_cnt
		{
			get { return m_vox_cnt; }
			set
			{
				if (value >= 1) m_vox_cnt = value;
				else
					m_vox_cnt = 6;//set default if 0 or something strange. 
			}
		}
		public bool show_ship
		{
			get { return m_ship; }
			set { m_ship = value; }
		}
		public bool new_scan_method
		{
			get { return m_scan_method; }
			set { m_scan_method = value; }
		}
		public bool advDraw
		{
			get { return true; }
			set { m_advDraw = value; }
		}
		public int version
		{
			get { return m_version; }
			set { m_version = value; }
		}
		#endregion
	}
}
