using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.Common;
using Sandbox.Common.Components;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Game;
using Sandbox.ModAPI;
//using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRageMath;
using IMyCubeBlock = Sandbox.ModAPI.IMyCubeBlock;
namespace BreachCharge
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Warhead))]
    class BreachCharge : MyGameLogicComponent
    {
        MyObjectBuilder_EntityBase m_objectBuilder;
        private string SubTypeNameLarge = "BreachCharge";
        private bool _didInit = false;
        IMyCubeBlock block;

        public override void Close()
        {
            if (block == null)
                return;
            if (block.BlockDefinition.SubtypeName == SubTypeNameLarge)
            {
                if (_didInit == true)
                {
                    //MyLogger.Default.WriteLine("Close was called");
                    block.OnMarkForClose -= OnMarkForClose;
                }
            }
            else
            {
                return;
            }
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;

            m_objectBuilder = objectBuilder;
            //MyLogger.Default.ToScreen = true;
            //MyLogger.Default.WriteLine("Initting a warhead");


        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            DoInit();
        }

        void DoInit()
        {
            //MyLogger.Default.WriteLine("Got inside doinit");
            if (_didInit) return;
            _didInit = true;

            if (Entity == null)
            {
                //MyLogger.Default.WriteLine("Block was null! bailin");
                _didInit = false;
                return;
            }

            //MyLogger.Default.ToScreen = true;
            //MyLogger.Default.WriteLine("Initting a warhead");
            block = (IMyCubeBlock)Entity;
            if (block.BlockDefinition.SubtypeName == SubTypeNameLarge)
            {
                //MyLogger.Default.WriteLine("Just Placed a breach charge");
                block.OnMarkForClose += OnMarkForClose;
            }
            else
            {
                return;
            }
        }


        private void OnMarkForClose(IMyEntity myEntity)
        {
            if (MyAPIGateway.Players.Count == 0)
                return;
            if (Entity == null)
            {
                //MyLogger.Default.WriteLine("Bailin!");
                return;
            }
            var position = Entity.GetPosition();
            var range = 200.0;
            var sphere = new BoundingSphereD(position, range);
            var grids = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<IMyCubeGrid>();
            var affectedBlocks = new List<IMySlimBlock>();
            foreach (var grid in grids)
            {
                //MyLogger.Default.WriteLine("Finding other breach charges");
                grid.GetBlocks(affectedBlocks, x => x.FatBlock != null
                                                    && x.FatBlock.BlockDefinition.SubtypeName==SubTypeNameLarge
                                                    && x.FatBlock.GetIntersectionWithSphere(ref sphere));
            }

            //MyLogger.Default.WriteLine("found"+affectedBlocks.Count+ " charges");

            foreach (var blk in affectedBlocks)
            {
                //MyLogger.Default.WriteLine("detonating other charges");
                if (blk != null)
                {
                    var cube = (IMyTerminalBlock)blk.FatBlock;
                    cube.GetActionWithName("Detonate").Apply(cube);
                }
            }
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return m_objectBuilder;
        }
    }


    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class LoggerSession : MySessionComponentBase
    {
        protected override void UnloadData()
        {
            base.UnloadData();
            MyLogger.DefaultClose();
        }
    }

    /// <summary>
    /// Borrowed and modified from official API sample mission mod.  Thanks!
    /// </summary>
    class MyLogger
    {
        private System.IO.TextWriter m_writer;
        private int m_indent;
        private StringBuilder m_cache = new StringBuilder();

        private bool _isFileOpen;
        private bool _isClosed;
        private string _fileName;

        public bool ToScreen { get; set; }

        private static MyLogger _sDefault;

        public static MyLogger Default
        {
            get { return _sDefault ?? (_sDefault = new MyLogger("DefaultLog.txt")); }
        }

        public static void DefaultClose()
        {
            if (_sDefault != null) _sDefault.Close();
        }

        public MyLogger(string logFile)
        {
            _fileName = logFile;
            ReadyFile();
        }

        private bool ReadyFile()
        {
            if (_isFileOpen) return true;
            if (MyAPIGateway.Utilities != null)
            {
                m_writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(_fileName, typeof(MyLogger));
                _isFileOpen = true;
            }
            return _isFileOpen;
        }

        public void IncreaseIndent()
        {
            m_indent++;
        }

        public void DecreaseIndent()
        {
            if (m_indent > 0)
                m_indent--;
        }

        public void WriteLine(string text)
        {
            if (ToScreen && MyAPIGateway.Utilities != null)
            {
                MyAPIGateway.Utilities.ShowMessage("Log", text);
            }
            if (_isClosed) return;
            if (!ReadyFile())
            {
                m_cache.Append(text);
                return;
            }
            if (m_cache.Length > 0)
                m_writer.WriteLine(m_cache);
            m_cache.Clear();
            m_cache.Append(DateTime.Now.ToString("[HH:mm:ss] "));
            for (int i = 0; i < m_indent; i++)
                m_cache.Append("\t");
            m_writer.WriteLine(m_cache.Append(text));
            m_writer.Flush();
            m_cache.Clear();
        }

        public void Write(string text)
        {
            if (ToScreen)
            {
                MyAPIGateway.Utilities.ShowMessage("Log", text);
            }
            m_cache.Append(text);
        }


        internal void Close()
        {
            _isClosed = true;
            if (m_cache.Length > 0)
                m_writer.WriteLine(m_cache);
            m_writer.Flush();
            m_writer.Close();
        }
    }
}


