using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AirlockRKR
{
    class Control
    {
        public enum Status { Standby, leaveInit, leaveProgress, leavePressurize, leaveSchleuse, enterInit, enterProgress, enterPressurize, enterSchleuse }
        public enum Event { AbortTimerDown, InEnter, InLeave, SEnter, SLeave, OutEnter, OutLeave, NoEvent }

        public static Status run(List<Event> currentEvents, Status progress, Config config)
        {
            Status newStatus = progress;
            if (currentEvents.Contains(Event.AbortTimerDown))
            {
                Utils.closeDoors(config.doors1);
                Utils.closeDoors(config.doors2);
                Utils.setPressureColor(config.lights, config.sluiceVents);
                newStatus = Status.Standby;
            }

            if (currentEvents.Contains(Event.InEnter) && progress.Equals(Status.Standby))
            {
                Utils.setPressureColor(config.lights, config.sluiceVents);
                if (Utils.isOneDoorOpen(config.doors2))
                {
                    Utils.closeDoors(config.doors2);
                }
                else
                {
                    newStatus = Status.leaveInit;
                    progress = Status.leaveInit;
                }
            }
            if (progress.Equals(Status.leaveInit))
            {
                bool sluicePressure = Utils.isPressurized(config.sluiceVents[0]);
                bool pressure1 = config.pressurized1 || (config.vents1.Count > 0 && Utils.isPressurized(config.vents1[0]));
                if (sluicePressure.Equals(pressure1))
                {
                    if (Utils.isOneDoorClosed(config.doors1))
                    {
                        Utils.openDoors(config.doors1);
                    }
                    else
                    {
                        Utils.setPressureColor(config.lights, config.sluiceVents);
                        newStatus = Status.leaveProgress;
                        // hier müssten wir den Abbruch Timer simulieren
                    }
                }
                else if (!sluicePressure && pressure1)
                {
                    // Luftdruck in der Schleuse zu gering 
                    Utils.setColor(config.lights, VRageMath.Color.Yellow);
                    Utils.pressurizeRoom(config.sluiceVents);
                }
                else
                {
                    // Luftdruck in der Schleuse zu hoch 
                    Utils.setColor(config.lights, VRageMath.Color.Yellow);
                    Utils.depressurizeRoom(config.sluiceVents);
                }
            }
            if (currentEvents.Contains(Event.SEnter) && progress.Equals(Status.leaveProgress))
            {
                // hier müssten wir den Abbruch Timer anhalten
                if (Utils.isOneDoorOpen(config.doors1))
                {
                    Utils.closeDoors(config.doors1);
                }
                else
                {
                    newStatus = Status.leavePressurize;
                }
            }
            if (progress.Equals(Status.leavePressurize))
            {
                bool sluicePressure = Utils.isPressurized(config.sluiceVents[0]);
                bool pressure2 = config.pressurized2 || (config.vents2.Count > 0 && Utils.isPressurized(config.vents2[0]));
                if (sluicePressure.Equals(pressure2))
                {
                    if (Utils.isOneDoorClosed(config.doors2))
                    {
                        Utils.openDoors(config.doors2);
                    }
                    else
                    {
                        Utils.setPressureColor(config.lights, config.sluiceVents);
                        newStatus = Status.leaveSchleuse;
                        // hier müssten wir den Abbruch Timer simulieren
                    }
                }
                else if (!sluicePressure && pressure2)
                {
                    // Luftdruck in der Schleuse zu gering
                    Utils.setColor(config.lights, VRageMath.Color.Yellow);
                    Utils.pressurizeRoom(config.sluiceVents);
                }
                else
                {
                    // Luftdruck in der Schleuse zu hoch
                    Utils.setColor(config.lights, VRageMath.Color.Yellow);
                    Utils.depressurizeRoom(config.sluiceVents);
                }
            }
            if (currentEvents.Contains(Event.OutLeave) && progress.Equals(Status.leaveSchleuse))
            {
                Utils.setPressureColor(config.lights, config.sluiceVents);
                // hier müssten wir den Abbruch Timer anhalten
                if (Utils.isOneDoorOpen(config.doors2))
                {
                    Utils.closeDoors(config.doors2);
                }
                newStatus = Status.Standby;
            }
            if (currentEvents.Contains(Event.OutEnter) && progress.Equals(Status.Standby))
            {
                Utils.setPressureColor(config.lights, config.sluiceVents);
                
                if (Utils.isOneDoorOpen(config.doors1))
                {
                    Utils.closeDoors(config.doors1);
                }
                else
                {
                    newStatus = Status.enterInit;
                    progress = Status.enterInit;
                }
            }
            if (progress.Equals(Status.enterInit))
            {
                bool sluicePressure = Utils.isPressurized(config.sluiceVents[0]);
                bool pressure2 = config.pressurized2 || (config.vents2.Count > 0 && Utils.isPressurized(config.vents2[0]));
                if (sluicePressure.Equals(pressure2))
                {
                    if (Utils.isOneDoorClosed(config.doors2))
                    {
                        Utils.openDoors(config.doors2);
                    }
                    else
                    {
                        Utils.setPressureColor(config.lights, config.sluiceVents);
                        newStatus = Status.enterProgress;
                        // hier müssten wir den Abbruch Timer simulieren
                    }
                }
                else if (!sluicePressure && pressure2)
                {
                    // Luftdruck in der Schleuse zu gering  
                    Utils.setColor(config.lights, VRageMath.Color.Yellow);
                    Utils.pressurizeRoom(config.sluiceVents);
                }
                else
                {
                    // Luftdruck in der Schleuse zu hoch  
                    Utils.setColor(config.lights, VRageMath.Color.Yellow);
                    Utils.depressurizeRoom(config.sluiceVents);
                }
            }
            if (currentEvents.Contains(Event.SEnter) && progress.Equals(Status.enterProgress))
            {
                // hier müssten wir den Abbruch Timer anhalten
                if (Utils.isOneDoorOpen(config.doors2))
                {
                    Utils.closeDoors(config.doors2);
                }
                else
                {
                    newStatus = Status.enterPressurize;
                }
            }
            if (progress.Equals(Status.enterPressurize))
            {
                bool sluicePressure = Utils.isPressurized(config.sluiceVents[0]);
                bool pressure1 = config.pressurized1 || (config.vents1.Count > 0 && Utils.isPressurized(config.vents1[0])); ;
                if (sluicePressure.Equals(pressure1))
                {
                    if (Utils.isOneDoorClosed(config.doors1))
                    {
                        Utils.openDoors(config.doors1);
                    }
                    else
                    {
                        Utils.setPressureColor(config.lights, config.sluiceVents);
                        newStatus = Status.enterSchleuse;
                        // hier müssten wir den Abbruch Timer simulieren
                    }
                }
                else if (!sluicePressure && pressure1)
                {
                    // Luftdruck in der Schleuse zu gering 
                    Utils.setColor(config.lights, VRageMath.Color.Yellow);
                    Utils.pressurizeRoom(config.sluiceVents);
                }
                else
                {
                    // Luftdruck in der Schleuse zu hoch 
                    Utils.setColor(config.lights, VRageMath.Color.Yellow);
                    Utils.depressurizeRoom(config.sluiceVents);
                }
            }
            if (currentEvents.Contains(Event.InLeave) && progress.Equals(Status.enterSchleuse))
            {
                Utils.setPressureColor(config.lights, config.sluiceVents);
                // hier müssten wir den Abbruch Timer anhalten
                if (Utils.isOneDoorOpen(config.doors1))
                {
                    Utils.closeDoors(config.doors1);
                }
                newStatus = Status.Standby;
            }
            return newStatus;
        }
    }
}
