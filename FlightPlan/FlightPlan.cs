﻿using System;
using UnityEngine;
using System.Collections.Generic;
using KSP.IO;
using KSP.UI.Screens;
using FlightPlan_KACWrapper;
using System.Linq;
using ToolbarControl_NS;

namespace FlightPlan_NS
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public partial class FlightPlan : MonoBehaviour
    {
        ToolbarControl toolbarControl;

        private enum EntryType
        {
            Encounter,
            Escape,
            Maneuver,
            Crater,
            Burn,
            SnacksLanded,
            SnacksOrbit,
            DivingCourse,
            None
        }

        private struct PlanEntry
        {
            internal double UT;
            internal MapObject obj;
            internal EntryType type;
            internal double magnitude;
            internal PlanEntry(double UT, MapObject obj, EntryType type = EntryType.None, double magnitude = 0)
            {
                this.UT = UT;
                this.obj = obj;
                this.type = type;
                this.magnitude = magnitude;
            }
        }

        static private FlightPlan instance;

        private bool available = false;
        private bool active = false;
        private bool hidden = false;
        private Rect winRect = new Rect();
        private int winId;
        private bool refresh = true;
        private bool showAsUT = false;

        private string msgUpgrade = "Upgrade Tracking Station to access Flight Plan!";

        private PluginConfiguration config;

        private List<PlanEntry> flightPlan = new List<PlanEntry>();
        private DateTime lastTime;

        private UICore.UICore UI;
        bool uiCoreActive = true, oldUiCoreActive = true;

        internal static GUIStyle guiLabelStyle, guiBlueLabelStyle;
        internal static GUIStyle guiButtonStyle, guiGreenButtonStyle, guiWhiteLabelStyle;

        public void Awake()
        {
            if (instance != null)
            {
                Destroy(this);
                return;
            }
            instance = this;
            LoadData();
        }

        internal const string MODID = "FlightPlan_NS";
        internal const string MODNAME = "Flight Plan";

        void LoadUICore()
        {
            if (UI == null)
            {
                UI = new UICore.UICore();
                UI.LoadConfig("GameData/FlightPlan/Skin/FlightPlan.cfg");
            }
        }
        private void Start()
        {
            //colors = new Dictionary<Color, Texture2D>();

            //Instance = this;
            available = (PSystemSetup.Instance.GetSpaceCenterFacility("TrackingStation").GetFacilityLevel() > 0);
            lastTime = DateTime.Now;
            config = PluginConfiguration.CreateForType<FlightPlan>();
            config.load();

            if (uiCoreActive)
            {
                LoadUICore();
            }
            winId = GUIUtility.GetControlID(FocusType.Passive);
            winRect = config.GetValue<Rect>(this.name, new Rect(0, 0, Screen.width / 2, Screen.height / 2));
            showAsUT = config.GetValue<bool>("showAsUT", false);

            GameEvents.onHideUI.Add(OnHide);
            GameEvents.onShowUI.Add(OnUnHide);
            GameEvents.onGamePause.Add(OnHide);
            GameEvents.onGameUnpause.Add(OnUnHide);

            KACWrapper.InitKACWrapper();
            if (KACWrapper.APIReady)
                KACWrapper.KAC.onAlarmStateChanged += KACWrapper_KAC_onAlarmStateChanged;

            toolbarControl = gameObject.AddComponent<ToolbarControl>();
            toolbarControl.AddToAllToolbars(onAppTrue, onAppFalse,
                        ApplicationLauncher.AppScenes.SPACECENTER |
                        ApplicationLauncher.AppScenes.TRACKSTATION |
                        ApplicationLauncher.AppScenes.FLIGHT |
                        ApplicationLauncher.AppScenes.MAPVIEW,
                        MODID,
                        "flightPlanButton",
                        "FlightPlan/Textures/flight-plan-icon",
                        "FlightPlan/Textures/flight-plan-icon-toolbar",
                        MODNAME
                );
        }

        private void OnDestroy()
        {
            //Debug.Log("OnDestory");
            config.SetValue(this.name, winRect);
            config.SetValue("showAsUT", showAsUT);
            config.save();

            UnlockControls();

            toolbarControl.OnDestroy();
            Destroy(toolbarControl);

            GameEvents.onHideUI.Remove(OnHide);
            GameEvents.onShowUI.Remove(OnUnHide);
            GameEvents.onGamePause.Remove(OnHide);
            GameEvents.onGameUnpause.Remove(OnUnHide);

            if (KACWrapper.APIReady)
                KACWrapper.KAC.onAlarmStateChanged -= KACWrapper_KAC_onAlarmStateChanged;

            if (instance == this)
                instance = null;
        }

        void KACWrapper_KAC_onAlarmStateChanged(KACWrapper.KACAPI.AlarmStateChangedEventArgs e)
        {

        }


        public void onAppTrue()
        {
            if (!available)
                ScreenMessages.PostScreenMessage(msgUpgrade);
            else
                active = true;
        }

        public void onAppFalse()
        {
            active = false;
            refresh = true;
            UnlockControls();
        }

        private void OnHide()
        {
            hidden = true;
            UnlockControls();
        }

        private void OnUnHide()
        {
            hidden = false;
        }

        internal virtual void onToggle()
        {
            if (!available)
            {
                ScreenMessages.PostScreenMessage(msgUpgrade);
                return;
            }

            active = !active;
            if (!active)
            {
                refresh = true;
                UnlockControls();
            }
        }

        private void Update()
        {
            // Refresh once per second to reduce garbage generation
            if (lastTime.AddSeconds(1) > DateTime.Now || !active || hidden)
                return;

            if (FlightGlobals.ActiveVessel != null)
            {
                BuildFlightPlan(FlightGlobals.ActiveVessel);
                refresh = true;
                lastTime = DateTime.Now;
            }
        }

        private void OnGUI()
        {

            if (active && !hidden)
            {
                if (refresh)
                {
                    winRect.height = 0;
                    winRect.width = 0;
                    refresh = false;
                }

                if (uiCoreActive)
                    winRect = UI.Layout.Window(winId, winRect, RenderWindow, "Flight Plan");
                else
                    winRect = GUILayout.Window(winId, winRect, RenderWindow, "Flight Plan");

                if (winRect.Contains(Event.current.mousePosition))
                {
                    LockControls();
                }
                else
                {
                    UnlockControls();
                }
            }
        }

        private ControlTypes LockControls()
        {
            return InputLockManager.SetControlLock(ControlTypes.ALLBUTTARGETING, this.name);
        }

        private void UnlockControls()
        {
            InputLockManager.RemoveControlLock(this.name);
        }

        private void RenderWindow(int windowId)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            GUILayout.BeginVertical(GUILayout.MinWidth(400));

            if (vessel != null)
            {
                if (uiCoreActive)
                    UI.Layout.Label("Vessel:");
                else
                    GUILayout.Label("Vessel:");
                TargetFocusButton(vessel);
                if (uiCoreActive)
                    UI.Layout.Label("Current SOI:");
                else
                    GUILayout.Label("Current SOI:");
                TargetFocusButton(vessel.mainBody);

                if (vessel.targetObject != null)
                {
                    if (uiCoreActive)
                        UI.Layout.Label("Target:");
                    else
                        GUILayout.Label("Target:");
                    TargetFocusButton(vessel.targetObject);
                }

                if (uiCoreActive)
                    UI.Layout.Label("Plan entries:", UI.Palette.Col("blue"));
                else
                {
                    GUILayout.Label("Plan entries:", guiBlueLabelStyle);
                }
                RenderFlightPlan();
            }

            if (uiCoreActive)
            {
                UI.Layout.HR(5);
                showAsUT = UI.Layout.Toggle(showAsUT, "Universal Time");
                uiCoreActive = UI.Layout.Toggle(uiCoreActive, "UI Core Active");
                UI.Layout.HR(5);
            }
            else
            {
                GUILayout.Label("", GUILayout.Height(5));
                showAsUT = GUILayout.Toggle(showAsUT, "Universal Time");
                uiCoreActive = GUILayout.Toggle(uiCoreActive, "UI Core Active");
                if (uiCoreActive != oldUiCoreActive)
                {
                    oldUiCoreActive = uiCoreActive;
                    if (uiCoreActive)
                    {
                        LoadUICore();
                    }
                    SaveData();
                }
                GUILayout.Label("", GUILayout.Height(5));
            }
            // missing green for GUILayout.Button
            if ((uiCoreActive && UI.Layout.Button("Close", UI.Palette.Col("green"))) ||
                (!uiCoreActive && GUILayout.Button("Close", guiGreenButtonStyle)))
            {
                toolbarControl.SetFalse();
            }
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void TargetFocusButton(ITargetable ob)
        {
            MapObject mo = null;
            if (ob is Vessel)
            {
                Vessel v = (Vessel)ob;
                mo = v.mapObject;
            }
            else if (ob is CelestialBody)
            {
                CelestialBody b = (CelestialBody)ob;
                mo = b.MapObject;
            }

            if (mo != null)
            {

                if ((uiCoreActive && UI.Layout.Button(mo.Discoverable.RevealName(), UI.Palette.Col("green"))) ||
                (!uiCoreActive && GUILayout.Button(mo.Discoverable.RevealName(), guiGreenButtonStyle)))
                {
                    if (HighLogic.LoadedSceneIsFlight)
                        MapView.EnterMapView();
                    PlanetariumCamera.fetch.SetTarget(mo);
                }
            }
        }

        private void BuildFlightPlan(Vessel vessel)
        {
            flightPlan.Clear();

            // Eat snacks while landed
            if (vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.PRELAUNCH)
            {
                flightPlan.Add(new PlanEntry(0, vessel.mainBody.MapObject, EntryType.SnacksLanded));
                return;
            }

            // Diving course while splashed
            if (vessel.situation == Vessel.Situations.SPLASHED)
            {
                flightPlan.Add(new PlanEntry(0, vessel.mainBody.MapObject, EntryType.DivingCourse));
                return;
            }

            int maneuverCount = vessel.patchedConicSolver.maneuverNodes.Count;

            Orbit orb = vessel.orbit;
            List<Orbit> plan;

            double ooopsUT = 0;

            if (maneuverCount > 0)
                plan = vessel.patchedConicSolver.flightPlan;
            else
            {
                plan = new List<Orbit>();
                while (orb != null)
                {
                    plan.Add(orb);
                    orb = orb.nextPatch;
                }
            }


            // Filter out nearby patches with the same reference body appeared after maneuver added
            CelestialBody prev = vessel.mainBody;
            foreach (Orbit patch in plan)
            {
                // Trust me, I'm a programmer
                if (patch.PeR == 0) continue;

                if (patch.referenceBody != prev)
                {
                    if (patch.referenceBody == prev.referenceBody)
                        flightPlan.Add(new PlanEntry(patch.StartUT, patch.referenceBody.MapObject, EntryType.Escape));
                    else
                        flightPlan.Add(new PlanEntry(patch.StartUT, patch.referenceBody.MapObject, EntryType.Encounter, patch.PeA));

                    prev = patch.referenceBody;
                }

                // Assuming that burn in atmosphere is 60 seconds earlier than periapsis
                // For entries order, but not for correct prediction 
                if (patch.PeA <= patch.referenceBody.atmosphereDepth && patch.referenceBody.atmosphere)
                {
                    flightPlan.Add(new PlanEntry(patch.StartUT + patch.GetTimeToPeriapsis() - 60, patch.referenceBody.MapObject, EntryType.Burn));
                }

                if (patch.PeA <= 0)
                {
                    ooopsUT = patch.StartUT + patch.GetTimeToPeriapsis();
                    flightPlan.Add(new PlanEntry(ooopsUT, patch.referenceBody.MapObject, EntryType.Crater));
                    break;
                }
            }

            // Push maneuvers into flight plan
            if (maneuverCount > 0)
            {
                foreach (ManeuverNode man in vessel.patchedConicSolver.maneuverNodes)
                {
                    if (ooopsUT > 0 && man.UT > ooopsUT)
                        continue;

                    flightPlan.Add(new PlanEntry(man.UT, man.scaledSpaceTarget, EntryType.Maneuver, man.DeltaV.magnitude));
                }
            }

            // Sort by time
            flightPlan.Sort((s1, s2) => s1.UT.CompareTo(s2.UT));

            // Nothing to do? Eat snacks!!!
            if (flightPlan.Count == 0)
                flightPlan.Add(new PlanEntry(0, vessel.mainBody.MapObject, EntryType.SnacksOrbit));
        }

        private void RenderFlightPlan()
        {
            double totalDeltaV = 0;
            string text = "";
            Color color;
            if (uiCoreActive)
                color = UI.Palette.Col("gray");
            else
                color = Color.gray;
            foreach (PlanEntry entry in flightPlan)
            {
                switch (entry.type)
                {
                    case EntryType.Encounter:
                        text = " encounter " + entry.obj.celestialBody.RevealName() + " (" + Format.Number(entry.magnitude, "m") + ")";
                        if (uiCoreActive)
                            color = UI.Palette.Col("green");
                        else
                            color = Color.green;
                        break;
                    case EntryType.Escape:
                        text = " escape to " + entry.obj.celestialBody.RevealName();
                        color = Color.white;
                        break;
                    case EntryType.Maneuver:
                        text = " perform maneuver ";
                        text += Format.Number(entry.magnitude, "m/s");
                        totalDeltaV += entry.magnitude;
                        if (uiCoreActive)
                            color = UI.Palette.Col("yellow");
                        else
                            color = Color.yellow;
                        break;
                    case EntryType.Burn:
                        text = " burn in " + entry.obj.celestialBody.RevealName() + "'s atmosphere";
                        if (uiCoreActive)
                            color = UI.Palette.Col("red");
                        else
                            color = Color.red;
                        break;
                    case EntryType.Crater:
                        text = " make a new crater on " + entry.obj.celestialBody.RevealName();
                        if (uiCoreActive)
                            color = UI.Palette.Col("red");
                        else
                            color = Color.red;
                        break;
                    case EntryType.SnacksLanded:
                        text = " eat snacks landed at " + entry.obj.celestialBody.RevealName();
                        if (uiCoreActive)
                            color = UI.Palette.Col("yellow");
                        else
                            color = Color.yellow;
                        break;
                    case EntryType.DivingCourse:
                        text = " diving course in " + entry.obj.celestialBody.RevealName() + "'s ocean";
                        if (uiCoreActive)
                            color = UI.Palette.Col("yellow");
                        else
                            color = Color.yellow;
                        break;
                    case EntryType.SnacksOrbit:
                        text = " eat snacks orbiting " + entry.obj.celestialBody.RevealName();
                        if (uiCoreActive)
                            color = UI.Palette.Col("yellow");
                        else
                            color = Color.yellow;
                        break;
                }
                GUILayout.BeginHorizontal();

                if (entry.UT > 0)
                {
                    if (uiCoreActive)
                    {
                        UI.Layout.LabelRight(
                            (showAsUT ?
                                "At " + KSPUtil.dateTimeFormatter.PrintDateCompact(entry.UT, true, true) :
                                "In " + KSPUtil.dateTimeFormatter.PrintDateDeltaCompact(
                                    entry.UT - Planetarium.GetUniversalTime(),
                                    true,
                                    true,
                                    true
                                )
                            ),
                            color,
                            GUILayout.Width(150)
                        );
                    }
                    else
                    {
                        guiLabelStyle.normal.textColor = color;
                        GUILayout.FlexibleSpace();
                        GUILayout.Label((showAsUT ?
                                "At " + KSPUtil.dateTimeFormatter.PrintDateCompact(entry.UT, true, true) :
                                "In " + KSPUtil.dateTimeFormatter.PrintDateDeltaCompact(
                                    entry.UT - Planetarium.GetUniversalTime(),
                                    true,
                                    true,
                                    true
                                )
                            ),
                                guiLabelStyle,
                            GUILayout.Width(150));
                    }
                }
                guiButtonStyle.normal.textColor = color;
                if ((uiCoreActive && UI.Layout.ButtonLeft(text, color, GUILayout.MinWidth(230))) ||
                    (!uiCoreActive && GUILayout.Button(text, guiButtonStyle, GUILayout.MinWidth(230))))
                {
                    if (!MapView.MapIsEnabled)
                        MapView.EnterMapView();
                    if (entry.obj != null)
                        PlanetariumCamera.fetch.SetTarget(entry.obj);
                }

                if (KACWrapper.APIReady)
                {
                    KACButton(entry, color, text);
                }

                GUILayout.EndHorizontal();
            }
            if (uiCoreActive)
            {
                UI.Layout.HR(10);
                UI.Layout.LabelAndText("Total Δv", Format.Number(totalDeltaV, "m/s"), UI.Palette.Col("blue"), Color.white);
            }
            else
            {
                GUILayout.Label("", guiWhiteLabelStyle, GUILayout.Height(10));
                GUILayout.BeginHorizontal();
                GUILayout.Label("Total Δv" + ": ", guiBlueLabelStyle);
                GUILayout.Label(Format.Number(totalDeltaV, "m/s"), guiWhiteLabelStyle);
                GUILayout.EndHorizontal();
            }
        }

        private void KACButton(PlanEntry entry, Color color, string text)
        {
            KACWrapper.KACAPI.AlarmTypeEnum alarmType;
            switch (entry.type)
            {
                case EntryType.Encounter:
                    alarmType = KACWrapper.KACAPI.AlarmTypeEnum.SOIChange;
                    break;
                case EntryType.Escape:
                    alarmType = KACWrapper.KACAPI.AlarmTypeEnum.SOIChange;
                    break;
                case EntryType.Maneuver:
                    alarmType = KACWrapper.KACAPI.AlarmTypeEnum.Maneuver;
                    break;
                case EntryType.Burn:
                    alarmType = KACWrapper.KACAPI.AlarmTypeEnum.Closest;
                    break;
                case EntryType.Crater:
                    alarmType = KACWrapper.KACAPI.AlarmTypeEnum.Periapsis;
                    break;
                default:
                    alarmType = KACWrapper.KACAPI.AlarmTypeEnum.Raw;
                    break;
            }

            guiButtonStyle.normal.textColor = color;
            if ((uiCoreActive && UI.Layout.Button("A", color, GUILayout.Width(20))) ||
                (!uiCoreActive && GUILayout.Button("A", guiButtonStyle, GUILayout.Width(20))))
            {
                String tmpID = KACWrapper.KAC.CreateAlarm(
                    alarmType,
                    FlightGlobals.ActiveVessel.vesselName,
                    entry.UT - 600
                );

                KACWrapper.KACAPI.KACAlarm alarmNew = KACWrapper.KAC.Alarms.First(a => a.ID == tmpID);
                alarmNew.Notes = FlightGlobals.ActiveVessel.vesselName + "\n" +
                    "Is about to " + text;
                alarmNew.AlarmMargin = 600;
                alarmNew.AlarmAction = KACWrapper.KACAPI.AlarmActionEnum.KillWarp;
                alarmNew.VesselID = FlightGlobals.ActiveVessel.id.ToString();
                ScreenMessages.PostScreenMessage("KAC alarm added for " + FlightGlobals.ActiveVessel.vesselName);
            }
        }
    }
}

