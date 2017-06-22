using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LCDCameraMod.Managers
{
    public class CameraSelectionManager
    {
        private Dictionary<long, List<string>> m_selectionList;
        private static CameraSelectionManager m_instance = null;
        public static CameraSelectionManager Instance
        {
            get
            {
                if(m_instance == null)
                {
                    m_instance = new CameraSelectionManager();
                }

                return m_instance;
            }
        }

        public List<KeyValuePair<long, List<string>>> SelectionList
        {
            get
            {
                return m_selectionList.ToList();
            }
        }

        public CameraSelectionManager()
        {
            m_selectionList = new Dictionary<long, List<string>>();
        }

        public void SelectCamera(long lcdEntityId, string cameraName)
        {
            if (!m_selectionList.ContainsKey(lcdEntityId))
                m_selectionList.Add(lcdEntityId, new List<string>());

            if (!m_selectionList[lcdEntityId].Contains(cameraName))
                m_selectionList[lcdEntityId].Add(cameraName);
        }

        public void DeselectCamera(long lcdEntityId, string cameraName)
        {
            if (!m_selectionList.ContainsKey(lcdEntityId))
                return;

            if (m_selectionList[lcdEntityId].Contains(cameraName))
                m_selectionList[lcdEntityId].Remove(cameraName);
        }

        public void Save()
        {

        }
    }

    public class LCDSelectionItem
    {
        public long LCDEntityId;
        public string Selection;
    }
}
