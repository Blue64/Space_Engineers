using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AirlockRKR
{
    public class Config
    {
        public List<IMyDoor> doors1 { get; private set; }
        public List<IMyDoor> doors2 { get; private set; }
        public List<IMyAirVent> vents1 { get; private set; }
        public List<IMyAirVent> sluiceVents { get; private set; }
        public List<IMyAirVent> vents2 { get; private set; }
        public Boolean pressurized1 { get; set; }
        public Boolean pressurized2 { get; set; }
        public List<IMySensorBlock> sensors1 { get; private set; }
        public List<IMySensorBlock> sluiceSensors { get; private set; }
        public List<IMySensorBlock> sensors2 { get; private set; }
        public List<IMyInteriorLight> lights { get; private set; }
        public IMyTextPanel statusPanel { get; set; }
        public long abortMilliseconds { get; set; }

        public Config()
        {
            doors1 = new List<IMyDoor>();
            doors2 = new List<IMyDoor>();
            vents1 = new List<IMyAirVent>();
            sluiceVents = new List<IMyAirVent>();
            vents2 = new List<IMyAirVent>();
            sensors1 = new List<IMySensorBlock>();
            sluiceSensors = new List<IMySensorBlock>();
            sensors2 = new List<IMySensorBlock>();
            lights = new List<IMyInteriorLight>();
            statusPanel = null;
            abortMilliseconds = 10000L;
        }

        public Boolean isValid() {
            Boolean check = true;
            check &= isDoors1Valid(); ;
            check &= isDoors2Valid();
            check &= isVentsValid();
            check &= isSluiceVentsValid();
            check &= isSensors1Valid();
            check &= isSluiceSensorsValid();
            check &= isSensors2Valid();
            return check;
        }

        public Boolean isDoors1Valid()
        {
            return doors1.Count() >= 1;
        }

        public Boolean isDoors2Valid()
        {
            return doors2.Count() >= 1;
        }

        public Boolean isVentsValid()
        {
            return (vents1.Count() >= 1) || pressurized1 || pressurized2 || (vents2.Count() >= 1);
        }

        public Boolean isSluiceVentsValid()
        {
            return sluiceVents.Count() >= 1;
        }

        public Boolean isSensors1Valid()
        {
            return sensors1.Count() >= 1;
        }

        public Boolean isSluiceSensorsValid()
        {
            return sluiceSensors.Count() >= 1;
        }

        public Boolean isSensors2Valid()
        {
            return sensors2.Count() >= 1;
        }

        public Boolean isStatusPanelValid()
        {
            return this.statusPanel != null;
        }

        internal bool lightsFound()
        {
            return lights.Count() >= 1;
        }
    }
}
