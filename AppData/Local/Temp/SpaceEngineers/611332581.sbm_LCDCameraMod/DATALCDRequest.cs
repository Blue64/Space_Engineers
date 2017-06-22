using System;
using Sandbox.ModAPI;
using LCDCameraMod.Managers;

namespace LCDCameraMod.DataMessage
{
    public class DataLCDRequest : DataHandlerBase
    {
        public override long GetDataId()
        {
            return 6002;
        }

        public override void HandleCommand(ulong steamId, byte[] data)
        {
            string selectionList = MyAPIGateway.Utilities.SerializeToXML<CameraSelectionManager>(CameraSelectionManager.Instance);
            Communication.SendDataToPlayer(steamId, 6003, selectionList);
        }
    }
}
