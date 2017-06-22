namespace Parachute
{
	public class ParaSettings
	{
		private DebugLevel logLevel = DebugLevel.None;
		public DebugLevel debug
		{
			get { return logLevel; }
			set { logLevel = value; }
		}
	}
}
