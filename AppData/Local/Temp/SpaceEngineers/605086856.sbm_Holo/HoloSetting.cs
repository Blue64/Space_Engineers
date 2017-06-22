namespace Hologram
{
	public class HoloSetting
	{
		double m_b = 0.0;
		double m_d = 0.0;
		double m_f = 0.0;
		double m_l = 0.0;
		double m_r = 0.0;
		double m_s = 1.0;
		double m_u = 0.0;
		public double B
		{
			get { return m_b; }
			internal set
			{
				if (value <= 30d)
				{
					if (value >= 0d) m_b = value / 10d;
					else
						m_b = 0d;
				}
				else
					m_b = 3d;
			}
		}
		public double D
		{
			get { return m_d; }
			internal set
			{
				if (value <= 30d)
				{
					if (value >= 0d) m_d = value / 10d;
					else
						m_d = 0d;
				}
				else
					m_d = 3d;
			}
		}
		public double F
		{
			get { return m_f; }
			internal set
			{
				if (value <= 30d)
				{
					if (value >= 0d) m_f = value / 10d;
					else
						m_f = 0d;
				}
				else
					m_f = 3d;
			}
		}
		public double L
		{
			get { return m_l; }
			internal set
			{
				if (value <= 30d)
				{
					if (value >= 0d) m_l = value / 10d;
					else
						m_l = 0d;
				}
				else
					m_l = 3d;
			}
		}
		public double R
		{
			get { return m_r; }
			internal set
			{
				if (value <= 30d)
				{
					if (value >= 0d) m_r = value / 10d;
					else
						m_r = 0d;
				}
				else
					m_r = 3d;
			}
		}
		public double S
		{
			get { return m_s; }
			internal set
			{
				if (value <= 3d)
				{
					if (value >= 0.05) m_s = value;
					else
						m_s = 0.05;
				}
				else
					m_s = 3d;
			}
		}
		public double U
		{
			get { return m_u; }
			internal set
			{
				if (value <= 30d)
				{
					if (value >= 0d) m_u = value / 10d;
					else
						m_u = 0d;
				}
				else
					m_u = 3d;
			}
		}
		public override string ToString()
		{
			return string.Format("HoloSetting[F: {0} B: {1} U: {2} D: {3} L: {4} R: {5} S: {6}", m_f, m_b, m_u, m_d, m_l, m_r, m_s);
		}
	}
}