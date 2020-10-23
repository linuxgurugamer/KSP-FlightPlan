using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommNet;

namespace FlightPlan_NS
{
    public partial class FlightPlan
    {
        const string FLIGHTPLAN = "FlightPlan";
        const string DIR = "GameData/FlightPlan/PluginData";
        const string FILE = DIR + "/FlightPlan.cfg";
        void SaveData()
        {
            ConfigNode filenode = new ConfigNode();
            ConfigNode node = new ConfigNode(FLIGHTPLAN);
            node.AddValue("uiCoreActive", uiCoreActive);
            filenode.AddNode(node);
            if (!Directory.Exists(DIR))
                Directory.CreateDirectory(DIR);
            filenode.Save(FILE);
        }

        void LoadData()
        {
            if (File.Exists(FILE))
            {
                ConfigNode filenode = ConfigNode.Load(FILE);
                ConfigNode node = null;
                if (filenode.TryGetNode(FLIGHTPLAN, ref node))
                {
                    if (node.TryGetValue("uiCoreActive", ref uiCoreActive))
                        oldUiCoreActive = uiCoreActive;
                }
            }
        }
    }
}
