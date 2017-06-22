using System;
using Sandbox.ModAPI;
using LCDCameraMod.Managers;

namespace LCDCameraMod.DataMessage
{
    public class DataLCDAddSelection : DataHandlerBase
    {
        public override long GetDataId()
        {
            return 6000;
        }

        public override void HandleCommand(ulong steamId, byte[] data)
        {
            var selection = MyAPIGateway.Utilities.SerializeFromXML<LCDSelectionItem>(Convert.ToString(data));
            CameraSelectionManager.Instance.SelectCamera(selection.LCDEntityId, selection.Selection);
        }
    }
}
