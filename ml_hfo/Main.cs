using System;
using System.Runtime.InteropServices;

namespace ml_hfo
{
    public static class LeapExtender
    {
        [DllImport("LeapExtender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern Boolean LeapInitialize();

        [DllImport("LeapExtender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern Boolean LeapTerminate();

        [DllImport("LeapExtender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern Boolean LeapGetHandsData(IntPtr f_fingers, IntPtr f_handsPresent);

        [DllImport("LeapExtender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void LeapSetTrackingMode(int f_mode);
    }

    public class HandFingersOverrider : MelonLoader.MelonMod
    {
        bool m_enabled = false;
        bool m_enabledLeap = false;
        bool m_enabledVR = false;

        bool m_leapInitialized = false;
        float[] m_fingersData = null;
        GCHandle m_fingersDataPtr;
        bool[] m_handsPresent = null;
        GCHandle m_handsPresentPtr;

        public override void OnApplicationStart()
        {
            MelonLoader.MelonPreferences.CreateCategory("HFO", "Fingers Override");
            MelonLoader.MelonPreferences.CreateEntry("HFO", "Override", false, "Fingers override");
            MelonLoader.MelonPreferences.CreateEntry("HFO", "OverrideLM", false, "Override with Leap Motion");
            MelonLoader.MelonPreferences.CreateEntry("HFO", "OverrideVR", false, "VR-SDK3 mode");

            m_fingersData = new float[10];
            m_fingersDataPtr = GCHandle.Alloc(m_fingersData, GCHandleType.Pinned);

            m_handsPresent = new bool[2];
            m_handsPresentPtr = GCHandle.Alloc(m_handsPresent, GCHandleType.Pinned);

            OnPreferencesSaved();
        }

        public override void OnApplicationQuit()
        {
            m_fingersDataPtr.Free();
            m_fingersData = null;

            m_handsPresentPtr.Free();
            m_handsPresent = null;

            if (m_leapInitialized)
            {
                LeapExtender.LeapTerminate();
                m_leapInitialized = false;
            }
        }

        public override void OnPreferencesSaved()
        {
            m_enabled = MelonLoader.MelonPreferences.GetEntryValue<bool>("HFO", "Override");
            m_enabledLeap = MelonLoader.MelonPreferences.GetEntryValue<bool>("HFO", "OverrideLM");
            m_enabledVR = MelonLoader.MelonPreferences.GetEntryValue<bool>("HFO", "OverrideVR");

            ToggleOverriding();
        }

        public override void OnUpdate()
        {
            if (m_enabled)
            {
                if (m_enabledLeap)
                {
                    // Use Leap Motion data
                    if (m_leapInitialized) LeapExtender.LeapGetHandsData(m_fingersDataPtr.AddrOfPinnedObject(), m_handsPresentPtr.AddrOfPinnedObject());
                }
                else
                {
                    // Use keyboard data
                    if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl))
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            if (UnityEngine.Input.GetKeyDown((UnityEngine.KeyCode)(UnityEngine.KeyCode.F1 + i))) m_fingersData[i] = ((m_fingersData[i] >= 0.95f) ? 0.0f : 1.0f);
                        }
                    }
                    if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl))
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            if (UnityEngine.Input.GetKeyDown((UnityEngine.KeyCode)(UnityEngine.KeyCode.F1 + i))) m_fingersData[5 + i] = ((m_fingersData[5 + i] >= 0.95f) ? 0.0f : 1.0f);
                        }
                    }
                }

                if (m_enabledVR)
                {
                    // Based on old code of HandGestureController, it's highly tied to VRCInputManager and VRCInputProcessor
                    // and updates own internal values in Update() regardless of what you set, so different approach is made.
                    // Set SDK3 parameters directly, user has to make own avatar with specific parameters:
                    // _FingerValue# float, where # is number from 0 to 9
                    // _HandPresent# bool, where # is number 0 for left hand and 1 for right hand
                    // Total - 82 bytes, at least it works
                    // No parameters caching for now, just cycle lookup and set

                    var l_expParams = VRCPlayer.field_Internal_Static_VRCPlayer_0?.prop_VRCAvatarManager_0?.prop_VRCAvatarDescriptor_0?.expressionParameters?.parameters;
                    var l_playableController = VRCPlayer.field_Internal_Static_VRCPlayer_0?.field_Private_AnimatorControllerManager_0?.field_Private_AvatarAnimParamController_0?.field_Private_AvatarPlayableController_0;
                    if ((l_playableController != null) && (l_playableController != null))
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
                                        l_playableController.Method_Public_Boolean_Int32_Single_3(i, m_fingersData[l_bufferIndex]); // Why the fuck float for all types???
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
                                        l_playableController.Method_Public_Boolean_Int32_Single_3(i, m_handsPresent[l_bufferIndex] ? 1.0f : 0.0f); // Why the fuck float for all types???
                                    }
                                }
                                continue;
                            }
                        }
                    }
                }
                else
                {
                    // Desktop override is easy, somehow
                    HandGestureController l_handGestureController = VRCPlayer.field_Internal_Static_VRCPlayer_0?.field_Private_VRC_AnimationController_0?.field_Private_HandGestureController_0;
                    if (l_handGestureController)
                    {
                        l_handGestureController.field_Private_EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique_0 = VRCInputManager.EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique.Index;

                        for (int i = 0; i < 10; i++)
                        {
                            l_handGestureController.field_Private_ArrayOf_Single_1[i] = 1.0f - m_fingersData[i]; // Squeeze
                            //l_handGestureController.field_Private_ArrayOf_Single_3[i] = 1.0f - m_fingersData[i]; // Spread
                        }
                    }
                }
            }
        }

        public void ToggleOverriding()
        {
            if (m_enabled && m_enabledLeap)
            {
                if (!m_leapInitialized) m_leapInitialized = LeapExtender.LeapInitialize();
                LeapExtender.LeapSetTrackingMode(m_enabledVR ? 1 : 0);
            }
            else
            {
                if (m_leapInitialized)
                {
                    LeapExtender.LeapTerminate();
                    m_leapInitialized = false;
                }

                // Restore gesture controller
                HandGestureController l_handGestureController = VRCPlayer.field_Internal_Static_VRCPlayer_0?.field_Private_VRC_AnimationController_0?.field_Private_HandGestureController_0;
                if (l_handGestureController)
                {
                    l_handGestureController.field_Private_EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique_0 = VRCInputManager.EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique.Mouse;
                }
            }
        }
    }
}
