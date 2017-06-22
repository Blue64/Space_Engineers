using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.Components;
using Sandbox.Definitions;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace AirlockRKR
{
    public static class Utils
    {

        public static Vector3 ToHsvColor(this VRageMath.Color color)
        {
            var hsvColor = color.ColorToHSV();
            return new Vector3(hsvColor.X, hsvColor.Y * 2f - 1f, hsvColor.Z * 2f - 1f);
        }
        public static VRageMath.Color ToColor(this Vector3 hsv)
        {
            return new Vector3(hsv.X, (hsv.Y + 1f) / 2f, (hsv.Z + 1f) / 2f).HSVtoColor();
        }

        public static void setPressureColor(List<IMyInteriorLight> lights, List<IMyAirVent> vans, Boolean ignoreIfYellow = false)
        {
            if (ignoreIfYellow && lights.Count > 0 && Color.Yellow.Equals(lights[0].GetValue<Color>("Color")))
            {
                return;
            }
            if (!isPressurized(vans[0]))
            {
                setColor(lights, Color.Red);
            }
            else
            {
                setColor(lights, Color.Green);
            }
        }

        public static void setColor(List<IMyInteriorLight> lights, Color color)
        {
            for (int i = 0; i < lights.Count; i++)
            {
                IMyInteriorLight light = lights[i];
                light.SetValue<Color>("Color", color);
            }
        }

        public static float getPressure(IMyAirVent vanToCheck)
        {
            return vanToCheck.GetOxygenLevel() * 100;
        }

        public static float getCurrentOxygenFill(Sandbox.ModAPI.Ingame.IMyGridTerminalSystem gridTerminalSystem)
        {
            List<IMyOxygenTank> oxygenTanks = new List<IMyOxygenTank>();
            List<IMyTerminalBlock> gridBlocks = new List<IMyTerminalBlock>();
            gridTerminalSystem.GetBlocksOfType<IMyOxygenTank>(gridBlocks);
            foreach (IMyTerminalBlock gridBlock in gridBlocks)
            {
                if (gridBlock as IMyOxygenTank != null)
                {
                    oxygenTanks.Add(gridBlock as IMyOxygenTank);
                }
            }
            return getCurrentOxygenFill(oxygenTanks[0]);
        }

        public static float getCurrentOxygenFill(IMyOxygenTank tankToCheck)
        {
            return tankToCheck.GetOxygenLevel() * 100;
        }

        public static bool isPressurized(IMyAirVent vanToCheck)
        {
            return Utils.getPressure(vanToCheck) > 95;
        }

        public static bool isOneDoorOpen(List<IMyDoor> doors)
        {
            bool isOpen = false;
            foreach (IMyDoor door in doors)
            {
                isOpen |= door.Open;
            }
            return isOpen;
        }

        public static bool isOneDoorClosed(List<IMyDoor> doors)
        {
            bool isClosed = false;
            foreach (IMyDoor door in doors)
            {
                isClosed |= !door.Open;
            }
            return isClosed;
        }

        public static void closeDoors(List<IMyDoor> doors)
        {
            foreach (IMyDoor door in doors)
            {
                door.GetActionWithName("Open_Off").Apply(door);
            }
        }

        public static void openDoors(List<IMyDoor> doors)
        {
            foreach (IMyDoor door in doors)
            {
                door.GetActionWithName("Open_On").Apply(door);
            }
        }

        public static void pressurizeRoom(List<IMyAirVent> vents) 
        {
            foreach (IMyAirVent vent in vents) {
                vent.GetActionWithName("Depressurize_Off").Apply(vent);
            }
        }

        public static void depressurizeRoom(List<IMyAirVent> vents)
        {
            foreach (IMyAirVent vent in vents)
            {
                vent.GetActionWithName("Depressurize_On").Apply(vent);
            }
        }

        internal static List<Control.Event> getSensorStatusChanges(Dictionary<IMySensorBlock, bool> sensorStatus, Config config)
        {
            List<Control.Event> currentEvents = new List<Control.Event>();
            foreach (IMySensorBlock sensor in config.sensors1)
            {
                bool oldStatus = sensorStatus.GetValueOrDefault(sensor, false);
                if (!oldStatus.Equals(sensor.IsActive) || sensor.IsActive)
                {
                    currentEvents.Add(sensor.IsActive ? Control.Event.InEnter : Control.Event.InLeave);
                }
                if (sensorStatus.ContainsKey(sensor))
                {
                    sensorStatus.Remove(sensor);
                }
                sensorStatus.Add(sensor, sensor.IsActive);
            }
            foreach (IMySensorBlock sensor in config.sluiceSensors)
            {
                bool oldStatus = sensorStatus.GetValueOrDefault(sensor, false);
                if (!oldStatus.Equals(sensor.IsActive) || sensor.IsActive)
                {
                    currentEvents.Add(sensor.IsActive ? Control.Event.SEnter : Control.Event.SLeave);
                }
                if (sensorStatus.ContainsKey(sensor))
                {
                    sensorStatus.Remove(sensor);
                }
                sensorStatus.Add(sensor, sensor.IsActive);
            }
            foreach (IMySensorBlock sensor in config.sensors2)
            {
                bool oldStatus = sensorStatus.GetValueOrDefault(sensor, false);
                if (!oldStatus.Equals(sensor.IsActive) || sensor.IsActive)
                {
                    currentEvents.Add(sensor.IsActive ? Control.Event.OutEnter : Control.Event.OutLeave);
                }
                if (sensorStatus.ContainsKey(sensor))
                {
                    sensorStatus.Remove(sensor);
                }
                sensorStatus.Add(sensor, sensor.IsActive);
            }
            List<IMySensorBlock> sensorsToDelete = new List<IMySensorBlock>();
            foreach (IMySensorBlock sensor in sensorStatus.Keys)
            {
                if (!config.sensors1.Contains(sensor) && !config.sluiceSensors.Contains(sensor) && !config.sensors2.Contains(sensor))
                {
                    sensorsToDelete.Add(sensor);
                }
            }
            foreach (IMySensorBlock sensor in sensorsToDelete)
            {
                sensorStatus.Remove(sensor);
            }
            return currentEvents;
        }

        public static IMyTextPanel searchLcdWithName(Sandbox.ModAPI.Ingame.IMyGridTerminalSystem gridTerminal, String name)
        {
            IMyTextPanel lcd = null;
            IMyTerminalBlock lcdTerminalBlock = gridTerminal.GetBlockWithName(name);
            if (lcdTerminalBlock as IMyTextPanel != null)
            {
                lcd = lcdTerminalBlock as IMyTextPanel;
            }
            return lcd;
        }

    }
}
