namespace MagRails
{
	using System;
	using System.Text;
	using Sandbox.ModAPI;
	using Sandbox.Common;
	using VRage.Utils;
	using VRage.Game.Components;

	/*
	*	Logger written by Digi.
	*	Some modifications by Draygo
	*	Debugger introduced by Draygo
	*/
	class Log : MySessionComponentBase
	{
		private const string MOD_NAME = "MagRails";
		private const string LOG_FILE = "info.log";

		private static System.IO.TextWriter writer = null;
		private static IMyHudNotification notify = null;
		private static int indent = 0;
		private static StringBuilder cache = new StringBuilder();

		internal static bool running = false;
		internal static DebugLevel _DebugLevel = DebugLevel.None;

		public static void IncreaseIndent()
		{
			indent++;
		}

		public static void DecreaseIndent()
		{
			if (indent > 0)
				indent--;
		}

		public static void ResetIndent()
		{
			indent = 0;
		}

		public static void Error(Exception e)
		{
			Error(e.ToString());
		}
		public static void DebugWrite(DebugLevel _d, string msg)
		{
			if (!running) return;
			if (_DebugLevel == DebugLevel.None) return;
			if (_DebugLevel == DebugLevel.Custom)
			{
				if (_d == DebugLevel.Custom) Info(msg);
				return;
			}

			if (_DebugLevel == DebugLevel.Error)
			{
				if (_d == DebugLevel.Error)
					Error(msg);
				return;
			}
			if (_DebugLevel == DebugLevel.Info)
			{
				if (_d == DebugLevel.Error)
					Error(msg);
				if (_d == DebugLevel.Info)
					Info(msg);
				return;
			}
			if (_DebugLevel == DebugLevel.Verbose)
			{
				if (_d == DebugLevel.Error)
					Error(msg);
				if (_d == DebugLevel.Info)
					Info(msg);
				if (_d == DebugLevel.Verbose)
					Info("*" + msg);
				return;
			}
		}
		public static void DebugWrite<T>(DebugLevel _d, T msg)
		{
			DebugWrite(_d, msg.ToString());
		}
		public static void Error(string msg)
		{
			Info("ERROR: " + msg);

			try
			{
				string text = MOD_NAME + " error - open %AppData%/SpaceEngineers/Storage/" + MyAPIGateway.Session.WorkshopId + "_" + MOD_NAME + "/" + LOG_FILE + " for details";

				MyLog.Default.WriteLineAndConsole(text);
			}
			catch (Exception e)
			{
				Info("ERROR: Could not send notification to local client: " + e.ToString());
			}
		}

		public static void Info(string msg)
		{
			Write(msg);
		}

		private static void Write(string msg)
		{
			try
			{
				if (writer == null)
				{
					if (MyAPIGateway.Utilities == null)
						throw new Exception("API not initialied but got a log message: " + msg);

					writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(LOG_FILE, typeof(Log));
				}

				cache.Clear();
				cache.Append(DateTime.Now.ToString("[HH:mm:ss] "));

				for (int i = 0; i < indent; i++)
				{
					cache.Append("\t");
				}

				cache.Append(msg);

				writer.WriteLine(cache);
				writer.Flush();

				cache.Clear();
			}
			catch (Exception e)
			{
				MyLog.Default.WriteLineAndConsole(MOD_NAME + " had an error while logging message='" + msg + "'\nLogger error: " + e.Message + "\n" + e.StackTrace);
			}
		}

		public static void Close()
		{
			if (writer != null)
			{
				writer.Flush();
				writer.Close();
				writer = null;
			}

			indent = 0;
			cache.Clear();
		}
	}
	public enum DebugLevel
	{
		None = 0,
		Error,
		Info,
		Verbose,
		Custom
	}
}
