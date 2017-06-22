using System;
using System.Collections.Generic;

using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.Definitions;
using Sandbox.Game.World;

using VRageMath;

using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;

namespace Eikester.DigitalClock
{
	
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_AdvancedDoor), new string[] 
		{ 	
			"Eikester_DigitalClock_LB", 
			"Eikester_DigitalClock_SB" 
		}
	)]
	public class DigitalClock : MyGameLogicComponent
	{
        private IMyCubeBlock m_block;
        private IMyDoor m_door;

        private Color COLOROFF = new Color(5, 0, 0);
        private Color COLORON = new Color(255, 0, 0);

        private const float INTENSITYON = 0.8f;
        private const float INTENSITYOFF = 0.1f;

        private int hour = 0;
        private int minute = 0;

        // for now used as a switch between Local and UTC time
        // TODO: make it switch between Local and Ingame time
        private bool showRealTime = true;

        //########################################### 
        //	 000
        //	5   1
        //	 666
        //	4	2
        //	 333	
        //###########################################
        private Dictionary<int, byte[]> digitTable = new Dictionary<int, byte[]>() 
        { 
            {0, new byte[]{1,1,1,1,1,1,0}},
            {1, new byte[]{0,1,1,0,0,0,0}},
            {2, new byte[]{1,1,0,1,1,0,1}},
            {3, new byte[]{1,1,1,1,0,0,1}},
            {4, new byte[]{0,1,1,0,0,1,1}},
            {5, new byte[]{1,0,1,1,0,1,1}},
            {6, new byte[]{1,0,1,1,1,1,1}},
            {7, new byte[]{1,1,1,0,0,0,0}},
            {8, new byte[]{1,1,1,1,1,1,1}},
            {9, new byte[]{1,1,1,1,0,1,1}},
        };

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Container.Entity.GetObjectBuilder(copy);
        }
		
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Container.Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            m_block = (IMyCubeBlock)Entity;
            m_block.IsWorkingChanged += Block_IsWorkingChanged;

            m_door = (IMyDoor)Entity;
            m_door.DoorStateChanged += Block_DoorStateChanged;
        }

        void Block_DoorStateChanged(bool open)
        {
            showRealTime = open;
            SetState(m_block.IsWorking);
        }

        void Block_IsWorkingChanged(IMyCubeBlock obj)
        {
            SetState(obj.IsWorking);
        }

        public override void Close()
        {
            m_block.IsWorkingChanged -= Block_IsWorkingChanged;
            m_door.DoorStateChanged -= Block_DoorStateChanged;

            if (digitTable != null)
            {
                digitTable.Clear();
                digitTable = null;
            }
        }

		public override void UpdateOnceBeforeFrame()
		{
			try
            {
                Container.Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            }
            catch
            {
            }
		}
		
		private void SetState(bool on)
		{
			if(on == false)
			{
				for(int i = 0; i < 7; i++)
				{
					for(int j = 0; j < 2; j++)
					{
                        m_block.SetEmissiveParts(string.Format("Em_H{0}{1}", j, i), COLOROFF, INTENSITYOFF);
                        m_block.SetEmissiveParts(string.Format("Em_M{0}{1}", j, i), COLOROFF, INTENSITYOFF);
					}
				}

                m_block.SetEmissiveParts("Em_Red", COLOROFF, INTENSITYOFF);
                m_block.SetEmissiveParts("Em_Local", COLOROFF, INTENSITYOFF);
                m_block.SetEmissiveParts("Em_UTC", COLOROFF, INTENSITYOFF);

			}
			else
			{
                hour = showRealTime ? DateTime.Now.Hour : DateTime.UtcNow.Hour;
                minute = showRealTime ? DateTime.Now.Minute : DateTime.UtcNow.Minute;

                // padding with zeros so hour and minute is always 2 digits long i.e. 01:06 
                string m = minute.ToString("D2");
                string h = hour.ToString("D2");
				
				for(int i = 0; i < 7; i++)
				{
					for(int j = 0; j < 2; j++)
					{
                        bool b = GetDigitSegmentState(i, h.Substring(j, 1));
                        m_block.SetEmissiveParts(string.Format("Em_H{0}{1}", j, i), b ? COLORON : COLOROFF, b ? INTENSITYON : INTENSITYOFF);

                        b = GetDigitSegmentState(i, m.Substring(j, 1));
                        m_block.SetEmissiveParts(string.Format("Em_M{0}{1}", j, i), b ? COLORON : COLOROFF, b ? INTENSITYON : INTENSITYOFF);
					}
				}

                m_block.SetEmissiveParts("Em_Red", COLORON, INTENSITYON);
                m_block.SetEmissiveParts("Em_Local", showRealTime ? COLORON : COLOROFF, showRealTime ? INTENSITYON : INTENSITYOFF);
                m_block.SetEmissiveParts("Em_UTC", showRealTime ? COLOROFF : COLORON, showRealTime ? INTENSITYOFF : INTENSITYON);
			}
		}

		public override void UpdateBeforeSimulation100()
        {
            SetState(m_block.IsWorking);
        }
		
		private bool GetDigitSegmentState(int part, string digit)
		{
			int val = Int32.Parse(digit);
		
			if(digitTable[val][part] == 1)
				return true;
		
			return false;
		}
	}
}