using System;
using System.Runtime.InteropServices;

namespace ml_hfo
{
    public static class LeapExtender
    {
        [DllImport("LeapExtender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool LeapInitialize();

        [DllImport("LeapExtender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool LeapTerminate();

        [DllImport("LeapExtender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool LeapGetHandsData(IntPtr f_fingers, IntPtr f_handsPresent);

        [DllImport("LeapExtender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void LeapSetTrackingMode(int f_mode);
    }

    public class HandFingersOverrider : MelonLoader.MelonMod
    {
        static bool ms_enabled = false;
        static bool ms_enabledSDK3 = false;
        bool m_enabledLeap = false;
        bool m_enabledLeapVR = false;

        bool m_leapInitialized = false;
        static float[] ms_fingersData = null;
        GCHandle m_fingersDataPtr;
        static bool[] ms_handsPresent = null;
        GCHandle m_handsPresentPtr;

        public override void OnApplicationStart()
        {
            MelonLoader.MelonPreferences.CreateCategory("HFO", "Fingers Override");
            MelonLoader.MelonPreferences.CreateEntry("HFO", "Override", false, "Fingers override");
            MelonLoader.MelonPreferences.CreateEntry("HFO", "OverrideSDK3", false, "Send SDK3 parameters");
            MelonLoader.MelonPreferences.CreateEntry("HFO", "OverrideLM", false, "Use Leap Motion tracking");
            MelonLoader.MelonPreferences.CreateEntry("HFO", "OverrideLMVR", false, "Set HMD mode for Leap Motion");

            ms_fingersData = new float[10];
            m_fingersDataPtr = GCHandle.Alloc(ms_fingersData, GCHandleType.Pinned);

            ms_handsPresent = new bool[2];
            m_handsPresentPtr = GCHandle.Alloc(ms_handsPresent, GCHandleType.Pinned);

            // Hook HandGestureController.Update
            var l_originalUpdate = typeof(HandGestureController).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            var l_hookUpdatePostfix = typeof(HandFingersOverrider).GetMethod("UpdateHook_Postfix", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            if ((l_originalUpdate != null) && (l_hookUpdatePostfix != null))
            {
                Harmony.Patch(l_originalUpdate, null, new Harmony.HarmonyMethod(l_hookUpdatePostfix));
            }
            else MelonLoader.MelonLogger.Warning("HandGestureController.Update hook error! Result is [" + (l_originalUpdate != null) + "," + (l_hookUpdatePostfix != null) + "]");

            OnPreferencesSaved();
        }

        public override void OnApplicationQuit()
        {
            m_fingersDataPtr.Free();
            ms_fingersData = null;

            m_handsPresentPtr.Free();
            ms_handsPresent = null;

            if (m_leapInitialized)
            {
                LeapExtender.LeapTerminate();
                m_leapInitialized = false;
            }
        }

        public override void OnPreferencesSaved()
        {
            ms_enabled = MelonLoader.MelonPreferences.GetEntryValue<bool>("HFO", "Override");
            ms_enabledSDK3 = MelonLoader.MelonPreferences.GetEntryValue<bool>("HFO", "OverrideSDK3");
            m_enabledLeap = MelonLoader.MelonPreferences.GetEntryValue<bool>("HFO", "OverrideLM");
            m_enabledLeapVR = MelonLoader.MelonPreferences.GetEntryValue<bool>("HFO", "OverrideLMVR");

            ToggleOverriding();
        }

        public override void OnUpdate()
        {
            if (ms_enabled)
            {
                if (m_enabledLeap)
                {
                    // Use Leap Motion data
                    if (m_leapInitialized) LeapExtender.LeapGetHandsData(m_fingersDataPtr.AddrOfPinnedObject(), m_handsPresentPtr.AddrOfPinnedObject());
                }
                else
                {
                    for (int i = 0; i < 2; i++) ms_handsPresent[i] = true;

                    // Use keyboard data
                    if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl))
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            if (UnityEngine.Input.GetKeyDown((UnityEngine.KeyCode)(UnityEngine.KeyCode.F1 + i))) ms_fingersData[i] = ((ms_fingersData[i] >= 0.95f) ? 0.0f : 1.0f);
                        }
                    }
                    if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl))
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            if (UnityEngine.Input.GetKeyDown((UnityEngine.KeyCode)(UnityEngine.KeyCode.F1 + i))) ms_fingersData[5 + i] = ((ms_fingersData[5 + i] >= 0.95f) ? 0.0f : 1.0f);
                        }
                    }
                }

                if (ms_enabledSDK3)
                {
                    // Set SDK3 parameters directly, user has to make own avatar with specific parameters
                    var l_expParams = VRCPlayer.field_Internal_Static_VRCPlayer_0?.prop_VRCAvatarManager_0?.prop_VRCAvatarDescriptor_0?.expressionParameters?.parameters;
                    var l_playableController = VRCPlayer.field_Internal_Static_VRCPlayer_0?.field_Private_AnimatorControllerManager_0?.field_Private_AvatarAnimParamController_0?.field_Private_AvatarPlayableController_0;
                    if ((l_expParams != null) && (l_playableController != null))
                    {
                        for (int i = 0; i < l_expParams.Length; i++)
                        {
                            var l_expParam = l_expParams[i];
                            if (l_expParam.name.StartsWith("_FingerValue") && (l_expParam.valueType == VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Float))
                            {
                                int l_bufferIndex = -1;
                                if (Int32.TryParse(l_expParam.name.Substring(12), out l_bufferIndex))
                                {
                                    if ((l_bufferIndex >= 0) && (l_bufferIndex <= 9))
                                    {
                                        l_playableController.Method_Public_Boolean_Int32_Single_1(i, ms_fingersData[l_bufferIndex]); // Why the fuck float for all types???
                                    }
                                }
                                continue;
                            }
                            if (l_expParam.name.StartsWith("_HandPresent") && (l_expParam.valueType == VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Bool))
                            {
                                int l_bufferIndex = -1;
                                if (Int32.TryParse(l_expParam.name.Substring(12), out l_bufferIndex))
                                {
                                    if ((l_bufferIndex >= 0) && (l_bufferIndex <= 1))
                                    {
                                        l_playableController.Method_Public_Boolean_Int32_Single_1(i, ms_handsPresent[l_bufferIndex] ? 1.0f : 0.0f); // Why the fuck float for all types???
                                    }
                                }
                                continue;
                            }
                        }
                    }
                }
            }
        }

        public static void UpdateHook_Postfix(ref HandGestureController __instance)
        {
            if (ms_enabled && !ms_enabledSDK3)
            {
                if ((__instance != null) && (__instance == VRCPlayer.field_Internal_Static_VRCPlayer_0?.field_Private_VRC_AnimationController_0?.field_Private_HandGestureController_0))
                {
                    __instance.field_Internal_Boolean_0 = true;
                    __instance.field_Private_EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique_0 = VRCInputManager.EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique.Index;
                    for (int i = 0; i < 10; i++)
                    {
                        __instance.field_Private_ArrayOf_Single_1[i] = 1.0f - ms_fingersData[i]; // Squeeze
                        //l_handGestureController.field_Private_ArrayOf_Single_3[i] = 1.0f - m_fingersData[i]; // Spread
                    }
                }
            }
        }

        public void ToggleOverriding()
        {
            if (ms_enabled)
            {
                if (m_enabledLeap)
                {
                    if (!m_leapInitialized) m_leapInitialized = LeapExtender.LeapInitialize();
                    LeapExtender.LeapSetTrackingMode(m_enabledLeapVR ? 1 : 0);
                }
                else
                {
                    if (m_leapInitialized)
                    {
                        LeapExtender.LeapTerminate();
                        m_leapInitialized = false;
                    }
                }
            }
            else
            {
                if (m_leapInitialized)
                {
                    LeapExtender.LeapTerminate();
                    m_leapInitialized = false;
                }

                HandGestureController l_controller = VRCPlayer.field_Internal_Static_VRCPlayer_0?.field_Private_VRC_AnimationController_0?.field_Private_HandGestureController_0;
                if (l_controller != null)
                {
                    l_controller.field_Internal_Boolean_0 = false;
                    l_controller.field_Private_EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique_0 = VRCInputManager.EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique.Mouse;
                }
            }
        }
    }
}
