using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using System.IO;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace SGCam_HelmetAT
{
    public class LOG    //call as: LOG.Log.WriteLine(string)
    {
        public static bool debug = true;
        public static readonly string filenameLOG1 = "HelmetAutoToggle_DEBUG.log";    //select log filename
        private readonly StringBuilder _stringCache = new StringBuilder();
        private int _stringIndent;
        private readonly TextWriter _stringWriter;
        public static LOG log1;
        
        public LOG(string logfile)
        {
            if (debug)
            {
                try
                {
                    _stringWriter = MyAPIGateway.Utilities.WriteFileInLocalStorage(logfile, typeof(LOG));
                    log1 = this;
                }
                catch
                {
                }
            }
            else
            {
                _stringWriter = null;
                log1 = this;
                return;
            }
        }
        public static LOG Log
        {
            
                
                get
            {
                    if (MyAPIGateway.Utilities == null)
                        return null;
                    if (log1 == null)
                    {
                        log1 = new LOG(filenameLOG1);
                    }
                    return log1;
                }
            
        }
        public void IncreaseIndent()
        {
            _stringIndent++;
        }
        public void DecreaseIndent()
        {
            _stringIndent--;
        }
        public void WriteLine(string text)
        {
            try
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    try
                    {
                        if (_stringCache.Length > 0)
                            _stringWriter.WriteLine(_stringCache);

                        _stringCache.Clear();
                        _stringCache.Append(DateTime.Now.ToString("[HH:mm:ss:ffff] "));
                        for (var i = 0; i < _stringIndent; i++)
                            _stringCache.Append("\t");

                        _stringWriter.WriteLine(_stringCache.Append(text));
                        _stringWriter.Flush();
                        _stringCache.Clear();
                    }
                    catch
                    {
                    }
                });
            }
            catch { }
        }
        private static int lineCount = 0;
        private static bool init;
        private static string[] log = new string[10];
        public void Debug_obj(string text)
        {
            IncreaseIndent();
            WriteLine("DEBUG_OBJ: " + text);
            DecreaseIndent();
            text = $"{DateTime.Now.ToString("[HH:mm:ss:ffff]")}: {text}";
            if (!init)
            {
                init = true;
                MyAPIGateway.Utilities.GetObjectiveLine().Title = "Kits debug";
                MyAPIGateway.Utilities.GetObjectiveLine().Objectives.Clear();
                MyAPIGateway.Utilities.GetObjectiveLine().Objectives.Add("Start");
                MyAPIGateway.Utilities.GetObjectiveLine().Show();
            }
            if (lineCount > 9)
                lineCount = 0;
            log[lineCount] = text;
            string[] oldLog = log;
            for (int i = 0; i < 9; i++)
            {
                log[i] = oldLog[i + 1];
            }
            log[9] = text;
            MyAPIGateway.Utilities.GetObjectiveLine().Objectives[0] = string.Join("\r\n", log);
            lineCount++;
        }
        public void Write(string text)
        {
            _stringCache.Append(text);
        }
        internal void Close()
        {
            if (_stringCache.Length > 0)
                _stringWriter.WriteLine(_stringCache);
            _stringWriter.Flush();
            _stringWriter.Close();
        }
    }
}
