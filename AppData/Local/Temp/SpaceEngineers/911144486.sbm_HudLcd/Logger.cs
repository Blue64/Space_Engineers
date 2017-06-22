using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace Jawastew
{
    public static class Logger
    {
        private static TextWriter writer = null;
        private static String cache = "";
        static bool islogging = false;

        public static bool logging(bool state)
        {
            return islogging = state;
        }

        public static void log(String mod, String lines)
        {
            if (!islogging)
            {
                return;
            }
            String tmp = DateTime.Now.ToString("[HH:mm:ss] ") + mod + ": " + lines;
            if (writer == null)
            {
                try
                {
                    writer = MyAPIGateway.Utilities.WriteFileInLocalStorage("info.log", typeof(Logger));
                } catch (Exception)
                {
                    
                }
            }
            try
            {
                if (cache != "" && cache.Length < 10000)
                {
                    tmp = cache + tmp;
                    cache = "";
                } else
                {
                    cache = "";
                }
                writer.WriteLine(tmp);
                writer.Flush();
            } catch (Exception e)
            {
                cache += "\r\n" + tmp;
            }
            
        }

        public static void Close()
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }
    }
}
