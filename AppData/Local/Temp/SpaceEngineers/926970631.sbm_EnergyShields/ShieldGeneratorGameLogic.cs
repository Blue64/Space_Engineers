using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Engine.Physics;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Common.Utils;
using VRageMath;
using VRage;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using Sandbox.Game.EntityComponents;
using VRage.Game.Components;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Utils;
using VRage.Game.Entity.UseObject;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Gui;

namespace Cython.EnergyShields
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_Refinery), false, "LargeShipSmallShieldGeneratorBase", "SmallShipSmallShieldGeneratorBase", "SmallShipMicroShieldGeneratorBase", "LargeShipLargeShieldGeneratorBase")] 
	public class ShieldGeneratorGameLogic: MyGameLogicComponent
	{
		enum ShieldType 
		{
			LargeBlockSmall,
			LargeBlockLarge,
			SmallBlockSmall,
			SmallBlockLarge
		}
		
		MyObjectBuilder_EntityBase m_objectBuilder;

		MyResourceSinkComponent m_resourceSink;

		ShieldMessage saveFileShieldMessage = new ShieldMessage();

		long m_ticks = 0;
		public long m_ticksUntilRecharge = 0;

		public float m_currentShieldPoints = 0f;
		public float m_maximumShieldPoints;

		float m_rechargeMultiplier;
		float m_pointsToRecharge;
		float m_currentPowerConsumption;

		float m_setPowerConsumption;

		ShieldType m_shieldType;

		bool m_closed = false;

        readonly int m_overchargeTimeout = 180;
        int m_overchargedTicks = 0;

		public override void Init (MyObjectBuilder_EntityBase objectBuilder)
		{
			m_objectBuilder = objectBuilder;

			NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.BEFORE_NEXT_FRAME;

			m_shieldType = getShieldType ();

			IMyCubeBlock cubeBlock = Entity as IMyCubeBlock;


			cubeBlock.AddUpgradeValue ("ShieldPoints", 1f);
			cubeBlock.AddUpgradeValue ("ShieldRecharge", 1f);
			cubeBlock.AddUpgradeValue ("PowerConsumption", 1f);

			Sandbox.ModAPI.IMyTerminalBlock terminalBlock = Entity as Sandbox.ModAPI.IMyTerminalBlock;

			terminalBlock.AppendingCustomInfo += UpdateBlockInfo;
		}
			

		public override MyObjectBuilder_EntityBase GetObjectBuilder (bool copy = false)
		{
			if (copy) 
			{
				return (MyObjectBuilder_EntityBase) m_objectBuilder.Clone ();
			} 
			else 
			{
				return m_objectBuilder;
			}
		}

		public override void UpdateOnceBeforeFrame ()
		{
			m_resourceSink = Entity.Components.Get<MyResourceSinkComponent> ();

			if(!EnergyShieldsCore.shieldGenerators.ContainsKey(Entity.EntityId)) {
				EnergyShieldsCore.shieldGenerators.Add(Entity.EntityId, this);
			}
		}

		public override void UpdateBeforeSimulation ()
		{
			calculateMaximumShieldPoints ();
			calculateShieldPointsRecharge ();

           // MyAPIGateway.Utilities.ShowNotification("Value: " + m_currentShieldPoints, 20000);
			/*
			if ((m_ticks % EnergyShieldsCore.Config.GeneralSettings.StatusUpdateInterval) == 0)
			{
				
				Sandbox.ModAPI.IMyTerminalBlock terminalBlock = Entity as Sandbox.ModAPI.IMyTerminalBlock;
				terminalBlock.RefreshCustomInfo ();
				printShieldStatus();


				MyAPIGateway.Utilities.ShowNotification("Gen: " + Entity.EntityId, 150);

			}
			*/


			if ((m_ticks % 100) == 0)
			{
				if(MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Multiplayer.IsServer)
				{
					ShieldMessage shieldMessage = new ShieldMessage(Entity.EntityId, m_currentShieldPoints);

					byte[] message = new byte[12];
					byte[] messageID = BitConverter.GetBytes(Entity.EntityId);
					byte[] messageValue = BitConverter.GetBytes(m_currentShieldPoints);

					for(int i = 0; i < 8; i++) {
						message[i] = messageID[i];
					}

					for(int i = 0; i < 4; i++) {
						message[i+8] = messageValue[i];
					}

					MyAPIGateway.Multiplayer.SendMessageToOthers (5854, message, true);
					//Log.writeLine("<ShieldGeneratorGameLogic> Sync sent.");
				}
			}

			saveFileShieldMessage.value = m_currentShieldPoints;

			MyCubeBlock cube = Entity as MyCubeBlock;
		}

		public void UpdatePrintBalanced()
		{
			if(!m_closed && Entity.InScene)
			{
				Sandbox.ModAPI.IMyTerminalBlock terminalBlock = Entity as Sandbox.ModAPI.IMyTerminalBlock;
				terminalBlock.RefreshCustomInfo ();
				printShieldStatus();
			}
		}

		public void UpdateNetworkBalanced()
		{
			if(!m_closed && Entity.InScene)
			{
				
				if(MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Multiplayer.IsServer)
				{
					ShieldMessage shieldMessage = new ShieldMessage(Entity.EntityId, m_currentShieldPoints);

					byte[] message = new byte[12];
					byte[] messageID = BitConverter.GetBytes(Entity.EntityId);
					byte[] messageValue = BitConverter.GetBytes(m_currentShieldPoints);

					for(int i = 0; i < 8; i++) {
						message[i] = messageID[i];
					}

					for(int i = 0; i < 4; i++) {
						message[i+8] = messageValue[i];
					}

					MyAPIGateway.Multiplayer.SendMessageToOthers (5854, message, true);
					//Log.writeLine("<ShieldGeneratorGameLogic> Sync sent.");
				}

			}
		}

		public override void UpdateAfterSimulation ()
		{
			if(m_ticks == 0) 
			{
				bool contains = false;

				foreach(var shieldMessage in EnergyShieldsCore.shieldSaveFile.ShieldGenerators) 
				{
					if(shieldMessage.entityID == Entity.EntityId)
					{
						contains = true;

						saveFileShieldMessage = shieldMessage;
					}
				}

				if(!contains)
				{
					saveFileShieldMessage = new ShieldMessage(Entity.EntityId, m_currentShieldPoints);
					EnergyShieldsCore.shieldSaveFile.ShieldGenerators.Add(saveFileShieldMessage);
				}
			}

			m_ticks++;
		}

		public override void OnAddedToScene ()
		{
			foreach(var shieldStatus in EnergyShieldsCore.shieldSaveFile.ShieldGenerators)
			{
				if(Entity.EntityId == shieldStatus.entityID)
				{
					m_currentShieldPoints = shieldStatus.value;
				}
			}
		}

		public override void OnRemovedFromScene ()
		{
			foreach(var shieldStatus in EnergyShieldsCore.shieldSaveFile.ShieldGenerators)
			{
				if(Entity.EntityId == shieldStatus.entityID)
				{
					shieldStatus.value = m_currentShieldPoints;
				}
			}
		}

		public override void Close ()
		{
			m_closed = true;
			
			Sandbox.ModAPI.IMyTerminalBlock terminalBlock = Entity as Sandbox.ModAPI.IMyTerminalBlock;

			terminalBlock.AppendingCustomInfo -= UpdateBlockInfo;

			if(EnergyShieldsCore.shieldSaveFile.ShieldGenerators.Contains(saveFileShieldMessage)) 
			{
				EnergyShieldsCore.shieldSaveFile.ShieldGenerators.Remove(saveFileShieldMessage);
			}

			if(EnergyShieldsCore.shieldGenerators.ContainsKey(Entity.EntityId)) {
				EnergyShieldsCore.shieldGenerators.Remove(Entity.EntityId);
			}


		}

		ShieldType getShieldType() {

			string subtypeID = (Entity as IMyCubeBlock).BlockDefinition.SubtypeId;

			if (subtypeID == "LargeShipSmallShieldGeneratorBase") 
			{
				return ShieldType.LargeBlockSmall;
			} 
			else if (subtypeID == "LargeShipLargeShieldGeneratorBase") 
			{
				return ShieldType.LargeBlockLarge;
			} 
			if (subtypeID == "SmallShipMicroShieldGeneratorBase") 
			{
				return ShieldType.SmallBlockSmall;
			} 
			else if (subtypeID == "SmallShipSmallShieldGeneratorBase") 
			{
				return ShieldType.SmallBlockLarge;
			} 
			else 
			{
				return ShieldType.LargeBlockSmall;
			}

		}

		ShieldDefinition getShieldDefinition() 
		{
			ShieldType type = m_shieldType;

			if (type == ShieldType.LargeBlockSmall) 
			{
				return EnergyShieldsCore.Config.LargeBlockSmallGenerator;
			} 
			else if (type == ShieldType.LargeBlockLarge) 
			{
				return EnergyShieldsCore.Config.LargeBlockLargeGenerator;
			} 
			else if (type == ShieldType.SmallBlockSmall) 
			{
				return EnergyShieldsCore.Config.SmallBlockSmallGenerator;
			} 
			else if (type == ShieldType.SmallBlockLarge) 
			{
				return EnergyShieldsCore.Config.SmallBlockLargeGenerator;
			} 
			else 
			{
				return EnergyShieldsCore.Config.LargeBlockSmallGenerator;
			}
				
		}

		public void UpdateBlockInfo(Sandbox.ModAPI.IMyTerminalBlock block, StringBuilder info)
		{
			info.Clear ();
			info.AppendLine (" ");

			IMyCubeBlock cubeBlock = Entity as IMyCubeBlock;

			IMyGridTerminalSystem tsystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid (cubeBlock.CubeGrid as IMyCubeGrid);


			if (tsystem != null) {

				List<IMyTerminalBlock> shieldsConnected = new List<IMyTerminalBlock> (); 

				tsystem.GetBlocksOfType<IMyRefinery> (shieldsConnected, EnergyShieldsCore.shieldFilter);

				float shipCurrentShieldPoints = 0f;
				float shipMaximumShieldPoints = 0f;

				foreach (var shield in shieldsConnected) 
				{
					ShieldGeneratorGameLogic generatorLogic = ((IMyTerminalBlock)shield).GameLogic.GetAs<ShieldGeneratorGameLogic> ();

					shipCurrentShieldPoints += generatorLogic.m_currentShieldPoints;
					shipMaximumShieldPoints += generatorLogic.m_maximumShieldPoints;

				}

				info.Append("Ship Shield: ");
				MyValueFormatter.AppendGenericInBestUnit (shipCurrentShieldPoints, info);
				info.Append("Pt/");
				MyValueFormatter.AppendGenericInBestUnit (shipMaximumShieldPoints, info);
				info.Append("Pt\n");


			}

			info.Append("Local Shield: ");
			MyValueFormatter.AppendGenericInBestUnit (m_currentShieldPoints, info);
			info.Append("Pt/");
			MyValueFormatter.AppendGenericInBestUnit (m_maximumShieldPoints, info);
			info.Append("Pt\n");

			info.Append("Recharge: ");
			MyValueFormatter.AppendGenericInBestUnit (m_pointsToRecharge * 60, info);
			info.Append("Pt/s ");
			if(EnergyShieldsCore.Config.AlternativeRechargeMode.Enable && (m_ticksUntilRecharge > 0)) {
				info.Append("(" + (int)Math.Ceiling(m_ticksUntilRecharge/60d) + "s)\n");
			} else {
				info.Append("\n");
			}
			
			info.Append("Effectivity: ");
			MyValueFormatter.AppendWorkInBestUnit (m_currentPowerConsumption, info);
			info.Append ("/");
			MyValueFormatter.AppendWorkInBestUnit (m_setPowerConsumption, info);
			info.Append (" (" + (m_rechargeMultiplier * 100).ToString("N") + "%)");
		}

		private void printShieldStatus() {

			if (MyAPIGateway.Multiplayer.IsServer) 
			{
				if (Entity.InScene) 
				{
					Sandbox.ModAPI.IMyFunctionalBlock funcBlock = Entity as Sandbox.ModAPI.IMyFunctionalBlock;

					if (funcBlock.CustomName.Contains (":")) 
					{
						String name = funcBlock.CustomName;
						long gridID = funcBlock.CubeGrid.EntityId;
						int index = name.IndexOf (':');

						if ((index + 1) < name.Length) 
						{
							name = name.Remove (index + 1);
						}

						IMyGridTerminalSystem tsystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid (funcBlock.CubeGrid as IMyCubeGrid);
						List<IMyTerminalBlock> shieldsConnected = new List<IMyTerminalBlock> ();

						if (tsystem != null) 
						{
							tsystem.GetBlocksOfType<Sandbox.ModAPI.Ingame.IMyRefinery> (shieldsConnected, EnergyShieldsCore.shieldFilter);

							float shipCurrentShieldPoints = 0f;
							float shipMaximumShieldPoints = 0f;

							foreach (var shield in shieldsConnected) 
							{
								ShieldGeneratorGameLogic generatorLogic = ((IMyTerminalBlock)shield).GameLogic.GetAs<ShieldGeneratorGameLogic> ();

								shipCurrentShieldPoints += generatorLogic.m_currentShieldPoints;
								shipMaximumShieldPoints += generatorLogic.m_maximumShieldPoints;
							}

							name = name + " (" + Math.Round (shipCurrentShieldPoints) + "/" +
								Math.Round (shipMaximumShieldPoints) + ")";
							funcBlock.SetCustomName (name);
						} 
						else 
						{
							name = name + " (" + Math.Round (m_currentShieldPoints) + "/" +
								Math.Round (m_maximumShieldPoints) + ")";
							funcBlock.SetCustomName (name);
						}
					}
				}
				
			}
		}

		void calculateMaximumShieldPoints() 
		{
			IMyCubeBlock cubeBlock = Entity as IMyCubeBlock;

			m_maximumShieldPoints = getShieldDefinition ().Points * cubeBlock.UpgradeValues ["ShieldPoints"] * EnergyShieldsCore.Config.UpgradeModuleMultiplier.PointMultiplier;
		}

		void calculateShieldPointsRecharge()
		{
			if (EnergyShieldsCore.Config.AlternativeRechargeMode.Enable) 
			{
				if(!(Entity as Sandbox.ModAPI.IMyFunctionalBlock).IsFunctional) 
				{
					if (m_setPowerConsumption != 0.000000f) 
					{
						m_resourceSink.SetRequiredInputByType (MyResourceDistributorComponent.ElectricityId, 0.000000f);
						m_setPowerConsumption = 0.000000f;

						m_pointsToRecharge = 0f;
						m_currentShieldPoints = 0f;
					}
				}
				else if(!(Entity as Sandbox.ModAPI.IMyFunctionalBlock).Enabled)
				{
					if (m_setPowerConsumption != 0.000000f) 
					{
						m_resourceSink.SetRequiredInputByType (MyResourceDistributorComponent.ElectricityId, 0.000000f);
						m_setPowerConsumption = 0.000000f;

						m_pointsToRecharge = 0f;
					}

					m_currentPowerConsumption = m_resourceSink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);

					/*
					if (!m_resourceSink.IsPoweredByType (MyResourceDistributorComponent.ElectricityId)) 
					{
						m_currentShieldPoints = 0f;
					}
					*/

				}
				else if (m_maximumShieldPoints == m_currentShieldPoints) 
				{
					if (m_setPowerConsumption != 0.00001f) 
					{
						m_resourceSink.SetRequiredInputByType (MyResourceDistributorComponent.ElectricityId, 0.00001f);
						m_setPowerConsumption = 0.00001f;

						m_pointsToRecharge = 0f;
					}

					m_currentPowerConsumption = m_resourceSink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);

					/*
					if (!m_resourceSink.IsPoweredByType (MyResourceDistributorComponent.ElectricityId)) 
					{
						m_currentShieldPoints = 0f;
					}
					*/
				}
				else
				{
					IMyCubeBlock cubeBlock = Entity as IMyCubeBlock;

					float powerConsumption = getShieldDefinition ().PowerConsumption * cubeBlock.UpgradeValues ["PowerConsumption"] * EnergyShieldsCore.Config.UpgradeModuleMultiplier.PowerMultiplier;
					m_resourceSink.MaxRequiredInput = powerConsumption;

					if (powerConsumption != m_setPowerConsumption) 
					{
						m_resourceSink.SetRequiredInputByType (MyResourceDistributorComponent.ElectricityId, powerConsumption);
						m_setPowerConsumption = powerConsumption;
					}

					m_currentPowerConsumption = m_resourceSink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
					m_rechargeMultiplier = m_currentPowerConsumption / powerConsumption;

					m_pointsToRecharge = getShieldDefinition ().RechargeAtPeak * cubeBlock.UpgradeValues ["ShieldRecharge"]
					                        * EnergyShieldsCore.Config.UpgradeModuleMultiplier.RechargeMultiplier * EnergyShieldsCore.Config.AlternativeRechargeMode.RechargeMultiplier * m_rechargeMultiplier;

					if (m_ticksUntilRecharge > 0) 
					{
						m_ticksUntilRecharge--;

						m_pointsToRecharge = 0;
					} 
					else 
					{
                        if (m_maximumShieldPoints > m_currentShieldPoints)
                        {
                            m_currentShieldPoints += m_pointsToRecharge;
                        }

                    }

					if (m_maximumShieldPoints < m_currentShieldPoints) 
					{
                        if(m_overchargedTicks >= m_overchargeTimeout)
                        {
                            m_currentShieldPoints = m_maximumShieldPoints;
                        }
                        m_overchargedTicks++;
					}
                    else
                    {
                        m_overchargedTicks = 0;
                    }

					/*
					if (!m_resourceSink.IsPoweredByType (MyResourceDistributorComponent.ElectricityId)) {
						m_currentShieldPoints = 0f;
					}
					*/
				}
			}
			else
			{
				if(!(Entity as Sandbox.ModAPI.IMyFunctionalBlock).IsFunctional) 
				{
					if (m_setPowerConsumption != 0.000000f) 
					{
						m_resourceSink.SetRequiredInputByType (MyResourceDistributorComponent.ElectricityId, 0.000000f);
						m_setPowerConsumption = 0.000000f;

						m_pointsToRecharge = 0f;
						m_currentShieldPoints = 0f;
					}
				}
				else if(!(Entity as Sandbox.ModAPI.IMyFunctionalBlock).Enabled) 
				{
					if (m_setPowerConsumption != 0.000000f) 
					{
						m_resourceSink.SetRequiredInputByType (MyResourceDistributorComponent.ElectricityId, 0.000000f);
						m_setPowerConsumption = 0.000000f;

						m_pointsToRecharge = 0f;
					}

					m_currentPowerConsumption = m_resourceSink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);

					/*
					if (!m_resourceSink.IsPoweredByType (MyResourceDistributorComponent.ElectricityId)) 
					{
						m_currentShieldPoints = 0f;
					}
					*/

				} 
				else if (m_maximumShieldPoints == m_currentShieldPoints) 
				{
					if (m_setPowerConsumption != 0.00001f) 
					{
						m_resourceSink.SetRequiredInputByType (MyResourceDistributorComponent.ElectricityId, 0.00001f);
						m_setPowerConsumption = 0.00001f;

						m_pointsToRecharge = 0f;
					}

					m_currentPowerConsumption = m_resourceSink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);

					/*
					if (!m_resourceSink.IsPoweredByType (MyResourceDistributorComponent.ElectricityId)) 
					{
						m_currentShieldPoints = 0f;
					}
					*/
				}
				else
				{
					float multiplier = (m_currentShieldPoints / m_maximumShieldPoints);

					if (multiplier > 0.99999f) {
						multiplier = 0.00001f;
					} else if ((multiplier > 0.9f) || (multiplier < 0.08f)) {
						multiplier = 0.1f;
					} else if ((multiplier > 0.7f) || (multiplier < 0.12f)) {
						multiplier = 0.2f;
					} else if ((multiplier > 0.5f) || (multiplier < 0.18f)) {
						multiplier = 0.4f;
					} else if ((multiplier > 0.35f) || (multiplier < 0.22f)) {
						multiplier = 0.7f;
					} else {
						multiplier = 1.0f;
					}

					IMyCubeBlock cubeBlock = Entity as IMyCubeBlock;

					float powerConsumption = getShieldDefinition ().PowerConsumption * cubeBlock.UpgradeValues ["PowerConsumption"] * EnergyShieldsCore.Config.UpgradeModuleMultiplier.PowerMultiplier * multiplier;
					m_resourceSink.MaxRequiredInput = powerConsumption;

					if (powerConsumption != m_setPowerConsumption) 
					{
						m_resourceSink.SetRequiredInputByType (MyResourceDistributorComponent.ElectricityId, powerConsumption);
						m_setPowerConsumption = powerConsumption;
					}

					m_currentPowerConsumption = m_resourceSink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
					m_rechargeMultiplier = m_currentPowerConsumption / powerConsumption;

					m_pointsToRecharge = getShieldDefinition ().RechargeAtPeak * cubeBlock.UpgradeValues ["ShieldRecharge"]
						* EnergyShieldsCore.Config.UpgradeModuleMultiplier.RechargeMultiplier * EnergyShieldsCore.Config.AlternativeRechargeMode.RechargeMultiplier * m_rechargeMultiplier * multiplier;
					
					m_currentShieldPoints += m_pointsToRecharge;

                    if (m_maximumShieldPoints < m_currentShieldPoints)
                    {
                        m_currentShieldPoints = m_maximumShieldPoints;
                    }

                    if (m_maximumShieldPoints < m_currentShieldPoints)
                    {
                        if (m_overchargedTicks >= m_overchargeTimeout)
                        {
                            m_currentShieldPoints = m_maximumShieldPoints;
                        }
                        m_overchargedTicks++;
                    }
                    else
                    {
                        m_overchargedTicks = 0;
                    }

                    /*
					if (!m_resourceSink.IsPoweredByType (MyResourceDistributorComponent.ElectricityId)) {
						m_currentShieldPoints = 0f;
					}
					*/
                }
			}
		}
	}
}

