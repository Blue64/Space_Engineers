using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AirlockRKR
{
    public class ConfigReader
    {
        private IMyGridTerminalSystem gridTerminal = null;
        private IMyTerminalBlock terminalBlock = null;
        private String configLCDName = null;

        public ConfigReader(IMyGridTerminalSystem gridTerminal, IMyTerminalBlock terminalBlock, String configLCDName)
        {
            this.terminalBlock = terminalBlock;
            this.gridTerminal = gridTerminal;
            this.configLCDName = configLCDName;
        }

        public Config readConfig()
        {
            Config config = new Config();
            IMyTextPanel configLCD = Utils.searchLcdWithName(gridTerminal, this.configLCDName);
            if (configLCD != null)
            {
                String script = configLCD.GetPublicText();
                if (!String.IsNullOrEmpty(script))
                {
                    String[] lines = script != null ? script.Split(new string[] { "\n" }, StringSplitOptions.None) : null;
                    if (lines != null && lines.Length >= 1)
                    {
                        foreach (String line in lines)
                        {
                            String[] keyValue = line.Split(new string[] { "=" }, StringSplitOptions.None);
                            if (keyValue.Length == 2)
                            {
                                String key = keyValue[0].Replace("const string", "").Replace("string", "").Replace("const String", "").Replace("String", "").Trim();
                                String valuesString = keyValue[1].Replace("\"", "").Replace(";", "");
                                String[] values = valuesString.Split(new string[] { "," }, StringSplitOptions.None);
                                switch (key)
                                {
                                    case "doors1":
                                        config.doors1.AddList(searchDoors(values));
                                        break;
                                    case "doors2":
                                        config.doors2.AddList(searchDoors(values));
                                        break;
                                    case "vents1":
                                        config.vents1.AddList(searchVents(values));
                                        break;
                                    case "sluiceVents":
                                        config.sluiceVents.AddList(searchVents(values));
                                        break;
                                    case "vents2":
                                        config.vents2.AddList(searchVents(values));
                                        break;
                                    case "pressurized1":
                                        bool pressurized1 = false;
                                        if (values.Length > 0 && Boolean.TryParse(values[0], out pressurized1))
                                        {
                                            config.pressurized1 = pressurized1;
                                        }
                                        break;
                                    case "pressurized2":
                                        bool pressurized2 = false;
                                        if (values.Length > 0 && Boolean.TryParse(values[0], out pressurized2))
                                        {
                                            config.pressurized2 = pressurized2;
                                        }
                                        break;
                                    case "sensors1":
                                        config.sensors1.AddList(searchSensors(values));
                                        break;
                                    case "sluiceSensors":
                                        config.sluiceSensors.AddList(searchSensors(values));
                                        break;
                                    case "sensors2":
                                        config.sensors2.AddList(searchSensors(values));
                                        break;
                                    case "lights":
                                        config.lights.AddList(searchLights(values));
                                        break;
                                    case "abortSeconds":
                                        long abortMilliseconds = 0;
                                        if (values.Length > 0 && long.TryParse(values[0].Trim(), out abortMilliseconds))
                                        {
                                            config.abortMilliseconds = abortMilliseconds * 1000;
                                        }
                                        break;
                                    case "statusPanel":
                                        if (values.Length > 0)
                                        {
                                            config.statusPanel = Utils.searchLcdWithName(this.gridTerminal, values[0].Trim());
                                        }
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    configLCD.WritePublicText("const String doors1 = \"Tür1\";\n" +
                                              "const String pressurized1 = \"true\";\n" +
                                              "const String vents1 = \"\";\n" +
                                              "const String sensors1 = \"sensor1\";\n" +
                                              "const String sluiceVents = \"Vent1\";\n" +
                                              "const String sluiceSensors = \"sluiceSensor\";\n" +
                                              "const String doors2 = \"Tür2\";\n" +
                                              "const String pressurized2 = \"false\";\n" +
                                              "const String vents2 = \"\";\n" +
                                              "const String sensors2 = \"sensor2\";\n" +
                                              "const String lights = \"\";\n" +
                                              "const String abortSeconds = \"10\";\n" +
                                              "const String statusPanel = \"textPanel\";\n", false);
                    configLCD.SetShowOnScreen(Sandbox.Common.ObjectBuilders.ShowTextOnScreenFlag.PUBLIC);
                }
            }
            return config;
        }

        public List<IMyInteriorLight> searchLights(string[] values)
        {
            List<IMyInteriorLight> lights = new List<IMyInteriorLight>();
            for (int i = 0; i < values.Length; i++)
            {
                IMyTerminalBlock block = this.gridTerminal.GetBlockWithName(values[i].Trim());
                if (block != null && block as IMyInteriorLight != null)
                {
                    IMyInteriorLight light = block as IMyInteriorLight;
                    if (!lights.Contains(light))
                    {
                        lights.Add(light);
                    }
                }
            }
            return lights;
        }

        public List<IMySensorBlock> searchSensors(string[] values)
        {
            List<IMySensorBlock> sensors = new List<IMySensorBlock>();
            for (int i = 0; i < values.Length; i++)
            {
                IMyTerminalBlock block = this.gridTerminal.GetBlockWithName(values[i].Trim());
                if (block != null && block as IMySensorBlock != null)
                {
                    IMySensorBlock sensor = block as IMySensorBlock;
                    if (!sensors.Contains(sensor))
                    {
                        sensors.Add(sensor);
                    }
                }
            }
            return sensors;
        }

        public List<IMyAirVent> searchVents(string[] values)
        {
            List<IMyAirVent> vents = new List<IMyAirVent>();
            for (int i = 0; i < values.Length; i++)
            {
                IMyTerminalBlock block = this.gridTerminal.GetBlockWithName(values[i].Trim());
                if (block != null && block as IMyAirVent != null)
                {
                    IMyAirVent vent = block as IMyAirVent;
                    if (!vents.Contains(vent))
                    {
                        vents.Add(vent);
                    }
                }
            }
            return vents;
        }

        public List<IMyDoor> searchDoors(String[] values)
        {
            List<IMyDoor> doors = new List<IMyDoor>();
            for (int i = 0; i < values.Length; i++)
            {
                IMyTerminalBlock block = this.gridTerminal.GetBlockWithName(values[i].Trim());
                if (block != null && block as IMyDoor != null)
                {
                    IMyDoor door = block as IMyDoor;
                    if (!doors.Contains(door))
                    {
                        doors.Add(door);
                    }
                }
            }
            return doors;
        }
    }
}
