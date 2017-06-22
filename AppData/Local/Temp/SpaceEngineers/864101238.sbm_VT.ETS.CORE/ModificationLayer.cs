/*
 * VerdanTech Extensible Terminal System Core
 * Copyright � 2017, VerdanTech
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Game;
using SM = Sandbox.ModAPI;
using SMI = Sandbox.ModAPI.Ingame;
using SEGMI = SpaceEngineers.Game.ModAPI.Ingame;
using VRGMI = VRage.Game.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using Sandbox.Game.GameSystems.Electricity;
using VRage;
using VRage.ObjectBuilders;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRageMath;

namespace VT.ETS.CORE
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation, Priority = 0)]
    class ModificationLayer : MySessionComponentBase
    {
        private bool m_init = false;
        private MyObjectBuilder_SessionComponent m_sessionComponent = null;
        /// <summary>
        /// A toggle-switch for class-by-class debug-logger output management...
        /// </summary>
        private static bool debugging = false;
        /// <summary>
        /// We set up debugging so it can't be enabled without a Logger available.
        /// </summary>
        internal static bool Debugging
        {
            get { return debugging; }
            set { if (debugLogger != null) debugging = value; }
        }
        /// <summary>
        /// File-creator and content-printer used to aggregate action-logs for use in post-session debugging.
        /// </summary>
        private static Logger debugLogger = null;
        /// <summary>
        /// We set up the logger to prevent null-assignment once its been initialized. 
        /// </summary>
        internal static Logger DebugLogger
        {
            get { return debugLogger; }
            set { if (value != null) debugLogger = value; }
        }
        /// <summary>
        /// The first thing called, it grabs our component, fires the base version of Init, and generates initial extensions.
        /// </summary>
        /// <param name="sessionComponent">The object-builder for the session-component.</param>
        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            m_sessionComponent = sessionComponent;
            base.Init(sessionComponent);
            GenerateInitialExtensions();
        }
        /// <summary>
        /// Called once per frame before the physics get run, it calls the base version and attempts to initialize if the first run failed.
        /// </summary>
        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();
            if (!m_init)
                GenerateInitialExtensions();
        }
        /// <summary>
        /// This is a diagnostic print-command with a "should-I-really-print-it" flag to make revising log entry batches quicker and easier.
        /// </summary>
        /// <param name="localWatch">This decides whether or not to actually print-to-file.</param>
        /// <param name="message">The text to be printed to the log-file.</param>
        private void Note(bool localWatch, string message)
        {
            if (debugging && localWatch)
                debugLogger.WriteLine(message);
        }
        /// <summary>
        /// Initializes our core-controls, processes existing blocks and grids, and registers entity-checks for grid processing.
        /// </summary>
        private void GenerateInitialExtensions()
        {
            // Set this to true/false to enabled/disable debug-logging in this method...
            bool watchMe = true;
            if (Sandbox.ModAPI.MyAPIGateway.Entities != null)
            {
                debugLogger = new Logger("ComponentLayer");
                debugging = true;
                Note(watchMe, "Generating initial extensions...");
                Note(watchMe, "Subscribing IncludeGrid to the entity-addition event...");
                Sandbox.ModAPI.MyAPIGateway.Entities.OnEntityAdd += AttemptGridInclusion;
                Note(watchMe, "Initializing base controls...");
                HashSet<VRage.ModAPI.IMyEntity> set = new HashSet<VRage.ModAPI.IMyEntity>();
                Note(watchMe, "Collecting known grids...");
                Sandbox.ModAPI.MyAPIGateway.Entities.GetEntities(set, entity => entity is VRage.Game.ModAPI.IMyCubeGrid);
                Note(watchMe, "Iterating over available grids...");
                foreach (VRage.ModAPI.IMyEntity e in set)
                    AttemptGridInclusion(e);
                Note(watchMe, "Marking initialization of extension complete.");
                m_init = true;
            }
        }
        /// <summary>
        /// Processes the provided entity, and if applicable processes the existing blocks and registers it for automatic processing of new blocks.
        /// </summary>
        /// <param name="obj">The entity being processed for grid-block processing and registration.</param>
        private void AttemptGridInclusion(VRage.ModAPI.IMyEntity obj)
        {
            // Set this to true/false to enabled/disable debug-logging in this method...
            bool watchMe = true;
            Note(watchMe, "Attempting inclusion of entity " + obj.EntityId.ToString() + " as grid...");
            VRage.Game.ModAPI.IMyCubeGrid grid = obj as VRage.Game.ModAPI.IMyCubeGrid;
            if (grid != null && grid.Physics != null && grid.Physics.Enabled)
            {
                List<VRage.Game.ModAPI.IMySlimBlock> blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
                Note(watchMe, "Collecting terminal block references...");
                grid.GetBlocks(blocks, block => block.FatBlock != null && block.FatBlock is Sandbox.ModAPI.IMyTerminalBlock);
                Note(watchMe, "Iterating over identified references");
                foreach (VRage.Game.ModAPI.IMySlimBlock block in blocks)
                    AttemptExtension(block);
                Note(watchMe, "Subscribing AttemptExtension to grid's block-added event...");
                grid.OnBlockAdded += AttemptExtension;
            }
            else
            {
                Note(watchMe, "Entity cast-to-grid operation failed...");
            }
        }
        /// <summary>
        /// Processes the slim-block, and provided its fat-block is a potential client-host, applies a client-base extension to it.
        /// </summary>
        /// <param name="slim">The slim block being processed for extension.</param>
        private void AttemptExtension(VRage.Game.ModAPI.IMySlimBlock slim)
        {
            // Set this to true/false to enabled/disable debug-logging in this method...
            bool watchMe = true;
            Note(watchMe, "Attempting extension...");
            Sandbox.ModAPI.IMyTerminalBlock block = slim.FatBlock as Sandbox.ModAPI.IMyTerminalBlock;
            if (block != null)
            {
                Note(watchMe, "Terminal block identified...");
                if (!CoreExtension.extensionSet.Any(kvp => kvp.Value.Host.EntityId == block.EntityId))
                {
                    DeployControlsForFatBlock(slim.FatBlock);
                    Note(watchMe, "Adding candidate extension to set...");
                    CoreExtension e = new CoreExtension(block);
                    CoreExtension.extensionSet.Add(block.EntityId, e);
                }
                else
                {
                    Note(watchMe, "Either incompatible or pre-extended block identified...");
                }
            }
            else
            {
                Note(watchMe, "Non-terminal block identified...");
            }
        }
        /// <summary>
        /// Compares the type to a list of types, invoking the generic control deployment for the type found.
        /// </summary>
        /// <param name="t">The type to be compared for control deployment.</param>
        private void DeployControlsForFatBlock<T>(T target)
        {
            // Set this to true/false to enabled/disable debug-logging in this method...
            bool watchMe = true;
            Note(watchMe, "Initializing controls on candidate of type " + target.GetType().ToString());
            Sandbox.ModAPI.IMyTerminalBlock block = target as Sandbox.ModAPI.IMyTerminalBlock;
            if (block == null)
            {
                Note(watchMe, "Invalid extension client...");
                return;
            }

            Note(watchMe, "Checking Tier 0 - Only the terminal-block itself.");
            if (block is SMI.IMyTerminalBlock) DeployClientControls<SMI.IMyTerminalBlock>();

            Note(watchMe, "Checking Tier 1 - Anything directly inheriting from the terminal block, tier 0.");
            if (block is SMI.IMyFunctionalBlock) DeployClientControls<SMI.IMyFunctionalBlock>();
            if (block is SMI.IMyCargoContainer) DeployClientControls<SMI.IMyCargoContainer>();
            if (block is SMI.IMyShipController) DeployClientControls<SMI.IMyShipController>();
            if (block is SMI.IMyWarhead) DeployClientControls<SMI.IMyWarhead>();
            if (block is SEGMI.IMyButtonPanel) DeployClientControls<SEGMI.IMyButtonPanel>();
            if (block is SEGMI.IMyControlPanel) DeployClientControls<SEGMI.IMyControlPanel>();
            if (block is SEGMI.IMyOxygenFarm) DeployClientControls<SEGMI.IMyOxygenFarm>();
            if (block is SEGMI.IMySolarPanel) DeployClientControls<SEGMI.IMySolarPanel>();

            Note(watchMe, "Checking Tier 2 - Anything directly inheriting from a tier 1 block.");
            if (block is SMI.IMyBatteryBlock) DeployClientControls<SMI.IMyBatteryBlock>();
            if (block is SMI.IMyBeacon) DeployClientControls<SMI.IMyBeacon>();
            if (block is SMI.IMyCameraBlock) DeployClientControls<SMI.IMyCameraBlock>();
            if (block is SMI.IMyCockpit) DeployClientControls<SMI.IMyCockpit>();
            if (block is SMI.IMyCollector) DeployClientControls<SMI.IMyCollector>();
            if (block is SMI.IMyConveyorSorter) DeployClientControls<SMI.IMyConveyorSorter>();
            if (block is SMI.IMyDecoy) DeployClientControls<SMI.IMyDecoy>();
            if (block is SMI.IMyDoor) DeployClientControls<SMI.IMyDoor>();
            if (block is SMI.IMyGyro) DeployClientControls<SMI.IMyGyro>();
            if (block is SMI.IMyJumpDrive) DeployClientControls<SMI.IMyJumpDrive>();
            if (block is SMI.IMyLaserAntenna) DeployClientControls<SMI.IMyLaserAntenna>();
            if (block is SMI.IMyLightingBlock) DeployClientControls<SMI.IMyLightingBlock>();
            if (block is SMI.IMyMotorBase) DeployClientControls<SMI.IMyMotorBase>();
            if (block is SMI.IMyOreDetector) DeployClientControls<SMI.IMyOreDetector>();
            if (block is SMI.IMyOxygenGenerator) DeployClientControls<SMI.IMyOxygenGenerator>();
            if (block is SMI.IMyOxygenTank) DeployClientControls<SMI.IMyOxygenTank>();
            if (block is SMI.IMyPistonBase) DeployClientControls<SMI.IMyPistonBase>();
            if (block is SMI.IMyProductionBlock) DeployClientControls<SMI.IMyProductionBlock>();
            if (block is SMI.IMyProgrammableBlock)
            {
                ITerminalProperty p = block.GetProperty(CoreExtension.Documentation_PropertyId);
                if (p == null)
                {
                    DefaultCreateAndAddProperty<SM.IMyProgrammableBlock, string>(CoreExtension.Documentation_PropertyId, CoreExtension.VT_ETS_CORE_API, (b, s) => { });
                }
                DeployClientControls<SMI.IMyProgrammableBlock>();
            }
            if (block is SMI.IMyProjector) DeployClientControls<SMI.IMyProjector>();
            if (block is SMI.IMyRadioAntenna) DeployClientControls<SMI.IMyRadioAntenna>();
            if (block is SMI.IMyReactor) DeployClientControls<SMI.IMyReactor>();
            if (block is SMI.IMyRemoteControl) DeployClientControls<SMI.IMyRemoteControl>();
            if (block is SMI.IMySensorBlock) DeployClientControls<SMI.IMySensorBlock>();
            if (block is SMI.IMyShipConnector) DeployClientControls<SMI.IMyShipConnector>();
            if (block is SMI.IMyShipDrill) DeployClientControls<SMI.IMyShipDrill>();
            if (block is SMI.IMyShipToolBase) DeployClientControls<SMI.IMyShipToolBase>();
            if (block is SMI.IMyTextPanel) DeployClientControls<SMI.IMyTextPanel>();
            if (block is SMI.IMyThrust) DeployClientControls<SMI.IMyThrust>();
            if (block is SMI.IMyUserControllableGun) DeployClientControls<SMI.IMyUserControllableGun>();
            if (block is SEGMI.IMyAirVent) DeployClientControls<SEGMI.IMyAirVent>();
            if (block is SEGMI.IMyGravityGeneratorBase) DeployClientControls<SEGMI.IMyGravityGeneratorBase>();
            if (block is SEGMI.IMyLandingGear) DeployClientControls<SEGMI.IMyLandingGear>();
            if (block is SEGMI.IMyMedicalRoom) DeployClientControls<SEGMI.IMyMedicalRoom>();
            if (block is SEGMI.IMyShipMergeBlock) DeployClientControls<SEGMI.IMyShipMergeBlock>();
            if (block is SEGMI.IMySoundBlock) DeployClientControls<SEGMI.IMySoundBlock>();
            if (block is SEGMI.IMyTimerBlock) DeployClientControls<SEGMI.IMyTimerBlock>();
            //if (block is SEGMI.IMyUpgradeModule) DeployClientControls<SEGMI.IMyUpgradeModule>();
            if (block is SEGMI.IMyVirtualMass) DeployClientControls<SEGMI.IMyVirtualMass>();

            Note(watchMe, "Checking Tier 3 - Anything directly inheriting from a tier 2 block.");
            if (block is SMI.IMyAdvancedDoor) DeployClientControls<SMI.IMyAdvancedDoor>();
            if (block is SMI.IMyAirtightDoorBase) DeployClientControls<SMI.IMyAirtightDoorBase>();
            if (block is SMI.IMyAssembler) DeployClientControls<SMI.IMyAssembler>();
            if (block is SMI.IMyCryoChamber) DeployClientControls<SMI.IMyCryoChamber>();
            if (block is SMI.IMyExtendedPistonBase) DeployClientControls<SMI.IMyExtendedPistonBase>();
            if (block is SMI.IMyLargeTurretBase) DeployClientControls<SMI.IMyLargeTurretBase>();
            if (block is SMI.IMyMotorStator) DeployClientControls<SMI.IMyMotorStator>();
            if (block is SMI.IMyMotorSuspension) DeployClientControls<SMI.IMyMotorSuspension>();
            if (block is SMI.IMyRefinery) DeployClientControls<SMI.IMyRefinery>();
            if (block is SMI.IMyReflectorLight) DeployClientControls<SMI.IMyReflectorLight>();
            if (block is SMI.IMyShipGrinder) DeployClientControls<SMI.IMyShipGrinder>();
            if (block is SMI.IMyShipWelder) DeployClientControls<SMI.IMyShipWelder>();
            if (block is SMI.IMySmallGatlingGun) DeployClientControls<SMI.IMySmallGatlingGun>();
            if (block is SMI.IMySmallMissileLauncher) DeployClientControls<SMI.IMySmallMissileLauncher>();
            if (block is SEGMI.IMyGravityGenerator) DeployClientControls<SEGMI.IMyGravityGenerator>();
            if (block is SEGMI.IMyGravityGeneratorSphere) DeployClientControls<SEGMI.IMyGravityGeneratorSphere>();
            if (block is SEGMI.IMyInteriorLight) DeployClientControls<SEGMI.IMyInteriorLight>();
            if (block is SEGMI.IMySpaceBall) DeployClientControls<SEGMI.IMySpaceBall>();

            Note(watchMe, "Checking Tier 4 - Anything directly inheriting from a tier 3 block.");
            if (block is SMI.IMyAirtightHangarDoor) DeployClientControls<SMI.IMyAirtightHangarDoor>();
            if (block is SMI.IMyAirtightSlideDoor) DeployClientControls<SMI.IMyAirtightSlideDoor>();
            if (block is SMI.IMyMotorAdvancedStator) DeployClientControls<SMI.IMyMotorAdvancedStator>();
            if (block is SMI.IMySmallMissileLauncherReload) DeployClientControls<SMI.IMySmallMissileLauncherReload>();
            if (block is SEGMI.IMyLargeConveyorTurretBase) DeployClientControls<SEGMI.IMyLargeConveyorTurretBase>();
            if (block is SEGMI.IMyLargeInteriorTurret) DeployClientControls<SEGMI.IMyLargeInteriorTurret>();

            Note(watchMe, "Checking Tier 5 - Anything directly inheriting from a tier 4 block.");
            if (block is SEGMI.IMyLargeGatlingTurret) DeployClientControls<SEGMI.IMyLargeGatlingTurret>();
            if (block is SEGMI.IMyLargeMissileTurret) DeployClientControls<SEGMI.IMyLargeMissileTurret>();

            ITerminalProperty coreProperty = null;
            coreProperty = block.GetProperty(CoreExtension.TryIncludeDelegate_PropertyId);
            if (coreProperty == null)
            {
                Note(watchMe, "WARNING: Inclusion Method inaccessible for type " + typeof(T).ToString());
            }
            else
            {
                Note(watchMe, "Inclusion Method Deployed: " + coreProperty.ToString());
            }
            coreProperty = block.GetProperty(CoreExtension.TryExcludeDelegate_PropertyId);
            if (coreProperty == null)
            {
                Note(watchMe, "WARNING: Exclusion Method inaccessible for type " + typeof(T).ToString());
            }
            else
            {
                Note(watchMe, "Exclusion Method Deployed: " + coreProperty.ToString());
            }
            coreProperty = block.GetProperty(CoreExtension.GetMethodSignatures_PropertyId);
            if (coreProperty == null)
            {
                Note(watchMe, "WARNING: Retrieval Method inaccessible for type " + typeof(T).ToString());
            }
            else
            {
                Note(watchMe, "Retrieval Method Deployed: " + coreProperty.ToString());
            }
            coreProperty = block.GetProperty(CoreExtension.InvokeMethodSignature_PropertyId);
            if (coreProperty == null)
            {
                Note(watchMe, "WARNING: Execution Method inaccessible for type " + typeof(T).ToString());
            }
            else
            {
                Note(watchMe, "Execution Method Deployed: " + coreProperty.ToString());
            }
        }
        /// <summary>
        /// This adds a delegate-list to the our client-hosts that allows mods to add their own delegates to a managed-access list.
        /// </summary>
        /// <typeparam name="T">The type of block to receive the delegate-list property.</typeparam>
        private void DeployClientControls<T>() where T : Sandbox.ModAPI.Ingame.IMyTerminalBlock
        {
            // Set this to true/false to enabled/disable debug-logging in this method...
            bool watchMe = true;
            Note(watchMe, "Deployment-Screening client-side controls on type " + typeof(T).ToString());
            if (SM.MyAPIGateway.TerminalControls == null)
            {
                m_init = false;
                return;
            }
            // Create a list to hold the block-type controls.
            List<IMyTerminalControl> controls = new List<IMyTerminalControl>();
            // Acquire the controls known to the block-type.
            SM.MyAPIGateway.TerminalControls.GetControls<T>(out controls);
            // Check for the existence of our core component.
            if (controls == null) controls = new List<IMyTerminalControl>();
            if (!controls.Any(c => c.Id == CoreExtension.TryIncludeDelegate_PropertyId))
            {
                Note(watchMe, "Deploying inclusion control for type " + typeof(T).ToString());
                DefaultCreateAndAddProperty<T, Delegate>(CoreExtension.TryIncludeDelegate_PropertyId, CoreExtension.GetInclusionMethod, (b, v) => { });
                controls = new List<IMyTerminalControl>();
                SM.MyAPIGateway.TerminalControls.GetControls<T>(out controls);
            }
            if (!controls.Any(c => c.Id == CoreExtension.TryExcludeDelegate_PropertyId))
            {
                Note(watchMe, "Deploying exclusion control for type " + typeof(T).ToString());
                DefaultCreateAndAddProperty<T, Delegate>(CoreExtension.TryExcludeDelegate_PropertyId, CoreExtension.GetExclusionMethod, (b, v) => { });
                controls = new List<IMyTerminalControl>();
                SM.MyAPIGateway.TerminalControls.GetControls<T>(out controls);
            }
            if (!controls.Any(c => c.Id == CoreExtension.GetMethodSignatures_PropertyId))
            {
                Note(watchMe, "Deploying retrieval control for type " + typeof(T).ToString());
                DefaultCreateAndAddProperty<T, Delegate>(CoreExtension.GetMethodSignatures_PropertyId, CoreExtension.GetRetrievalMethod, (b, v) => { });
                controls = new List<IMyTerminalControl>();
                SM.MyAPIGateway.TerminalControls.GetControls<T>(out controls);
            }
            if (!controls.Any(c => c.Id == CoreExtension.InvokeMethodSignature_PropertyId))
            {
                Note(watchMe, "Deploying execution control for type " + typeof(T).ToString());
                DefaultCreateAndAddProperty<T, Delegate>(CoreExtension.InvokeMethodSignature_PropertyId, CoreExtension.GetExecutionMethod, (b, v) => { });
                controls = new List<IMyTerminalControl>();
                SM.MyAPIGateway.TerminalControls.GetControls<T>(out controls);
            }
            // Iterate over the known controls.
            /**/foreach (IMyTerminalControl control in controls)
                Note(watchMe, "\t<" + control.Id + ">:\t" + control.ToString());/**/
        }
        /// <summary>
        /// This generates a property with the default settings I use and adds it to the targeted block-type.
        /// </summary>
        /// <typeparam name="T">The type of block to receive the new property.</typeparam>
        /// <typeparam name="S">The type of data to be stored in the new property.</typeparam>
        /// <param name="id">The identifier associated with the new property.</param>
        /// <param name="getter">The function returning the property-value associated with the given block.</param>
        /// <param name="setter">The action assigning the property-value associated with the given block.</param>
        private void DefaultCreateAndAddProperty<T, S>(string id, Func<SM.IMyTerminalBlock, S> getter, Action<SM.IMyTerminalBlock, S> setter)
        {
            // Set this to true/false to enabled/disable debug-logging in this method...
            bool watchMe = false;
            Note(watchMe, "Generating " + id + " property...");
            try
            {
                IMyTerminalControlProperty<S> customProperty = SM.MyAPIGateway.TerminalControls.CreateProperty<S, T>(id);
                Note(watchMe, "Assigning visibility behavior...");
                customProperty.Visible = b => false;
                Note(watchMe, "Assigining enabled behavior...");
                customProperty.Enabled = b => b.IsWorking;
                Note(watchMe, "Assigining getter behavior...");
                customProperty.Getter = getter;
                Note(watchMe, "Assigning setter behavior...");
                customProperty.Setter = setter;
                Note(watchMe, "Adding control... " + customProperty.ToString());
                SM.MyAPIGateway.TerminalControls.AddControl<T>(customProperty);
            }
            catch (Exception ex)
            {
                Note(watchMe, "An exception occurred creating property-id " + id.ToString() + " of Data-Type " + typeof(S).ToString() + " for Block-Type " + typeof(T).ToString() + "\n" + ex.ToString());
            }
        }
    }
}
