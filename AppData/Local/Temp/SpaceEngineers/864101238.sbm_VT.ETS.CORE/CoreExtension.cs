/*
 * VerdanTech Extensible Terminal System Core
 * Copyright © 2017, VerdanTech
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SM = Sandbox.ModAPI;
using SMI = Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;

namespace VT.ETS.CORE
{
    class CoreExtension
    {
        /// <summary>
        /// This keeps track of all clients (as well as servers) recognized by this mod.
        /// </summary>
        public static Dictionary<long, CoreExtension> extensionSet = new Dictionary<long, CoreExtension>();

        public const string TryIncludeDelegate_PropertyId = "VT.ETS.CORE.TryIncludeDelegate";
        public const string TryExcludeDelegate_PropertyId = "VT.ETS.CORE.TryExcludeDelegate";
        public const string GetMethodSignatures_PropertyId = "VT.ETS.CORE.GetMethodSignatures";
        public const string InvokeMethodSignature_PropertyId = "VT.ETS.CORE.InvokeMethodSignature";
        public const string Documentation_PropertyId = "VT.ETS.CORE.API";

        public static string VT_ETS_CORE_API(SM.IMyTerminalBlock b)
        {
            string documentation = @"
public class VT_ETS_CORE
{
    public static IMyGridTerminalSystem SYSTEM = null;

    bool initialized = false;

    const string Get_PID = ""VT.ETS.CORE.GetMethodSignatures"";

    const string Run_PID = ""VT.ETS.CORE.InvokeMethodSignature"";

    Delegate Get_DEL = null;

    Delegate Run_DEL = null;

    IMyTerminalBlock host = null;

    public IMyTerminalBlock Host { get { return host; } }

    protected VT_ETS_CORE() { }

    protected VT_ETS_CORE(long id)
    {
        if (SYSTEM != null)
        {
            host = SYSTEM.GetBlockWithId(id);
            if (host != null)
            {
                Get_DEL = host.GetValue<Delegate>(Get_PID);
                Run_DEL = host.GetValue<Delegate>(Run_PID);
                if (Get_DEL != null && Run_DEL != null) initialized = true;
            }
        }
    }

    public static bool Seek(long id, out VT_ETS_CORE target)
    {
        target = new VT_ETS_CORE(id);
        return target.initialized;
    }

    public List<string> GetSignatures()
    {
        return (List<string>)Get_DEL.DynamicInvoke();
    }

    public object RunSignature(string signature, params object[] arguments)
    {
        return Run_DEL.DynamicInvoke(signature, arguments);
    }
}";
            return documentation;
        }

        public Delegate includer;
        public Delegate excluder;
        public Delegate getter;
        public Delegate invoker;
        public static Delegate GetInclusionMethod(SM.IMyTerminalBlock b)
        {
            Delegate value = null;
            CoreExtension e;
            if (extensionSet.TryGetValue(b.EntityId, out e))
            {
                value = e.includer;
            }
            return value;
        }
        public static Delegate GetExclusionMethod(SM.IMyTerminalBlock b)
        {
            Delegate value = null;
            CoreExtension e;
            if (extensionSet.TryGetValue(b.EntityId, out e))
            {
                value = e.excluder;
            }
            return value;
        }
        public static Delegate GetRetrievalMethod(SM.IMyTerminalBlock b)
        {
            Delegate value = null;
            CoreExtension e;
            if (extensionSet.TryGetValue(b.EntityId, out e))
            {
                value = e.getter;
            }
            return value;
        }
        public static Delegate GetExecutionMethod(SM.IMyTerminalBlock b)
        {
            Delegate value = null;
            CoreExtension e;
            if (extensionSet.TryGetValue(b.EntityId, out e))
            {
                value = e.invoker;
            }
            return value;
        }


        public bool debugging = false;
        public Logger debugLogger = null;
        protected Sandbox.ModAPI.IMyTerminalBlock host;
        public Sandbox.ModAPI.IMyTerminalBlock Host
        {
            get { return host; }
        }
        /// <summary>
        /// Generates a client-extension on the provided host-block.
        /// </summary>
        /// <param name="target">The client-host this extension works for.</param>
        public CoreExtension(Sandbox.ModAPI.IMyTerminalBlock target)
        {
            // Set this to true/false to enabled/disable debug-logging in this method...
            bool watchMe = true;
            debugLogger = new Logger("Extension");
            debugging = true;
            host = target;
            Note(watchMe, "Initializing new extension...");
            Note(watchMe, "\tExtension host-grid: " + Host.CubeGrid.EntityId.ToString() + " <" + Host.CubeGrid.ToString() + ">");
            Note(watchMe, "\tExtension host-block: " + Host.EntityId.ToString() + " <" + Host.ToString() + ">");
            host.OnClose += ShutdownSequence;
            includer = new DelegationOfManageDelegate(TryIncludeDelegate);
            excluder = new DelegationOfManageDelegate(TryExcludeDelegate);
            getter = new DelegationOfGetMethodSignatures(GetMethodSignatures);
            invoker = new DelegationOfInvokeMethodSignature(InvokeMethodSignature);
        }
        /// <summary>
        /// This is a diagnostic print-command with a "should-I-really-print-it" flag to make revising log entry batches quicker and easier.
        /// </summary>
        /// <param name="localWatch">This decides whether or not to actually print-to-file.</param>
        /// <param name="message">The text to be printed to the log-file.</param>
        protected void Note(bool localWatch, string message)
        {
            if (debugging && localWatch)
                debugLogger.WriteLine(message);
        }
        /// <summary>
        /// Handles the deactivation and decommissioning of the extension.
        /// </summary>
        /// <param name="e">The entity whose closure this method needs to unsubscribe from.</param>
        private void ShutdownSequence(VRage.ModAPI.IMyEntity e)
        {
            // Set this to true/false to enabled/disable debug-logging in this method...
            bool watchMe = true;
            Note(watchMe, "Disconnecting Extension...");
            e.OnClose -= ShutdownSequence;
            Note(watchMe, "Terminating Extension...");
            debugLogger.CloseLog();
            extensionSet.Remove(host.EntityId);
        }
        /// <summary>
        /// A mapping of the signature-strings of the delegates to their actual values, for opaque network access purposes.
        /// </summary>
        public Dictionary<string, Delegate> extensionDelegates = new Dictionary<string, Delegate>();
        /// <summary>
        /// Attempts to determine if the provided delegate is in fact from an ingame script or a core-delegate reference.
        /// </summary>
        /// <param name="d">The delegate whose background we're checking.</param>
        /// <returns>True if the delegate appears to be from a script or the core, false otherwise.</returns>
        private bool IsBlacklisted(Delegate d)
        {
            bool watchMe = true;
            Note(watchMe, "Evaluating permissions...");
            if ((d.ToString()).Split('+').First() == "Program")
            {
                Note(watchMe, "Access Denied: Script-Derived delegate management is prohibited.");
                return true;
            }
            else if ((d.ToString()).Split('+').First() == "VT.ETS.CORE.CoreExtension")
            {
                Note(watchMe, "Access Denied: Core delegate management is prohibited.");
                return true;
            }
            return false;
        }
        /// <summary>
        /// Represents the editing behavior allowing mods to manage their own methods for network-access.
        /// </summary>
        /// <param name="managed">The delegate to be managed for the extension set.</param>
        /// <returns>True if the edit was successfully performed, false if not.</returns>
        private delegate bool DelegationOfManageDelegate(Delegate managed);
        /// <summary>
        /// Attempts to add the specified delegate to the network exposure set.
        /// </summary>
        /// <param name="d">The delegate to be added to the extension set.</param>
        /// <returns>True if the addition was successfully performed, false if not.</returns>
        private bool TryIncludeDelegate(Delegate d)
        {
            // Set this to true/false to enabled/disable debug-logging in this method...
            bool watchMe = true;
            Note(watchMe, "Attempting to include the extension delegate: " + d.ToString());
            // If we only block the inclusion of the inclusion on the network, a script can just provide dummy-access through itself.
            // So, Include has an extra layer to prevent the exposure of it to scripts which might create persistent references.
            if (IsBlacklisted(d)) return false;
            Note(watchMe, "Delegate target-object: " + (d.Target ?? "NULL").ToString());
            string methodSignature = d.Method.ToString();
            Note(watchMe, "Delegate signature-string: " + methodSignature);
            if (extensionDelegates.ContainsKey(methodSignature))
            {
                Note(watchMe, "Access Denied: The specified delegate signature conflicts with an existing signature.");
                return false;
            }
            extensionDelegates.Add(d.Method.ToString(), d);
            return true;
        }
        /// <summary>
        /// Attempts to remove the specified delegate from the network exposure set.
        /// </summary>
        /// <param name="d">The delegate to be removed from the extension set.</param>
        /// <returns>True if the removal was successfully performed, false if not.</returns>
        private bool TryExcludeDelegate(Delegate d)
        {
            // Set this to true/false to enabled/disable debug-logging in this method...
            bool watchMe = true;
            Note(watchMe, "Attempting to exclude the extension delegate: " + d.ToString());
            // Alternately, Exclude is much easier because mod delegates can't be gotten directly in-game.
            // However, it gets the same treatment in case a client acts strangely, to ensure our consistency.
            if (IsBlacklisted(d)) return false;
            string methodSignature = d.Method.ToString();
            Note(watchMe, "Delegate signature-string: " + methodSignature);
            if (!extensionDelegates.ContainsKey(methodSignature))
            {
                Note(watchMe, "Access Denied: The specified delegate signature does not exist.");
                return false;
            }
            extensionDelegates.Remove(d.Method.ToString());
            return true;
        }
        /// <summary>
        /// Represents the method-signature retrieval function of the client.
        /// </summary>
        /// <returns>The list of method-signatures known to the client.</returns>
        public delegate List<string> DelegationOfGetMethodSignatures();
        /// <summary>
        /// Permits the client to report the known signature-strings for the methods it has available.
        /// </summary>
        /// <returns>The list of signature-strings identifying the available delegates on the client.</returns>
        public List<string> GetMethodSignatures()
        {
            // Set this to true/false to enabled/disable debug-logging in this method...
            bool watchMe = true;
            Note(watchMe, "Extracting " + extensionDelegates.Keys.Count.ToString() + " keys from dictionary...");
            return extensionDelegates.Keys.ToList();
        }
        /// <summary>
        /// Represents the invocation of a delegate belonging to the client.
        /// </summary>
        /// <param name="signature">The stringification of the delegate method to be invoked.</param>
        /// <param name="arguments">The arguments belonging to the delegate in question.</param>
        /// <returns>The return value from the invoked delegate.</returns>
        public delegate object DelegationOfInvokeMethodSignature(string signature, object[] arguments);
        /// <summary>
        /// Attempts to execute the invocation of a delegate belonging to the client.
        /// </summary>
        /// <param name="signature">The stringification of the delegate method to be invoked.</param>
        /// <param name="arguments">The arguments belonging to the delegate in question.</param>
        /// <returns>The return value from the invoked delegate.</returns>
        public object InvokeMethodSignature(string signature, object[] arguments)
        {
            // Set this to true/false to enabled/disable debug-logging in this method...
            bool watchMe = true;
            object networkExchange = null;
            Delegate method = null;
            try
            {
                if (Host.IsWorking)
                {
                    Note(watchMe, "Attempting to identify the signature: " + signature);
                    if (extensionDelegates.TryGetValue(signature, out method))
                    {
                        Note(watchMe, "Attempting to invoke with " + arguments.Length.ToString() + " arguments...");
                        networkExchange = method.DynamicInvoke(arguments);
                        Note(watchMe, "Signature invocation succeeded: " + (networkExchange ?? "Void").ToString());
                    }
                    else
                    {
                        networkExchange = new Exception("ETS 405 - The specified resource, ID " + signature + ", is not a valid method-signature on " + Host.ToString());
                        Note(watchMe, "Subject of invocation is not an accessible method-signature:\n" + networkExchange.ToString());
                    }
                }
                else
                {
                    Note(watchMe, "Invocation attempted on non-working host, generating exception...");
                    networkExchange = new Exception("ETS 503 - The specified resource, ID " + Host.EntityId.ToString() + ", is not responding.");
                }
            }
            catch (Exception ex)
            {
                Note(watchMe, "An exception occurred during an invocation on " + this.Host.ToString() + "\n" + ex.ToString());
                networkExchange = ex;
            }
            return networkExchange;
        }
    }
}
