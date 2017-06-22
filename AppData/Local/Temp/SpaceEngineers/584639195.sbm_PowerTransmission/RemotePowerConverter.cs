
using Sandbox.Common.ObjectBuilders;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using Sandbox.Game.EntityComponents;
using System.Collections.Generic;
using VRage.ModAPI;
using System.Text;
using System;
using VRageMath;
using VRage.Game.Components;
using VRage.Game;
using VRage;
using Sandbox.ModAPI.Ingame;

namespace Cython.PowerTransmission
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_Reactor), "LargeBlockSmallRemotePowerConverter", "SmallBlockSmallRemotePowerConverter")] 
	public class RemotePowerConverter: MyGameLogicComponent
	{
		MyObjectBuilder_EntityBase m_objectBuilder;

		bool m_running = false;
		bool m_runningOld = false;

		float m_oldMultiplier = -1f;

		Sandbox.ModAPI.IMyFunctionalBlock m_functionalBlock;
		Sandbox.ModAPI.IMyReactor m_reactor;


		private SerializableDefinitionId m_remoteEnergyId;
		MyObjectBuilder_PhysicalObject m_remoteEnergyBuilder;

		IMyInventory m_inventory;

		public override void Init (MyObjectBuilder_EntityBase objectBuilder)
		{
			base.Init (objectBuilder);

			m_objectBuilder = objectBuilder;

			Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
		}

		public override MyObjectBuilder_EntityBase GetObjectBuilder (bool copy = false)
		{
			return m_objectBuilder;
		}

		public override void UpdateOnceBeforeFrame ()
		{
			
			m_functionalBlock = Entity as Sandbox.ModAPI.IMyFunctionalBlock;

			m_inventory = ((Sandbox.ModAPI.Ingame.IMyTerminalBlock)Entity).GetInventory(0) as IMyInventory;

			m_remoteEnergyBuilder = new MyObjectBuilder_Ingot() { SubtypeName = "RemoteEnergy" };

			m_reactor = Entity as Sandbox.ModAPI.IMyReactor;

			m_remoteEnergyId = new SerializableDefinitionId (typeof(MyObjectBuilder_Ingot), "RemoteEnergy");
		}

		public override void OnAddedToScene ()
		{
			base.OnAddedToScene ();

			m_functionalBlock = Entity as Sandbox.ModAPI.IMyFunctionalBlock;

			m_inventory = ((Sandbox.ModAPI.IMyTerminalBlock)Entity).GetInventory (0) as IMyInventory;

			m_remoteEnergyBuilder = new MyObjectBuilder_Ingot() { SubtypeName = "RemoteEnergy" };

			m_reactor = Entity as Sandbox.ModAPI.IMyReactor;

			m_remoteEnergyId = new SerializableDefinitionId (typeof(MyObjectBuilder_Ingot), "RemoteEnergy");
		}

		public override void OnRemovedFromScene ()
		{

			if (m_inventory != null) {
				m_inventory.RemoveItemsOfType (m_inventory.GetItemAmount (m_remoteEnergyId), m_remoteEnergyBuilder);
			}

			base.OnRemovedFromScene ();
		}

		public override void UpdateAfterSimulation ()
		{
			float gridPower;

			if (TransmissionManager.totalPowerPerGrid.TryGetValue (m_functionalBlock.CubeGrid.EntityId, out gridPower)) {
				
				m_running = gridPower > 0f;

				if (gridPower != m_oldMultiplier) {

					m_reactor.PowerOutputMultiplier = gridPower;

					m_oldMultiplier = gridPower;
				}

				//MyAPIGateway.Utilities.ShowNotification ("Grid " + Entity.EntityId + m_functionalBlock.CubeGrid.EntityId + ":" + gridPower, 17, MyFontEnum.Green);

				TransmissionManager.totalPowerPerGrid[m_functionalBlock.CubeGrid.EntityId] = 0f;

			} else {
				m_running = false;
			}
			//MyAPIGateway.Utilities.ShowNotification ("" + m_running, 17, MyFontEnum.Green);
			if ((m_running == true) && (m_running != m_runningOld)) {

				m_inventory.AddItems ((MyFixedPoint)1000, m_remoteEnergyBuilder);

			} else if ((m_running == false) && (m_running != m_runningOld)) {

				m_inventory.RemoveItemsOfType (m_inventory.GetItemAmount (m_remoteEnergyId), m_remoteEnergyBuilder);
			}


			if (m_running) {
				m_reactor.RequestEnable (true);
			} else {
				m_reactor.RequestEnable (false);
			}

			m_runningOld = m_running;
			
		}

		public override void UpdateBeforeSimulation100 ()
		{
			if (m_running) {
				m_inventory.AddItems (((MyFixedPoint)1000) - m_inventory.GetItemAmount (m_remoteEnergyId), m_remoteEnergyBuilder);
			}

			base.UpdateBeforeSimulation100 ();
		}

		public override void Close ()
		{
			if (m_inventory != null) {
				m_inventory.RemoveItemsOfType (m_inventory.GetItemAmount (m_remoteEnergyId), m_remoteEnergyBuilder);
			}

			base.Close ();
		}

		public override void MarkForClose ()
		{
			if (m_inventory != null) {
				m_inventory.RemoveItemsOfType (m_inventory.GetItemAmount (m_remoteEnergyId), m_remoteEnergyBuilder);
			}

			base.MarkForClose ();
		}
	}
}

