using Sandbox.Definitions;
//using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.ModAPI;

namespace AirlockRKR
{
    public class Logger
    {
        public String debugLcdName = null;
        public bool debug = false;
        public IMyTextPanel statusPanel = null;

        public Logger(String debugLcdName, bool debug)
        {
            this.debugLcdName = debugLcdName;
            this.debug = debug;
        }

        public Logger(IMyTextPanel statusPanel)
        {
            this.statusPanel = statusPanel;
            if (statusPanel != null)
            {
                statusPanel.SetShowOnScreen(Sandbox.Common.ObjectBuilders.ShowTextOnScreenFlag.PUBLIC);
            }
        }

        public void log(IMyGridTerminalSystem gridTerminal, string message, ErrorSeverity error, bool append = true)
        {
            if (debug && gridTerminal != null)
            {
                IMyTextPanel lcd = Utils.searchLcdWithName(gridTerminal, this.debugLcdName);
                this.log(lcd, message, error, append);
            }
        }

        public void log(IMyTextPanel lcd, string message, ErrorSeverity error, bool append = true)
        {
            if (debug)
            {
                if (lcd != null)
                {
                    lcd.WritePublicText(error.ToString() + ":" + message + "\n", append);
                }
            }
            else if (ErrorSeverity.Notice.Equals(error))
            {
                if (lcd != null)
                {
                    lcd.WritePublicText(message + "\n", append);
                }
            }
        }

        public void Clear(IMyGridTerminalSystem gridTerminal)
        {
            if (debug && gridTerminal != null)
            {
                IMyTextPanel lcd = Utils.searchLcdWithName(gridTerminal, this.debugLcdName);
                this.Clear(lcd);
            }
        }

        public void Clear(IMyTextPanel lcd)
        {
            if (debug)
            {
                log(lcd, "Logger aktiviert", ErrorSeverity.Notice, false);
            }
            else
            {
                log(lcd, "Status Schleusensteuerung", ErrorSeverity.Notice, false);
            }
        }

    }
}
