using System;
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
        public static extern bool LeapGetHandsData(IntPtr f_fingers, IntPtr f_handsPresent, IntPtr f_positions, IntPtr f_rotations);

        [DllImport("LeapExtender.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void LeapSetTrackingMode(int f_mode);
    }

    public class LeapMotionExtention : MelonLoader.MelonMod
    {
        const float c_defaultRootOffsetY = 0.5f;
        const float c_defaultRootOffsetZ = 0.25f;
        readonly Quaternion c_hmdRotationFix = new Quaternion(0f, 0.7071068f, 0.7071068f, 0f);

        bool m_enabled = false;
        bool m_sdk3 = false;
        bool m_vr = false;
        int m_rootPoint = 0; // 0 - player, 1 - head
        Vector3 m_rootOffset = new Vector3(0f, c_defaultRootOffsetY, c_defaultRootOffsetZ); // Default offset for avatar with height 1.0
        bool m_fingersOnly = false;

        bool m_leapInitialized = false;
        float[] m_fingersData = null;
        GCHandle m_fingersDataPtr;
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
            MelonLoader.MelonPreferences.CreateCategory("LME", "Leap Motion extension");
            MelonLoader.MelonPreferences.CreateEntry("LME", "Enabled", false, "Enable Leap Motion extension");
            MelonLoader.MelonPreferences.CreateEntry("LME", "VR", false, "Enable HMD mode for Leap Motion");
            MelonLoader.MelonPreferences.CreateEntry("LME", "SDK3", false, "Send SDK3 parameters");
            MelonLoader.MelonPreferences.CreateEntry("LME", "RootPoint", 0, "Root point (0 - player, 1 - head)");
            MelonLoader.MelonPreferences.CreateEntry("LME", "RootOffsetY", c_defaultRootOffsetY, "Avatar root point offset for Y axis");
            MelonLoader.MelonPreferences.CreateEntry("LME", "RootOffsetZ", c_defaultRootOffsetZ, "Avatar root point offset for Z axis");
            MelonLoader.MelonPreferences.CreateEntry("LME", "FingersOnly", false, "Fingers tracking only");

            m_fingersData = new float[10];
            m_fingersDataPtr = GCHandle.Alloc(m_fingersData, GCHandleType.Pinned);

            m_handsPresent = new bool[2];
            m_handsPresentPtr = GCHandle.Alloc(m_handsPresent, GCHandleType.Pinned);

            m_handPositions = new float[6];
            m_handPositionsPtr = GCHandle.Alloc(m_handPositions, GCHandleType.Pinned);

            m_handRotations = new float[8];
            m_handRotationsPtr = GCHandle.Alloc(m_handRotations, GCHandleType.Pinned);

            OnPreferencesSaved();
        }

        public override void OnApplicationQuit()
        {
            m_fingersDataPtr.Free();
            m_fingersData = null;

            m_handsPresentPtr.Free();
            m_handsPresent = null;

            m_handPositionsPtr.Free();
            m_handPositions = null;

            m_handRotationsPtr.Free();
            m_handRotations = null;

            if(m_leapInitialized)
            {
                LeapExtender.LeapTerminate();
                m_leapInitialized = false;
            }
        }

        public override void OnPreferencesSaved()
        {
            m_enabled = MelonLoader.MelonPreferences.GetEntryValue<bool>("LME", "Enabled");
            m_sdk3 = MelonLoader.MelonPreferences.GetEntryValue<bool>("LME", "SDK3");
            m_vr = MelonLoader.MelonPreferences.GetEntryValue<bool>("LME", "VR");
            m_rootPoint = Mathf.Clamp(MelonLoader.MelonPreferences.GetEntryValue<int>("LME", "RootPoint"), 0, 1);
            m_rootOffset.y = MelonLoader.MelonPreferences.GetEntryValue<float>("LME", "RootOffsetY");
            m_rootOffset.z = MelonLoader.MelonPreferences.GetEntryValue<float>("LME", "RootOffsetZ");
            m_fingersOnly = MelonLoader.MelonPreferences.GetEntryValue<bool>("LME", "FingersOnly");

            UpdateExtensionStates();
        }

        public override void OnUpdate()
        {
            if(m_enabled)
            {
                // Use Leap Motion data
                if(m_leapInitialized)
                    LeapExtender.LeapGetHandsData(m_fingersDataPtr.AddrOfPinnedObject(), m_handsPresentPtr.AddrOfPinnedObject(), m_handPositionsPtr.AddrOfPinnedObject(), m_handRotationsPtr.AddrOfPinnedObject());

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
                                        l_playableController.Method_Public_Boolean_Int32_Single_1(i, m_fingersData[l_bufferIndex]); // Why the fuck float for all types???
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
                                        l_playableController.Method_Public_Boolean_Int32_Single_1(i, m_handsPresent[l_bufferIndex] ? 1.0f : 0.0f); // Why the fuck float for all types???
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
                                l_handController.field_Private_ArrayOf_VRCInput_0[i * 5 + j].field_Public_Single_0 = 1.0f - m_fingersData[i * 5 + j]; // Squeeze
                                //l_handGestureController.field_Private_ArrayOf_VRCInput_1[i].field_Public_Single_0 = 1.0f - m_fingersData[i]; // Spread
                            }
                        }
                    }
                }
            }
        }

        public override void OnLateUpdate()
        {
            if(m_enabled)
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
                                l_handController.field_Private_ArrayOf_Single_1[i * 5 + j] = 1.0f - m_fingersData[i * 5 + j]; // Squeeze
                                //l_handGestureController.field_Private_ArrayOf_Single_3[i] = 1.0f - m_fingersData[i]; // Spread
                            }
                        }
                    }
                }
            }
        }

        void UpdateExtensionStates()
        {
            if(m_enabled)
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

            // Easy way to scale, but can be improved (?)
            var l_height = VRCTrackingManager.Method_Public_Static_Single_7();
            pos += m_rootOffset;
            if(m_rootPoint == 0)
            {
                if(m_vr)
                {
                    pos.y += m_rootOffset.y;
                    pos.z -= m_rootOffset.z;
                }
                pos *= l_height;
                pos.y -= (l_height - VRCTrackingManager.Method_Public_Static_Single_5());
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
            switch(m_rootPoint)
            {
                case 0:
                    l_result = VRCPlayer.field_Internal_Static_VRCPlayer_0.transform;
                    break;
                case 1:
                {
                    l_result = solver.spine?.headTarget?.transform?.parent;
                    if(l_result == null)
                        l_result = VRCPlayer.field_Internal_Static_VRCPlayer_0.transform;
                }
                break;
            }
            return l_result;
        }

        public static void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp = lhs;
            lhs = rhs;
            rhs = temp;
        }
    }
}
