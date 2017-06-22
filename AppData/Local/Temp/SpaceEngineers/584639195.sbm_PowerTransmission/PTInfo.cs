using System;

namespace Cython.PowerTransmission
{
	public class PTInfo
	{
		public long Id;
		public uint ChannelTarget;
		public float Power;
		public string Type;
		public bool Sender;

		public PTInfo (long id, bool sender, uint channel_target, float power, string type)
		{
			this.Id = id;
			this.Sender = sender;
			this.ChannelTarget = channel_target;
			this.Power = power;
			this.Type = type;
		}

		public PTInfo ()
		{
		}
	}
}

