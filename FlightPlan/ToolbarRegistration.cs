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
        void OnGUI()
        {
            if (FlightPlan.guiLabelStyle == null)
            {
                FlightPlan.guiLabelStyle = new GUIStyle(GUI.skin.label);
                FlightPlan.guiWhiteLabelStyle = new GUIStyle(GUI.skin.label);
                FlightPlan.guiBlueLabelStyle = new GUIStyle(GUI.skin.label);
                FlightPlan.guiWhiteLabelStyle.normal.textColor = Color.white;
                // Color values are in the range 0.0 - 1.0, not 0 - 255.
                FlightPlan.guiBlueLabelStyle.normal.textColor = new Color(172f/255f, 172f/255f, 243f/255f);

                FlightPlan.guiButtonStyle = new GUIStyle(GUI.skin.button);                
                FlightPlan.guiGreenButtonStyle = new GUIStyle(GUI.skin.button);
                FlightPlan.guiGreenButtonStyle.normal.textColor = Color.green;
            }
        }
    }
}