using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ml_lme
{
    public static class LeapExtender
    {
        [DllImport("LeapExtender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool LeapInitialize();

        [DllImport("LeapExtender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool LeapTerminate();

        [DllImport("LeapExtender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool LeapGetHandsData(IntPtr f_fingersBends, IntPtr f_fingersSpreads, IntPtr f_handsDetection, IntPtr f_handsPositions, IntPtr f_handsRotations);

        [DllImport("LeapExtender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void LeapSetTrackingMode(int f_mode);
    }

    public class LeapMotionExtention : MelonLoader.MelonMod
    {
        const float c_defaultRootOffsetY = 0.5f;
        const float c_defaultRootOffsetZ = 0.25f;
        readonly Quaternion c_hmdRotationFix = new Quaternion(0f, 0.7071068f, 0.7071068f, 0f);

        static bool ms_enabled = false;
        bool m_sdk3 = false;
        bool m_vr = false;
        bool m_useHeadRoot = false;
        Vector3 m_rootOffset = new Vector3(0f, c_defaultRootOffsetY, c_defaultRootOffsetZ); // Default offset for avatar with height 1.0
        bool m_fingersOnly = false;

        static bool ms_inVrMode = false;
        bool m_leapInitialized = false;
        float[] m_fingersBends = null;
        GCHandle m_fingersBendsPtr;
        float[] m_fingersSpreads = null;
        GCHandle m_fingersSpreadsPtr;
        bool[] m_handsPresent = null;
        GCHandle m_handsPresentPtr;
        float[] m_handPositions = null;
        GCHandle m_handPositionsPtr;
        float[] m_handRotations = null;
        GCHandle m_handRotationsPtr;

        Vector3 m_leftTargetPosition;
        Quaternion m_leftTargetRotation;
        Vector3 m_rightTargetPosition;
        Quaternion m_rightTargetRotation;

        public override void OnApplicationStart()
        {
            DependenciesHandler.ExtractDependencies();
            // DependenciesHandler.LoadDependencies(); // DllImport above load them fine somehow

            MelonLoader.MelonPreferences.CreateCategory("LME", "Leap Motion extension");
            MelonLoader.MelonPreferences.CreateEntry("LME", "Enabled", false, "Enable Leap Motion extension");
            MelonLoader.MelonPreferences.CreateEntry("LME", "VR", false, "Enable HMD mode for Leap Motion");
            MelonLoader.MelonPreferences.CreateEntry("LME", "SDK3", false, "Send SDK3 parameters");
            MelonLoader.MelonPreferences.CreateEntry("LME", "HeadRoot", false, "Use head as root point");
            MelonLoader.MelonPreferences.CreateEntry("LME", "RootOffsetY", c_defaultRootOffsetY, "Avatar root point offset for Y axis");
            MelonLoader.MelonPreferences.CreateEntry("LME", "RootOffsetZ", c_defaultRootOffsetZ, "Avatar root point offset for Z axis");
            MelonLoader.MelonPreferences.CreateEntry("LME", "FingersOnly", false, "Fingers tracking only");

            ms_inVrMode = VRCTrackingManager.Method_Public_Static_Boolean_9();

            m_fingersBends = new float[10];
            m_fingersBendsPtr = GCHandle.Alloc(m_fingersBends, GCHandleType.Pinned);

            m_fingersSpreads = new float[10];
            m_fingersSpreadsPtr = GCHandle.Alloc(m_fingersSpreads, GCHandleType.Pinned);

            m_handsPresent = new bool[2];
            m_handsPresentPtr = GCHandle.Alloc(m_handsPresent, GCHandleType.Pinned);

            m_handPositions = new float[6];
            m_handPositionsPtr = GCHandle.Alloc(m_handPositions, GCHandleType.Pinned);

            m_handRotations = new float[8];
            m_handRotationsPtr = GCHandle.Alloc(m_handRotations, GCHandleType.Pinned);

            // Patches
            var l_patchMethod = new Harmony.HarmonyMethod(typeof(LeapMotionExtention), "VRCIM_ControllersType");
            typeof(VRCInputManager).GetMethods().Where(x =>
                    x.Name.StartsWith("Method_Public_Static_Boolean_EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique_")
                ).ToList().ForEach(m => Harmony.Patch(m, l_patchMethod));

            OnPreferencesSaved();
        }

        public override void OnApplicationQuit()
        {
            if(m_leapInitialized)
            {
                LeapExtender.LeapTerminate();
                m_leapInitialized = false;
            }

            m_fingersBendsPtr.Free();
            m_fingersBends = null;

            m_fingersSpreadsPtr.Free();
            m_fingersSpreads = null;

            m_handsPresentPtr.Free();
            m_handsPresent = null;

            m_handPositionsPtr.Free();
            m_handPositions = null;

            m_handRotationsPtr.Free();
            m_handRotations = null;
        }

        public override void OnPreferencesSaved()
        {
            ms_enabled = MelonLoader.MelonPreferences.GetEntryValue<bool>("LME", "Enabled");
            m_sdk3 = MelonLoader.MelonPreferences.GetEntryValue<bool>("LME", "SDK3");
            m_vr = MelonLoader.MelonPreferences.GetEntryValue<bool>("LME", "VR");
            m_useHeadRoot = MelonLoader.MelonPreferences.GetEntryValue<bool>("LME", "HeadRoot");
            m_rootOffset.y = MelonLoader.MelonPreferences.GetEntryValue<float>("LME", "RootOffsetY");
            m_rootOffset.z = MelonLoader.MelonPreferences.GetEntryValue<float>("LME", "RootOffsetZ");
            m_fingersOnly = MelonLoader.MelonPreferences.GetEntryValue<bool>("LME", "FingersOnly");

            UpdateExtensionStates();
        }

        public override void OnUpdate()
        {
            // Check for VR mode to prevent desktop input lock
            ms_inVrMode = VRCTrackingManager.Method_Public_Static_Boolean_9();

            if(ms_enabled)
            {
                // Use Leap Motion data
                if(m_leapInitialized)
                    LeapExtender.LeapGetHandsData(m_fingersBendsPtr.AddrOfPinnedObject(), m_fingersSpreadsPtr.AddrOfPinnedObject(), m_handsPresentPtr.AddrOfPinnedObject(), m_handPositionsPtr.AddrOfPinnedObject(), m_handRotationsPtr.AddrOfPinnedObject());

                if(m_sdk3)
                {
                    // Set SDK3 parameters directly, user has to make own avatar with specific parameters
                    var l_expParams = VRCPlayer.field_Internal_Static_VRCPlayer_0?.prop_VRCAvatarManager_0?.prop_VRCAvatarDescriptor_0?.expressionParameters?.parameters;
                    var l_playableController = VRCPlayer.field_Internal_Static_VRCPlayer_0?.field_Private_AnimatorControllerManager_0?.field_Private_AvatarAnimParamController_0?.field_Private_AvatarPlayableController_0;
                    if((l_expParams != null) && (l_playableController != null))
                    {
                        for(int i = 0; i < l_expParams.Length; i++)
                        {
                            var l_expParam = l_expParams[i];
                            if(l_expParam.name.StartsWith("_FingerValue") && (l_expParam.valueType == VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Float))
                            {
                                int l_bufferIndex = -1;
                                if(Int32.TryParse(l_expParam.name.Substring(12), out l_bufferIndex))
                                {
                                    if((l_bufferIndex >= 0) && (l_bufferIndex <= 9))
                                    {
                                        l_playableController.Method_Public_Boolean_Int32_Single_0(i, m_fingersBends[l_bufferIndex]);
                                    }
                                }
                                continue;
                            }
                            if(l_expParam.name.StartsWith("_HandPresent") && (l_expParam.valueType == VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Bool))
                            {
                                int l_bufferIndex = -1;
                                if(Int32.TryParse(l_expParam.name.Substring(12), out l_bufferIndex))
                                {
                                    if((l_bufferIndex >= 0) && (l_bufferIndex <= 1))
                                    {
                                        l_playableController.Method_Public_Boolean_Int32_Single_0(i, m_handsPresent[l_bufferIndex] ? 1.0f : 0.0f); // Fallback, there is separated method for boolean parameters somewhere
                                    }
                                }
                                continue;
                            }
                        }
                    }
                }

                if(!m_fingersOnly)
                {
                    var l_solver = VRCPlayer.field_Internal_Static_VRCPlayer_0?.field_Private_VRC_AnimationController_0?.field_Private_VRIK_0?.solver;
                    if(l_solver != null)
                    {
                        if(m_handsPresent[0])
                        {
                            if(l_solver.leftArm?.target != null)
                            {
                                Vector3 l_newPos = new Vector3(m_handPositions[0], m_handPositions[1], -m_handPositions[2]) * 0.001f;
                                Quaternion l_newRot = new Quaternion(-m_handRotations[0], -m_handRotations[1], m_handRotations[2], m_handRotations[3]);
                                ApplyAdjustment(ref l_newPos, ref l_newRot);

                                Transform l_rootTransform = GetRootTransform(ref l_solver);
                                m_leftTargetPosition = l_rootTransform.position + l_rootTransform.rotation * l_newPos;
                                m_leftTargetRotation = l_rootTransform.rotation * l_newRot;

                                var l_pickupJoint = VRCPlayer.field_Internal_Static_VRCPlayer_0?.field_Private_VRC_AnimationController_0?.field_Private_IkController_0?.field_Private_VRCHandGrasper_0?.field_Private_GameObject_0;
                                if(l_pickupJoint != null)
                                {
                                    l_pickupJoint.transform.position = m_leftTargetPosition;
                                    l_pickupJoint.transform.rotation = m_leftTargetRotation;
                                }
                            }
                        }

                        if(m_handsPresent[1])
                        {
                            if(l_solver.rightArm?.target != null)
                            {
                                Vector3 l_newPos = new Vector3(m_handPositions[3], m_handPositions[4], -m_handPositions[5]) * 0.001f;
                                Quaternion l_newRot = new Quaternion(-m_handRotations[4], -m_handRotations[5], m_handRotations[6], m_handRotations[7]);
                                ApplyAdjustment(ref l_newPos, ref l_newRot);

                                Transform l_rootTransform = GetRootTransform(ref l_solver);
                                m_rightTargetPosition = l_rootTransform.position + l_rootTransform.rotation * l_newPos;
                                m_rightTargetRotation = l_rootTransform.rotation * l_newRot;

                                var l_pickupJoint = VRCPlayer.field_Internal_Static_VRCPlayer_0?.field_Private_VRC_AnimationController_0?.field_Private_IkController_0?.field_Private_VRCHandGrasper_1?.field_Private_GameObject_0;
                                if(l_pickupJoint != null)
                                {
                                    l_pickupJoint.transform.position = m_rightTargetPosition;
                                    l_pickupJoint.transform.rotation = m_rightTargetRotation;
                                }
                            }
                        }
                    }
                }

                var l_handController = VRCPlayer.field_Internal_Static_VRCPlayer_0?.field_Private_VRC_AnimationController_0?.field_Private_HandGestureController_0;
                if(l_handController != null)
                {
                    l_handController.field_Internal_Boolean_0 = true;
                    l_handController.field_Private_EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique_0 = VRCInputManager.EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique.Index;

                    for(int i = 0; i < 2; i++)
                    {
                        if(m_handsPresent[i])
                        {
                            for(int j = 0; j < 5; j++)
                            {
                                int l_dataIndex = i * 5 + j;
                                l_handController.field_Private_ArrayOf_VRCInput_0[l_dataIndex].field_Public_Single_0 = 1.0f - m_fingersBends[l_dataIndex]; // Squeeze
                                l_handController.field_Private_ArrayOf_VRCInput_1[l_dataIndex].field_Public_Single_0 = m_fingersSpreads[l_dataIndex]; // Spread
                            }
                        }
                    }
                }
            }
        }

        public override void OnLateUpdate()
        {
            if(ms_enabled)
            {
                if(!m_fingersOnly)
                {
                    var l_solver = VRCPlayer.field_Internal_Static_VRCPlayer_0?.field_Private_VRC_AnimationController_0?.field_Private_VRIK_0?.solver;
                    if(l_solver != null)
                    {
                        if(m_handsPresent[0])
                        {
                            if(l_solver.leftArm?.target != null)
                            {
                                l_solver.leftArm.positionWeight = 1f;
                                l_solver.leftArm.rotationWeight = 1f;
                                l_solver.leftArm.target.position = m_leftTargetPosition;
                                l_solver.leftArm.target.rotation = m_leftTargetRotation;
                            }
                        }

                        if(m_handsPresent[1])
                        {
                            if(l_solver.rightArm?.target != null)
                            {
                                l_solver.rightArm.positionWeight = 1f;
                                l_solver.rightArm.rotationWeight = 1f;
                                l_solver.rightArm.target.position = m_rightTargetPosition;
                                l_solver.rightArm.target.rotation = m_rightTargetRotation;
                            }
                        }
                    }
                }

                var l_handController = VRCPlayer.field_Internal_Static_VRCPlayer_0?.field_Private_VRC_AnimationController_0?.field_Private_HandGestureController_0;
                if(l_handController != null)
                {
                    l_handController.field_Internal_Boolean_0 = true;
                    l_handController.field_Private_EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique_0 = VRCInputManager.EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique.Index;
                    for(int i = 0; i < 2; i++)
                    {
                        if(m_handsPresent[i])
                        {
                            for(int j = 0; j < 5; j++)
                            {
                                int l_dataIndex = i * 5 + j;
                                l_handController.field_Private_ArrayOf_Single_1[l_dataIndex] = 1.0f - m_fingersBends[l_dataIndex]; // Squeeze
                                l_handController.field_Private_ArrayOf_Single_3[l_dataIndex] = m_fingersSpreads[l_dataIndex]; // Spread
                            }
                        }
                    }
                }
            }
        }

        void UpdateExtensionStates()
        {
            if(ms_enabled)
            {
                if(!m_leapInitialized)
                    m_leapInitialized = LeapExtender.LeapInitialize();
                LeapExtender.LeapSetTrackingMode(m_vr ? 1 : 0);
            }
            else
            {
                if(m_leapInitialized)
                {
                    LeapExtender.LeapTerminate();
                    m_leapInitialized = false;
                }

                // Reset HandGestureController, IKSolverVR resets itself
                var l_controller = VRCPlayer.field_Internal_Static_VRCPlayer_0?.field_Private_VRC_AnimationController_0?.field_Private_HandGestureController_0;
                if(l_controller != null)
                {
                    l_controller.field_Internal_Boolean_0 = false;
                    l_controller.field_Private_EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique_0 = VRCInputManager.EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique.Mouse;
                }
            }
        }

        void ApplyAdjustment(ref Vector3 pos, ref Quaternion rot)
        {
            if(m_vr)
            {
                pos.x *= -1f;
                Swap(ref pos.y, ref pos.z);
                rot = c_hmdRotationFix * rot;
            }

            // Easy way to scale, but can be improved (but how?)
            var l_height = VRCTrackingManager.Method_Public_Static_Single_5();
            pos += m_rootOffset;
            if(!m_useHeadRoot)
            {
                if(m_vr)
                {
                    pos.y += m_rootOffset.y;
                    pos.z -= m_rootOffset.z;
                }
                pos *= l_height;
                pos.y -= (l_height - (ms_inVrMode ? VRCTrackingManager.Method_Public_Static_Single_PDM_0() : VRCTrackingManager.Method_Public_Static_Single_4()));
            }
            else
            {
                pos.y -= m_rootOffset.y * (m_vr ? 1f : 2f);
                if(m_vr)
                    pos.z -= m_rootOffset.z * 2f;
                pos *= l_height;
            }
        }

        Transform GetRootTransform(ref RootMotion.FinalIK.IKSolverVR solver)
        {
            Transform l_result = null;
            if(!m_useHeadRoot)
            {
                l_result = VRCPlayer.field_Internal_Static_VRCPlayer_0.transform;
            }
            else
            {
                l_result = solver.spine?.headTarget?.transform?.parent;
                if(l_result == null)
                    l_result = VRCPlayer.field_Internal_Static_VRCPlayer_0.transform;
            }
            return l_result;
        }

        static bool VRCIM_ControllersType(ref bool __result, VRCInputManager.EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique __0)
        {
            if(ms_enabled && ms_inVrMode)
            {
                if(__0 == VRCInputManager.EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique.Index)
                {
                    __result = true;
                    return false;
                }
                else
                {
                    __result = false;
                    return false;
                }
            }
            else
                return true;
        }

        public static void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp = lhs;
            lhs = rhs;
            rhs = temp;
        }
    }
}
