using UnityEngine;
using ToolbarControl_NS;

namespace FlightPlan_NS
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RegisterToolbar : MonoBehaviour
    {
        void Start()
        {
            ToolbarControl.RegisterMod(FlightPlan.MODID, FlightPlan.MODNAME);
        }
    }
}