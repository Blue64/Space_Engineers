using System;
using Sandbox.ModAPI;
using LCDCameraMod.Managers;

namespace LCDCameraMod.DataMessage
{
    public class DataLCDRemoveSelection : DataHandlerBase
    {
        public override long GetDataId()
        {
            return 6001;
        }

        public override void HandleCommand(ulong steamId, byte[] data)
        {
            var selection = MyAPIGateway.Utilities.SerializeFromXML<LCDSelectionItem>(Convert.ToString(data));
            CameraSelectionManager.Instance.DeselectCamera(selection.LCDEntityId, selection.Selection);
        }
    }
}
