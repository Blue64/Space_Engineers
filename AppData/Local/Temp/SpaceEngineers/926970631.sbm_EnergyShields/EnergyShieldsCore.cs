using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRageMath;
using VRage;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Game.Components;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Cython.EnergyShields
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation | MyUpdateOrder.Simulation)]
	public class EnergyShieldsCore: MySessionComponentBase
	{
		public static Configuration Config = new Configuration ();
		public static ShieldSaveFile shieldSaveFile = new ShieldSaveFile();
		public static List<long> trackedSeats = new List<long>();
		public static IMyHudNotification shieldNotification = null;

		public static Dictionary<long, ShieldGeneratorGameLogic> shieldGenerators = new Dictionary<long, ShieldGeneratorGameLogic>();

		public static List<ShieldRenderer> shieldRenderers = new List<ShieldRenderer>();
		public static List<byte[]> effectSyncMessages = new List<byte[]>();

		public static readonly int MAX_CACHED_SYNC_MESSAGES = 40;

		public static MyStringHash damageTypeBullet = MyStringHash.GetOrCompute ("Bullet");
		public static MyStringHash damageTypeDeformation = MyStringHash.GetOrCompute ("Deformation");
		public static MyStringHash damageTypeExplosion = MyStringHash.GetOrCompute ("Explosion");
		public static MyStringHash damageTypeGrinder = MyStringHash.GetOrCompute ("Grind");
		public static MyStringHash damageTypeDrill = MyStringHash.GetOrCompute ("Drill");
		public static MyStringHash damageTypeIgnoreShields = MyStringHash.GetOrCompute ("IgnoreShields");
		public static MyStringHash damageTypeBlockedByShields = MyStringHash.GetOrCompute ("BlockedByShields");

		private PrintLoadBalancer printLoadBalancer = new PrintLoadBalancer();
		private NetworkLoadBalancer networkLoadBalancer = new NetworkLoadBalancer();

		MyObjectBuilder_SessionComponent m_objectBuilder;

		private Dictionary<long, List<ShieldGeneratorGameLogic>> m_cachedGrids = new Dictionary<long, List<ShieldGeneratorGameLogic>>();

		// shield effect grid
		private static SerializableVector3 shieldGridVelocity = new SerializableVector3(0,0,0);
		private static SerializableVector3I shieldGridPosition = new SerializableVector3I(0,0,0);
		private static SerializableBlockOrientation shieldOrientation = new SerializableBlockOrientation(Base6Directions.Direction.Forward, Base6Directions.Direction.Up);

		public static MyObjectBuilder_CubeGrid shieldEffectLargeObjectBuilder = new MyObjectBuilder_CubeGrid () {
			EntityId = 0,
			GridSizeEnum = MyCubeSize.Large,
			IsStatic = true,
			Skeleton = new List<BoneInfo> (),
			LinearVelocity = shieldGridVelocity,
			AngularVelocity = shieldGridVelocity,
			ConveyorLines = new List<MyObjectBuilder_ConveyorLine> (),
			BlockGroups = new List<MyObjectBuilder_BlockGroup> (),
			Handbrake = false,
			XMirroxPlane = null,
			YMirroxPlane = null,
			ZMirroxPlane = null,
			PersistentFlags = MyPersistentEntityFlags2.InScene,
			Name = "",
			DisplayName = "",
			CreatePhysics = false,
			PositionAndOrientation = new MyPositionAndOrientation (Vector3D.Zero, Vector3D.Forward, Vector3D.Up),
			CubeBlocks = new List<MyObjectBuilder_CubeBlock> () {
				new MyObjectBuilder_CubeBlock () {
					EntityId = 1,
					SubtypeName = "",
					Min = shieldGridPosition,
					BlockOrientation = shieldOrientation,
					ShareMode = MyOwnershipShareModeEnum.None,
					DeformationRatio = 0,
				}
			}
		};



		readonly string m_configName = "EnergyShieldsRelease.cfg";
		readonly string m_shieldSaveFileName = "EnergyShields.dat";
		
		long m_ticks = 0;
		bool m_init = false;

		public override void Init (MyObjectBuilder_SessionComponent sessionComponent)
		{
			m_objectBuilder = sessionComponent;
		}

		public override void UpdateBeforeSimulation ()
		{
			if (!m_init)
			{
				init ();
			} 
			else
			{
				if((m_ticks % EnergyShieldsCore.Config.GeneralSettings.StatusUpdateInterval) == 0)
				{
					showShieldOnHud();
				}

				m_cachedGrids.Clear();
			}

			updateShieldRenderers();

			printLoadBalancer.Update();
			networkLoadBalancer.Update();

            /*
			Random random = new Random();
            

            
			//MyVisualScriptLogicProvider.FogSetDensity(0.5f);
			MyVisualScriptLogicProvider.FogSetMultiplier(1f);

			for(int i = 0; i < 20; i++) {
				
				Vector3 addedVector = new Vector3((((float)random.NextDouble()) - 0.5f) * 30f, (((float)random.NextDouble()) - 0.5f) * 30f, (((float)random.NextDouble()) - 0.5f) * 30f);

				//MyVisualScriptLogicProvider.CreateParticleEffectAtPosition("WheelDust_MarsSoil", MyAPIGateway.Session.Player.GetPosition() + addedVector);

				MyParticleEffect effect;

				if(!MyParticlesManager.TryCreateParticleEffect("WheelDust_MarsSoil", out effect)) {
					return;
				}

				MatrixD matrix = new MatrixD();
				matrix.Translation = MyAPIGateway.Session.Player.GetPosition() + addedVector;
				effect.WorldMatrix = MatrixD.CreateWorld(MyAPIGateway.Session.Player.GetPosition() + addedVector + new Vector3(0f, 0f, -15f), new Vector3(1f, 0f, 0f), Vector3.CalculatePerpendicularVector(new Vector3(1f, 0f, 0f)));
				effect.UserScale = 8f;
				//effect.CalculateDeltaMatrix = true;
				
				//effect.Velocity = new Vector3(1f, 1f, 0f);

			}
            */
			m_ticks++;
		}

		public override void UpdateAfterSimulation ()
		{
			
			if(MyAPIGateway.Multiplayer.IsServer)
			{
				if(EnergyShieldsCore.effectSyncMessages.Count > 0) 
				{
					byte[] message = new byte[1];
					//Log.writeLine("<EnergyShieldsCore> " + EnergyShieldsCore.effectSyncMessages.Count + " effect syncs waiting.");

					for(int messagesSent = 0; messagesSent <= EnergyShieldsCore.effectSyncMessages.Count; messagesSent++)
					{
						int messagesSentLocal = (messagesSent % EnergyShieldsCore.MAX_CACHED_SYNC_MESSAGES);

						if((messagesSentLocal == 0) && (messagesSent != 0))
						{
							//Log.writeLine("<EnergyShieldsCore> Effect sync bulk with " + EnergyShieldsCore.MAX_CACHED_SYNC_MESSAGES + "/" + EnergyShieldsCore.effectSyncMessages.Count +  " messages sent. (" + message.Length + " bytes)");

							MyAPIGateway.Multiplayer.SendMessageToOthers(5855, message, true);
						}

						if(messagesSent != EnergyShieldsCore.effectSyncMessages.Count)
						{

							if(messagesSentLocal == 0)
							{
								message = new byte[25 * Math.Min(EnergyShieldsCore.MAX_CACHED_SYNC_MESSAGES, EnergyShieldsCore.effectSyncMessages.Count - messagesSent) + 4];
								byte[] messageSize = BitConverter.GetBytes(Math.Min(EnergyShieldsCore.MAX_CACHED_SYNC_MESSAGES, EnergyShieldsCore.effectSyncMessages.Count - messagesSent));

								for(int i = 0; i < 4; i++)
								{
									message[i] = messageSize[i];
								}
							}
								
							byte[] messageToSend = EnergyShieldsCore.effectSyncMessages.ElementAt(messagesSent);

							for(int i = 0; i < 25; i++)
							{
								message[(messagesSentLocal*25) + i + 4] = messageToSend[i];
							}

						} 
						else 
						{
							//Log.writeLine("<EnergyShieldsCore> Effect sync bulk with " + messagesSentLocal + "/" + EnergyShieldsCore.effectSyncMessages.Count +  " messages sent. (" + message.Length + " bytes)");
							MyAPIGateway.Multiplayer.SendMessageToOthers(5855, message, true);
						}
					}
				}
			}

			EnergyShieldsCore.effectSyncMessages.Clear();

		}

		public override MyObjectBuilder_SessionComponent GetObjectBuilder ()
		{
			return m_objectBuilder;
		}

		protected override void UnloadData ()
		{
			if(m_init) 
			{
				Log.writeLine("<EnergyShieldsCore> Logging stopped. (Exit)");
				Log.close();

				MyAPIGateway.Utilities.MessageEntered -= handleChatMessage;
				MyAPIGateway.Multiplayer.UnregisterMessageHandler(5853, configMessageHandler);
				MyAPIGateway.Multiplayer.UnregisterMessageHandler(5854, shieldMessageHandler);
				MyAPIGateway.Multiplayer.UnregisterMessageHandler(5855, effectMessageHandler);
			}

			EnergyShieldsCore.shieldGenerators.Clear();
		}

		public override void SaveData ()
		{
			saveShieldSaveFile ();
		}

		void init() 
		{
			Log.init("debug.log");
			Log.writeLine("<EnergyShieldsCore> Logging started.");

			MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler (0, shieldDamageHandler);

			MyAPIGateway.Multiplayer.RegisterMessageHandler(5853, configMessageHandler);
			MyAPIGateway.Multiplayer.RegisterMessageHandler(5854, shieldMessageHandler);
			MyAPIGateway.Multiplayer.RegisterMessageHandler(5855, effectMessageHandler);

			loadConfigFile();
			loadShieldSaveFile();

			MyAPIGateway.Utilities.MessageEntered += handleChatMessage;

			m_init = true;
		}

		void loadShieldSaveFile() 
		{
			if (MyAPIGateway.Utilities.FileExistsInLocalStorage (m_shieldSaveFileName, typeof(EnergyShieldsCore))) 
			{
				if (MyAPIGateway.Multiplayer.IsServer || !MyAPIGateway.Multiplayer.MultiplayerActive) 
				{
					Log.writeLine("<EnergyShieldsCore> Shield save file found.");
					try 
					{
						string buffer = "";

						using (TextReader file = MyAPIGateway.Utilities.ReadFileInLocalStorage (m_shieldSaveFileName, typeof(EnergyShieldsCore))) 
						{
							buffer = file.ReadToEnd ();
						}

						shieldSaveFile = MyAPIGateway.Utilities.SerializeFromXML<ShieldSaveFile> (buffer);

						if(shieldSaveFile != null) 
						{
							EnergyShieldsCore.shieldSaveFile = shieldSaveFile;
							Log.writeLine("<EnergyShieldsCore> Shield save file read in.");
						}

					} 
					catch (InvalidOperationException e) 
					{
						
					}
				}
			}

			foreach(var shieldMessage in EnergyShieldsCore.shieldSaveFile.ShieldGenerators)
			{
				if(MyAPIGateway.Entities.EntityExists(shieldMessage.entityID))
				{
					IMyEntity shield = MyAPIGateway.Entities.GetEntityById(shieldMessage.entityID);

					Log.writeLine("<EnergyShieldsCore> Shield " + shieldMessage.entityID + " found, assigned loaded value " + shieldMessage.value);

					ShieldGeneratorGameLogic shieldLogic = shield.GameLogic.GetAs<ShieldGeneratorGameLogic>();
					shieldLogic.m_currentShieldPoints = shieldMessage.value;
				}
			}
		}

		void saveShieldSaveFile() 
		{
			if(MyAPIGateway.Multiplayer != null)
			{
				if (MyAPIGateway.Multiplayer.IsServer || !MyAPIGateway.Multiplayer.MultiplayerActive) 
				{
					try 
					{
						using (TextWriter saveFile = MyAPIGateway.Utilities.WriteFileInLocalStorage (m_shieldSaveFileName, typeof(EnergyShieldsCore))) 
						{
							saveFile.Write(MyAPIGateway.Utilities.SerializeToXML<ShieldSaveFile> (EnergyShieldsCore.shieldSaveFile));
						}

						Log.writeLine("<EnergyShieldsCore> Shield save file saved.");
					} 
					catch (InvalidOperationException e) 
					{

					}
				}
			}
		}

		void loadConfigFile() 
		{
			if (MyAPIGateway.Utilities.FileExistsInLocalStorage (m_configName, typeof(EnergyShieldsCore))) 
			{
				if (MyAPIGateway.Multiplayer.IsServer || !MyAPIGateway.Multiplayer.MultiplayerActive) 
				{
					Configuration config;
					string buffer;

					using (TextReader file = MyAPIGateway.Utilities.ReadFileInLocalStorage (m_configName, typeof(EnergyShieldsCore))) 
					{
						buffer = file.ReadToEnd ();
					}

					try 
					{
						config = MyAPIGateway.Utilities.SerializeFromXML<Configuration> (buffer);

						if(config != null) 
						{
							EnergyShieldsCore.Config = config;
						}

					} 
					catch (InvalidOperationException e) 
					{
						using (TextWriter outputFile = MyAPIGateway.Utilities.WriteFileInLocalStorage (m_configName, typeof(EnergyShieldsCore))) 
						{
							generateNewConfigFile();
						}
					}
				}
				else
				{
					ConfigurationMessage configMessage = new ConfigurationMessage ();
					configMessage.sender = MyAPIGateway.Multiplayer.MyId;

					string message = MyAPIGateway.Utilities.SerializeToXML<ConfigurationMessage> (configMessage);

					MyAPIGateway.Multiplayer.SendMessageTo (5853, System.Text.Encoding.Unicode.GetBytes(message), MyAPIGateway.Multiplayer.ServerId, true);
					Log.writeLine("<EnergyShieldsCore> Config request sent to server.");
				}
			}
			else 
			{
				generateNewConfigFile();
			}
		}

		void generateNewConfigFile()
		{
			using (TextWriter outputFile = MyAPIGateway.Utilities.WriteFileInLocalStorage (m_configName, typeof(EnergyShieldsCore))) 
			{
				outputFile.Write(MyAPIGateway.Utilities.SerializeToXML<Configuration> (new Configuration()));
			}
		}

		public void handleChatMessage(string messageText, ref bool sendToOthers) 
		{
			var commands = messageText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

			if (commands.Length == 0)
				return;
			
			if (commands[0] == "/shield")
			{
				sendToOthers = false;

				string option = "";

				if((commands.Length > 1)) 
				{
					option = commands [1];

					if ((option == "ShowOnHud") && (!MyAPIGateway.Utilities.IsDedicated)) 
					{
						string option2 = "";

						if ((commands.Length > 2)) 
						{
							option2 = commands [2];

							if (option2 == "add") 
							{
								try 
								{
									IMyCubeBlock block = MyAPIGateway.Session.Player.Controller.ControlledEntity as IMyCubeBlock;

									if(block != null) {
										EnergyShieldsCore.trackedSeats.Add (block.EntityId);
									} else {
										MyAPIGateway.Utilities.ShowNotification ("No cockpit!", 1000, MyFontEnum.Red);
									}

								} 
								catch (Exception e) 
								{
								}
							} 
							else if (option2 == "remove") 
							{
								try 
								{

									IMyCubeBlock block = MyAPIGateway.Session.Player.Controller.ControlledEntity as IMyCubeBlock;

									if(block != null) 
									{
										EnergyShieldsCore.trackedSeats.Remove(block.EntityId);
									} else 
									{
										MyAPIGateway.Utilities.ShowNotification ("No cockpit!", 1000, MyFontEnum.Red);
									}

								} 
								catch (Exception e) 
								{
								}
							}
						}
					}
				}
			}
		}

		private void shieldMessageHandler(byte[] message)
		{
			long ID = BitConverter.ToInt64(message, 0);
			float value = BitConverter.ToSingle(message, 8);

			if(!MyAPIGateway.Multiplayer.IsServer)
			{
				//Log.writeLine("<ShieldGeneratorGameLogic> Sync received.");
				foreach(var shieldLogicKV in EnergyShieldsCore.shieldGenerators) 
				{
					if(ID == shieldLogicKV.Key) 
					{
						shieldLogicKV.Value.m_currentShieldPoints = value;
					}
				}
			}


		}

		private void effectMessageHandler(byte[] message)
		{
			if(!MyAPIGateway.Multiplayer.IsServer) 
			{
				int numberOfMessages = BitConverter.ToInt32(message, 0);

				//Log.writeLine("<EnergyShieldsCore> Effect sync with " + numberOfMessages +  " messages received. (" + message.Length + " bytes)");
				long cubeGridID = 0;
				try 
				{
					for(int i = 0; i < numberOfMessages; i++)
					{
						
						cubeGridID = BitConverter.ToInt64(message, 0 + (i*25) + 4);
						Vector3I slimBlockCoords = new Vector3I( BitConverter.ToInt32(message, 8 + (i*25) + 4), BitConverter.ToInt32(message, 12 + (i*25) + 4), BitConverter.ToInt32(message, 16 + (i*25) + 4));
						float status = BitConverter.ToSingle(message, 20 + (i*25) + 4);
						bool deformation = BitConverter.ToBoolean(message, 24 + (i*25) + 4);

						IMyCubeGrid cubeGrid = MyAPIGateway.Entities.GetEntityById(cubeGridID) as IMyCubeGrid;
						if(cubeGrid != null)
						{
							IMySlimBlock slimBlock = cubeGrid.GetCubeBlock(slimBlockCoords);
							if(slimBlock != null)
							{
								EnergyShieldsCore.shieldRenderers.Add(new ShieldRenderer(slimBlock, ShieldRenderMode.Normal, ShieldColor.BlueToRed, status, deformation));
							}
						}
					}
				}
				catch(KeyNotFoundException e) {
					//Log.writeLine("<EnergyShieldsCore> CubeGrid not found :" + cubeGridID);
				}
			}
		}

		void showShieldOnHud() 
		{
			if(MyAPIGateway.Utilities != null)
			{
				if (!MyAPIGateway.Utilities.IsDedicated) 
				{
					if((MyAPIGateway.Session != null) && (MyAPIGateway.Session.Player != null) && (MyAPIGateway.Session.Player.Controller != null) && (MyAPIGateway.Session.Player.Controller != null))
					{
						IMyCubeBlock block = MyAPIGateway.Session.Player.Controller.ControlledEntity as IMyCubeBlock;

						if(block != null) 
						{
							if(EnergyShieldsCore.trackedSeats.Contains(block.EntityId)) 
							{
								IMyGridTerminalSystem tsystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid (block.CubeGrid as IMyCubeGrid);
								List<IMyTerminalBlock> shieldsConnected = new List<IMyTerminalBlock>();

								if (tsystem != null) 
								{
									tsystem.GetBlocksOfType<IMyRefinery> (shieldsConnected, EnergyShieldsCore.shieldFilter);

									float shipCurrentShieldPoints = 0f;
									float shipMaximumShieldPoints = 0f;

									foreach (var shield in shieldsConnected) 
									{
										ShieldGeneratorGameLogic generatorLogic = ((IMyTerminalBlock)shield).GameLogic.GetAs<ShieldGeneratorGameLogic> ();

										shipCurrentShieldPoints += generatorLogic.m_currentShieldPoints;
										shipMaximumShieldPoints += generatorLogic.m_maximumShieldPoints;
									}

									if (EnergyShieldsCore.shieldNotification != null) 
									{
										EnergyShieldsCore.shieldNotification.Hide ();
									}

									EnergyShieldsCore.shieldNotification = MyAPIGateway.Utilities.CreateNotification ("Shield: " + ((int)Math.Ceiling(shipCurrentShieldPoints)) + "/" + ((int) shipMaximumShieldPoints), (int) (Math.Ceiling(EnergyShieldsCore.Config.GeneralSettings.StatusUpdateInterval * 1f / 60f + 1f / 60f) * 1000), MyFontEnum.Blue);
									EnergyShieldsCore.shieldNotification.Show ();
								} 
							}
						}
					}
				}
			}
		}

		void updateShieldRenderers()
		{
			if(!MyAPIGateway.Utilities.IsDedicated)
			{
				List<ShieldRenderer> toRemove = new List<ShieldRenderer>();

				foreach(var shieldRenderer in EnergyShieldsCore.shieldRenderers)
				{
					if(shieldRenderer.m_timeToLive > 0)
					{
						shieldRenderer.update();
					}
					else
					{
						toRemove.Add(shieldRenderer);
					}
				}

				foreach(var deadShieldRenderer in toRemove)
				{
					deadShieldRenderer.close();
					EnergyShieldsCore.shieldRenderers.Remove(deadShieldRenderer);
				}
			}

		}

		void shieldDamageHandler(object target, ref MyDamageInformation info) 
		{
			
			
			IMySlimBlock slimBlock = target as IMySlimBlock;

			if (slimBlock != null) 
			{
				handleDamageOnBlock (slimBlock, ref info);
			}

		}

		private void configMessageHandler(byte[] message) 
		{
			if (MyAPIGateway.Multiplayer.IsServer)
			{
				ConfigurationMessage configMessage = MyAPIGateway.Utilities.SerializeFromXML<ConfigurationMessage>(Encoding.Unicode.GetString (message));
				ulong sender = configMessage.sender;

				// send in parts (fucking size limit GOD!!!)
				ConfigurationMessage answer;

				answer = new ConfigurationMessage();
				answer.type = ConfigType.LargeBlockSmallShield;
				answer.config = System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<ShieldDefinition> (EnergyShieldsCore.Config.LargeBlockSmallGenerator));
				if(MyAPIGateway.Multiplayer.SendMessageTo (5853, System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<ConfigurationMessage> (answer)), configMessage.sender, true))
				{
					Log.writeLine("<EnergyShieldsCore> Config LargeBlockSmallShield sent to client. (ID:" + configMessage.sender + ")");
				}
				else
				{
					Log.writeLine("<EnergyShieldsCore> ERROR: Config LargeBlockSmallShield NOT sent to client. (ID:" + configMessage.sender + ")");
				}

				answer = new ConfigurationMessage();
				answer.type = ConfigType.LargeBlockLargeShield;
				answer.config = System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<ShieldDefinition> (EnergyShieldsCore.Config.LargeBlockLargeGenerator));
				if(MyAPIGateway.Multiplayer.SendMessageTo (5853, System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<ConfigurationMessage> (answer)), configMessage.sender, true))
				{
					Log.writeLine("<EnergyShieldsCore> Config LargeBlockLargeShield sent to client. (ID:" + configMessage.sender + ")");
				}
				else
				{
					Log.writeLine("<EnergyShieldsCore> ERROR: Config LargeBlockLargeShield NOT sent to client. (ID:" + configMessage.sender + ")");
				}

				answer = new ConfigurationMessage();
				answer.type = ConfigType.SmallBlockSmallShield;
				answer.config = System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<ShieldDefinition> (EnergyShieldsCore.Config.SmallBlockSmallGenerator));
				if(MyAPIGateway.Multiplayer.SendMessageTo (5853, System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<ConfigurationMessage> (answer)), configMessage.sender, true))
				{
					Log.writeLine("<ShieldGeneratorGameLogic> Config SmallBlockSmallShield sent to client. (ID:" + configMessage.sender + ")");
				}
				else
				{
					Log.writeLine("<ShieldGeneratorGameLogic> ERROR: Config SmallBlockSmallShield NOT sent to client. (ID:" + configMessage.sender + ")");
				}

				answer = new ConfigurationMessage();
				answer.type = ConfigType.SmallBlockLargeShield;
				answer.config = System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<ShieldDefinition> (EnergyShieldsCore.Config.SmallBlockLargeGenerator));
				if(MyAPIGateway.Multiplayer.SendMessageTo (5853, System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<ConfigurationMessage> (answer)), configMessage.sender, true))
				{
					Log.writeLine("<ShieldGeneratorGameLogic> Config SmallBlockLargeShield sent to client. (ID:" + configMessage.sender + ")");
				}
				else
				{
					Log.writeLine("<ShieldGeneratorGameLogic> ERROR: Config SmallBlockLargeShield NOT sent to client. (ID:" + configMessage.sender + ")");
				}

				answer = new ConfigurationMessage();
				answer.type = ConfigType.Module;
				answer.config = System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<ModuleDefinition> (EnergyShieldsCore.Config.UpgradeModuleMultiplier));
				if(MyAPIGateway.Multiplayer.SendMessageTo (5853, System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<ConfigurationMessage> (answer)), configMessage.sender, true))
				{
					Log.writeLine("<ShieldGeneratorGameLogic> Config ModuleDefinition sent to client. (ID:" + configMessage.sender + ")");
				}
				else
				{
					Log.writeLine("<ShieldGeneratorGameLogic> ERROR: Config ModuleDefition NOT sent to client. (ID:" + configMessage.sender + ")");
				}

				answer = new ConfigurationMessage();
				answer.type = ConfigType.Damage;
				answer.config = System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<DamageMultiplierDefinition> (EnergyShieldsCore.Config.DamageMultipliers));
				if(MyAPIGateway.Multiplayer.SendMessageTo (5853, System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<ConfigurationMessage> (answer)), configMessage.sender, true))
				{
					Log.writeLine("<ShieldGeneratorGameLogic> Config DamageDefinition sent to client. (ID:" + configMessage.sender + ")");
				}
				else
				{
					Log.writeLine("<ShieldGeneratorGameLogic> ERROR: Config DamageDefition NOT sent to client. (ID:" + configMessage.sender + ")");
				}

				answer = new ConfigurationMessage();
				answer.type = ConfigType.General;
				answer.config = System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<GeneralSettingsDefinition> (EnergyShieldsCore.Config.GeneralSettings));
				if(MyAPIGateway.Multiplayer.SendMessageTo (5853, System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<ConfigurationMessage> (answer)), configMessage.sender, true))
				{
					Log.writeLine("<ShieldGeneratorGameLogic> Config GeneralDefinition sent to client. (ID:" + configMessage.sender + ")");
				}
				else
				{
					Log.writeLine("<ShieldGeneratorGameLogic> ERROR: Config GeneralDefition NOT sent to client. (ID:" + configMessage.sender + ")");
				}

				answer = new ConfigurationMessage();
				answer.type = ConfigType.Effects;
				answer.config = System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<EffectDefinition> (EnergyShieldsCore.Config.Effects));
				if(MyAPIGateway.Multiplayer.SendMessageTo (5853, System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<ConfigurationMessage> (answer)), configMessage.sender, true))
				{
					Log.writeLine("<ShieldGeneratorGameLogic> Config EffectDefinition sent to client. (ID:" + configMessage.sender + ")");
				}
				else
				{
					Log.writeLine("<ShieldGeneratorGameLogic> ERROR: Config EffectDefition NOT sent to client. (ID:" + configMessage.sender + ")");
				}

				answer = new ConfigurationMessage();
				answer.type = ConfigType.Alternative;
				answer.config = System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<AlternativeRechargeModeDefinition> (EnergyShieldsCore.Config.AlternativeRechargeMode));
				if(MyAPIGateway.Multiplayer.SendMessageTo (5853, System.Text.Encoding.Unicode.GetBytes (MyAPIGateway.Utilities.SerializeToXML<ConfigurationMessage> (answer)), configMessage.sender, true))
				{
					Log.writeLine("<ShieldGeneratorGameLogic> Config AlternativeDefinition sent to client. (ID:" + configMessage.sender + ")");
				}
				else
				{
					Log.writeLine("<ShieldGeneratorGameLogic> ERROR: Config AlternativeDefition NOT sent to client. (ID:" + configMessage.sender + ")");
				}
				
					
			}
			else
			{
				
				ConfigurationMessage configMessage = MyAPIGateway.Utilities.SerializeFromXML<ConfigurationMessage>(Encoding.Unicode.GetString (message));

				if(configMessage.type == ConfigType.LargeBlockSmallShield)
				{
					EnergyShieldsCore.Config.LargeBlockSmallGenerator  = MyAPIGateway.Utilities.SerializeFromXML<ShieldDefinition>(Encoding.Unicode.GetString (configMessage.config));
					Log.writeLine("<ShieldGeneratorGameLogic> Config LargeBlockSmallShield received.");
				}
				else if(configMessage.type == ConfigType.LargeBlockLargeShield)
				{
					EnergyShieldsCore.Config.LargeBlockLargeGenerator  = MyAPIGateway.Utilities.SerializeFromXML<ShieldDefinition>(Encoding.Unicode.GetString (configMessage.config));
					Log.writeLine("<ShieldGeneratorGameLogic> Config LargeBlockLargeShield received.");
				}
				else if(configMessage.type == ConfigType.SmallBlockSmallShield)
				{
					EnergyShieldsCore.Config.SmallBlockSmallGenerator  = MyAPIGateway.Utilities.SerializeFromXML<ShieldDefinition>(Encoding.Unicode.GetString (configMessage.config));
					Log.writeLine("<ShieldGeneratorGameLogic> Config SmallBlockSmallShield received.");
				}
				else if(configMessage.type == ConfigType.LargeBlockLargeShield)
				{
					EnergyShieldsCore.Config.SmallBlockLargeGenerator  = MyAPIGateway.Utilities.SerializeFromXML<ShieldDefinition>(Encoding.Unicode.GetString (configMessage.config));
					Log.writeLine("<ShieldGeneratorGameLogic> Config SmallBlockLargeShield received.");
				}
				else if(configMessage.type == ConfigType.Module)
				{
					EnergyShieldsCore.Config.UpgradeModuleMultiplier  = MyAPIGateway.Utilities.SerializeFromXML<ModuleDefinition>(Encoding.Unicode.GetString (configMessage.config));
					Log.writeLine("<ShieldGeneratorGameLogic> Config Module received.");
				}
				else if(configMessage.type == ConfigType.Damage)
				{
					EnergyShieldsCore.Config.DamageMultipliers  = MyAPIGateway.Utilities.SerializeFromXML<DamageMultiplierDefinition>(Encoding.Unicode.GetString (configMessage.config));
					Log.writeLine("<ShieldGeneratorGameLogic> Config Damage received.");
				}
				else if(configMessage.type == ConfigType.General)
				{
					EnergyShieldsCore.Config.GeneralSettings  = MyAPIGateway.Utilities.SerializeFromXML<GeneralSettingsDefinition>(Encoding.Unicode.GetString (configMessage.config));
					Log.writeLine("<ShieldGeneratorGameLogic> Config General received.");
				}
				else if(configMessage.type == ConfigType.Effects)
				{
					EnergyShieldsCore.Config.Effects  = MyAPIGateway.Utilities.SerializeFromXML<EffectDefinition>(Encoding.Unicode.GetString (configMessage.config));
					Log.writeLine("<ShieldGeneratorGameLogic> Config Effects received.");
				}
				else if(configMessage.type == ConfigType.Alternative)
				{
					EnergyShieldsCore.Config.AlternativeRechargeMode  = MyAPIGateway.Utilities.SerializeFromXML<AlternativeRechargeModeDefinition>(Encoding.Unicode.GetString (configMessage.config));
					Log.writeLine("<ShieldGeneratorGameLogic> Config Alternative received.");
				}
			}
		}

		void CutOutVoxels(IMySlimBlock slimBlock, ref MyDamageInformation info, ref float multiplier) {
			
			if(info.AttackerId > 0) {

				IMyEntity collidedWith = MyAPIGateway.Entities.GetEntityById(info.AttackerId);

				if(MyAPIGateway.Entities.GetEntityById(info.AttackerId) is MyVoxelBase) {

					multiplier = multiplier * 15f;

					float speed = slimBlock.CubeGrid.Physics.LinearVelocity.Length();

					if(speed > 10f) {

						float radius = speed * 0.05f;


						var shape = MyAPIGateway.Session.VoxelMaps.GetSphereVoxelHand();

						MyCubeBlockDefinition blockDefinition = slimBlock.BlockDefinition as MyCubeBlockDefinition;

						int maxBlockSize = Math.Max(blockDefinition.Size.X, Math.Max(blockDefinition.Size.Y, blockDefinition.Size.Z));

						shape.Radius = radius + maxBlockSize * 2.5f + 0.5f;
						shape.Center = slimBlock.CubeGrid.GridIntegerToWorld(slimBlock.Position);

						if(!(collidedWith is MyVoxelMap)) {

							foreach (var pl in WorldPlanets.planets)
							{
								var planet = pl.Value;

								if (planet.Closed || planet.MarkedForClose)
								{
									WorldPlanets.removePlanets.Add(pl.Key);
									continue;
								}

								if (Vector3D.DistanceSquared(slimBlock.CubeGrid.GetPosition(), planet.WorldMatrix.Translation) < (planet.MaximumRadius * planet.MaximumRadius))
								{

									planet.RequestVoxelOperationSphere(shape.Center, shape.Radius, 0, VRage.Voxels.OperationType.Cut);

									break;
								}
							}
						}
						else {

							MyAPIGateway.Session.VoxelMaps.CutOutShape((MyVoxelBase)collidedWith, shape);
						}
					}
					
				}
			}
		}

		void handleDamageOnBlock(IMySlimBlock slimBlock, ref MyDamageInformation info)
		{
			//slimBlock.CubeGrid.GridIntegerToWorld(slimBlock.Position);
			try {
				
				float damageMultiplier = 1f;

				if (info.Type == damageTypeIgnoreShields) 
				{
					return;
				}
				else if (info.Type == damageTypeGrinder) 
				{
					if (EnergyShieldsCore.Config.GeneralSettings.GrindersIgnoreShields) 
					{
						return;
					} 
					else 
					{
						damageMultiplier = EnergyShieldsCore.Config.DamageMultipliers.GrindDamageMultiplier;
					}
				} 
				else if (info.Type == damageTypeDrill) 
				{
					damageMultiplier = EnergyShieldsCore.Config.DamageMultipliers.DrillDamageMultiplier;
				}
				else if (info.Type == damageTypeDeformation) 
				{
					if(EnergyShieldsCore.Config.GeneralSettings.IgnoreCollision)
					{
						return;
					}

					damageMultiplier = EnergyShieldsCore.Config.DamageMultipliers.DeformationDamageMultiplier;
				}
				else if (info.Type == damageTypeExplosion) 
				{
					damageMultiplier = EnergyShieldsCore.Config.DamageMultipliers.ExplosionDamageMultiplier;
				}
				else if (info.Type == damageTypeBullet) 
				{
					damageMultiplier = EnergyShieldsCore.Config.DamageMultipliers.BulletDamageMultiplier;

                    IMyCubeBlock c;
				}

				if(!m_cachedGrids.ContainsKey(slimBlock.CubeGrid.EntityId))
				{
					IMyGridTerminalSystem tsystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid (slimBlock.CubeGrid as IMyCubeGrid);
					List<IMyTerminalBlock> shields = new List<IMyTerminalBlock>();
					List<ShieldGeneratorGameLogic> shieldLogics = new List<ShieldGeneratorGameLogic>();

					if (tsystem != null) {

						tsystem.GetBlocksOfType<IMyRefinery> (shields, shieldFilter);

						foreach(var shield in shields)
						{
							shieldLogics.Add(((IMyTerminalBlock)shield).GameLogic.GetAs<ShieldGeneratorGameLogic> ());
						}
					}

					m_cachedGrids.Add(slimBlock.CubeGrid.EntityId, shieldLogics);
				}

				List<ShieldGeneratorGameLogic> shieldsConnected = m_cachedGrids[slimBlock.CubeGrid.EntityId];

				if(shieldsConnected.Count != 0) {
					
					float shipCurrentShieldPoints = 0f;
					float shipMaximumShieldPoints = 0f;

					foreach (var shield in shieldsConnected) 
					{
						ShieldGeneratorGameLogic generatorLogic = shield;

						if((generatorLogic.Entity as Sandbox.ModAPI.IMyFunctionalBlock).Enabled) {
							shipMaximumShieldPoints += generatorLogic.m_maximumShieldPoints;
							shipCurrentShieldPoints += generatorLogic.m_currentShieldPoints;
						}
					}

					if(EnergyShieldsCore.Config.GeneralSettings.PercentageBasedDamageLeaking) 
					{
						
						
						float shieldDamage = info.Amount * damageMultiplier * shipCurrentShieldPoints/shipMaximumShieldPoints;

						float totalShieldPoints = shipCurrentShieldPoints - shieldDamage;

						float leakedDamage = 0;

						if(totalShieldPoints > 0) 
						{
                            if (!(slimBlock.FatBlock != null && (slimBlock.FatBlock is IMyWheel)))
                            {
                                CutOutVoxels(slimBlock, ref info, ref damageMultiplier);
                            }
							
							if(slimBlock.FatBlock != null && slimBlock.FatBlock.IsFunctional) 
							{
								slimBlock.FatBlock.SetDamageEffect(false);
							}

							float shieldStatus = shipCurrentShieldPoints/shipMaximumShieldPoints;

							leakedDamage = info.Amount * (1-shipCurrentShieldPoints/shipMaximumShieldPoints);

							foreach(var shield in shieldsConnected) 
							{
								ShieldGeneratorGameLogic generatorLogic = shield;

								if((generatorLogic.Entity as Sandbox.ModAPI.IMyFunctionalBlock).Enabled && (generatorLogic.Entity as Sandbox.ModAPI.IMyFunctionalBlock).IsFunctional)
								{

									generatorLogic.m_currentShieldPoints -= shieldDamage * generatorLogic.m_maximumShieldPoints/shipMaximumShieldPoints;

									generatorLogic.m_ticksUntilRecharge = EnergyShieldsCore.Config.AlternativeRechargeMode.RechargeDelay;
								}
							}


							bool deformation = false;

							if(info.Type == damageTypeDeformation)
							{
								deformation = true;
							}

							if(!MyAPIGateway.Utilities.IsDedicated) 
							{
								EnergyShieldsCore.shieldRenderers.Add(new ShieldRenderer(slimBlock, ShieldRenderMode.Normal, ShieldColor.BlueToRed, shieldStatus, deformation));
							}

							if(MyAPIGateway.Multiplayer.IsServer)
							{
								addEffectSyncMessage(slimBlock, shieldStatus, deformation);
							}
						} 
						else 
						{
							leakedDamage = info.Amount * (1-shipCurrentShieldPoints/shipMaximumShieldPoints) 
								+ Math.Abs(totalShieldPoints)/damageMultiplier;

							foreach(var shield in shieldsConnected) 
							{
								ShieldGeneratorGameLogic generatorLogic = shield;

								if((generatorLogic.Entity as Sandbox.ModAPI.IMyFunctionalBlock).Enabled && (generatorLogic.Entity as Sandbox.ModAPI.IMyFunctionalBlock).IsFunctional)
								{

									generatorLogic.m_currentShieldPoints = 0;

									generatorLogic.m_ticksUntilRecharge = EnergyShieldsCore.Config.AlternativeRechargeMode.RechargeDelay;
								}
							}
						}

						info.Amount = leakedDamage;

						//MyAPIGateway.Utilities.ShowNotification("Leaked: " + leakedDamage, 1000);

					} 
					else 
					{
						float shieldStatus = shipCurrentShieldPoints/shipMaximumShieldPoints;
						
						float shieldDamage = info.Amount * damageMultiplier;

						float totalShieldPoints = shipCurrentShieldPoints - shieldDamage;

						if(totalShieldPoints > 0) 
						{
							info.Amount = 0;
							info.IsDeformation = false;

                            if (!(slimBlock.FatBlock != null && (slimBlock.FatBlock is IMyWheel)))
                            {
                                CutOutVoxels(slimBlock, ref info, ref damageMultiplier);
                            }

                            if (slimBlock.FatBlock != null && slimBlock.FatBlock.IsFunctional) 
							{
								slimBlock.FatBlock.SetDamageEffect(false);
							}

							bool deformation = false;

							if(info.Type == damageTypeDeformation)
							{
								deformation = true;
							}

							if(!MyAPIGateway.Utilities.IsDedicated) 
							{
								EnergyShieldsCore.shieldRenderers.Add(new ShieldRenderer(slimBlock, ShieldRenderMode.Normal, ShieldColor.BlueToRed, shieldStatus, deformation));
							}

							if(MyAPIGateway.Multiplayer.IsServer)
							{
								addEffectSyncMessage(slimBlock, shieldStatus, deformation);
							}

							foreach(var shield in shieldsConnected) 
							{
								
								ShieldGeneratorGameLogic generatorLogic = shield;

								if((generatorLogic.Entity as Sandbox.ModAPI.IMyFunctionalBlock).Enabled && (generatorLogic.Entity as Sandbox.ModAPI.IMyFunctionalBlock).IsFunctional)
								{

									generatorLogic.m_currentShieldPoints -= shieldDamage * generatorLogic.m_maximumShieldPoints/shipMaximumShieldPoints;

									generatorLogic.m_ticksUntilRecharge = EnergyShieldsCore.Config.AlternativeRechargeMode.RechargeDelay;
								}
							}
						} 
						else 
						{
							info.Amount = Math.Abs(totalShieldPoints)/(damageMultiplier);

							//MyAPIGateway.Utilities.ShowNotification("Damage leaked: " +info.Amount + " " + info.Type);

							//MyAPIGateway.Utilities.ShowNotification("Damage: " + info.Type);

							foreach(var shield in shieldsConnected) 
							{
								ShieldGeneratorGameLogic generatorLogic = shield;

								if((generatorLogic.Entity as Sandbox.ModAPI.IMyFunctionalBlock).Enabled && (generatorLogic.Entity as Sandbox.ModAPI.IMyFunctionalBlock).IsFunctional)
								{

									generatorLogic.m_currentShieldPoints = 0;

									generatorLogic.m_ticksUntilRecharge = EnergyShieldsCore.Config.AlternativeRechargeMode.RechargeDelay;


								}
							}
						}
					}
				}
		
			} catch(Exception e) {
			}
		}

		public void addEffectSyncMessage(IMySlimBlock slimBlock, float shieldStatus, bool deformation)
		{
			byte[] message = new byte[25];
			byte[] messageCubeGridID = BitConverter.GetBytes(slimBlock.CubeGrid.EntityId);
			byte[] messageSlimX = BitConverter.GetBytes(slimBlock.Position.X);
			byte[] messageSlimY = BitConverter.GetBytes(slimBlock.Position.Y);
			byte[] messageSlimZ = BitConverter.GetBytes(slimBlock.Position.Z);
			byte[] messageStatus = BitConverter.GetBytes(shieldStatus);
			byte[] messageDeformation = BitConverter.GetBytes(deformation);

			for(int i = 0; i < 8; i++) {
				message[i] = messageCubeGridID[i];
			}

			for(int i = 0; i < 4; i++) {
				message[i+8] = messageSlimX[i];
			}

			for(int i = 0; i < 4; i++) {
				message[i+12] = messageSlimY[i];
			}

			for(int i = 0; i < 4; i++) {
				message[i+16] = messageSlimZ[i];
			}

			for(int i = 0; i < 4; i++) {
				message[i+20] = messageStatus[i];
			}

			message[24] = messageDeformation[0];

			EnergyShieldsCore.effectSyncMessages.Add(message);

			//MyAPIGateway.Multiplayer.SendMessageToOthers(5855, message, true);
		}

		public static bool shieldFilter(IMyTerminalBlock block) 
		{

			return ((block.BlockDefinition.SubtypeName == "LargeShipSmallShieldGeneratorBase")
				|| (block.BlockDefinition.SubtypeName == "SmallShipSmallShieldGeneratorBase")
				|| (block.BlockDefinition.SubtypeName == "SmallShipMicroShieldGeneratorBase")
				|| (block.BlockDefinition.SubtypeName == "LargeShipLargeShieldGeneratorBase"));
		}
	}
}

