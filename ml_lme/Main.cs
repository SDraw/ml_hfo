using System;
using System.Linq;
using UnityEngine;

namespace ml_lme
{
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
        bool m_leapActive = false;
        Leap.Controller m_leapController = null;
        GestureMatcher.GesturesData m_gesturesData = null;

        Vector3 m_leftTargetPosition;
        Quaternion m_leftTargetRotation;
        Vector3 m_rightTargetPosition;
        Quaternion m_rightTargetRotation;

        System.Reflection.MethodInfo m_heightMethod = null;
        System.Reflection.MethodInfo m_eyeHeightMethod = null;
        System.Reflection.MethodInfo m_vrEyeHeightMethod = null;
        System.Reflection.MethodInfo m_vrCheckMethod = null;
        System.Reflection.MethodInfo m_parametersMethod = null;

        public override void OnApplicationStart()
        {
            DependenciesHandler.ExtractDependencies();

            MelonLoader.MelonPreferences.CreateCategory("LME", "Leap Motion extension");
            MelonLoader.MelonPreferences.CreateEntry("LME", "Enabled", false, "Enable Leap Motion extension");
            MelonLoader.MelonPreferences.CreateEntry("LME", "VR", false, "Enable HMD mode for Leap Motion");
            MelonLoader.MelonPreferences.CreateEntry("LME", "SDK3", false, "Send SDK3 parameters");
            MelonLoader.MelonPreferences.CreateEntry("LME", "HeadRoot", false, "Use head as root point");
            MelonLoader.MelonPreferences.CreateEntry("LME", "RootOffsetY", c_defaultRootOffsetY, "Avatar root point offset for Y axis");
            MelonLoader.MelonPreferences.CreateEntry("LME", "RootOffsetZ", c_defaultRootOffsetZ, "Avatar root point offset for Z axis");
            MelonLoader.MelonPreferences.CreateEntry("LME", "FingersOnly", false, "Fingers tracking only");

            m_leapController = new Leap.Controller();
            m_gesturesData = new GestureMatcher.GesturesData();

            // Methods search
            m_heightMethod = MethodsResolver.GetHeightMethod();
            if(m_heightMethod != null)
                MelonLoader.MelonDebug.Msg("VRCTrackingManager." + m_heightMethod.Name + " -> VRCTrackingManager.GetPlayerHeight");
            else
            {
                MelonLoader.MelonLogger.Warning("Can't resolve height method, fallback to zero float");
                m_heightMethod = typeof(LeapMotionExtention).GetMethod(nameof(ZeroFloat));
            }

            m_eyeHeightMethod = MethodsResolver.GetEyeHeightMethod();
            if(m_eyeHeightMethod != null)
                MelonLoader.MelonDebug.Msg("VRCTrackingManager." + m_eyeHeightMethod.Name + " -> VRCTrackingManager.GetPlayerEyeHeight (Desktop)");
            else
            {
                MelonLoader.MelonLogger.Warning("Can't resolve eye height method for desktop, fallback to zero float");
                m_eyeHeightMethod = typeof(LeapMotionExtention).GetMethod(nameof(ZeroFloat));
            }

            m_vrEyeHeightMethod = MethodsResolver.GetVREyeHeightMethod();
            if(m_vrEyeHeightMethod != null)
                MelonLoader.MelonDebug.Msg("VRCTrackingManager." + m_vrEyeHeightMethod.Name + " -> VRCTrackingManager.GetPlayerEyeHeight (VR)");
            else
            {
                MelonLoader.MelonLogger.Warning("Can't resolve eye height method for VR, fallback to zero float");
                m_vrEyeHeightMethod = typeof(LeapMotionExtention).GetMethod(nameof(ZeroFloat));
            }

            m_vrCheckMethod = MethodsResolver.GetVRCheckMethod();
            if(m_vrCheckMethod != null)
                MelonLoader.MelonDebug.Msg("VRCTrackingManager." + m_vrCheckMethod.Name + " -> VRCTrackingManager.IsInVRMode");
            else
            {
                MelonLoader.MelonLogger.Warning("Can't resolve vr check method, fallback to false boolean");
                m_vrCheckMethod = typeof(LeapMotionExtention).GetMethod(nameof(FalseBoolean));
            }

            m_parametersMethod = MethodsResolver.GetSDK3ParameterSetMethod();
            if(m_parametersMethod != null)
                MelonLoader.MelonDebug.Msg("AvatarPlayableController." + m_parametersMethod.Name + " -> AvatarPlayableController.SetAvatar<>Param");
            else
                MelonLoader.MelonLogger.Warning("Can't resolve avatar parameters set method, SDK3 avatar parameters set feature won't work");

            // Patches
            var l_patchMethod = new Harmony.HarmonyMethod(typeof(LeapMotionExtention), nameof(VRCIM_ControllersType));
            typeof(VRCInputManager).GetMethods().Where(x =>
                    x.Name.StartsWith("Method_Public_Static_Boolean_EnumNPublicSealedvaKeMoCoGaViOcViDaWaUnique_")
                ).ToList().ForEach(m => Harmony.Patch(m, l_patchMethod));

            OnPreferencesSaved();
        }

        public override void OnApplicationQuit()
        {
            if(m_leapActive)
                m_leapController.StopConnection();
            m_leapController.Dispose();
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
            ms_inVrMode = (bool)m_vrCheckMethod.Invoke(null, null);

            if(ms_enabled)
            {
                // Use Leap Motion data
                if(m_leapActive && m_leapController.IsConnected)
                {
                    var l_frame = m_leapController.Frame();
                    if(l_frame != null)
                        GestureMatcher.GetGestures(ref l_frame, ref m_gesturesData);
                }

                if(m_sdk3 && (m_parametersMethod != null))
                {
                    // Set SDK3 parameters directly, user has to make own avatar with specific parameters
                    var l_expParams = VRCPlayer.field_Internal_Static_VRCPlayer_0?.prop_VRCAvatarManager_0?.prop_VRCAvatarDescriptor_0?.expressionParameters?.parameters;
                    var l_playableController = VRCPlayer.field_Internal_Static_VRCPlayer_0?.field_Private_AnimatorControllerManager_0?.field_Private_AvatarAnimParamController_0?.field_Private_AvatarPlayableController_0;
                    if((l_expParams != null) && (l_playableController != null))
                    {
                        for(int i = 0; i < l_expParams.Length; i++)
                        {
                            var l_expParam = l_expParams[i];
                            if(l_expParam.name.StartsWith("_HandPresent") && (l_expParam.valueType == VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Bool))
                            {
                                int l_bufferIndex = -1;
                                if(Int32.TryParse(l_expParam.name.Substring(12), out l_bufferIndex))
                                {
                                    if((l_bufferIndex >= 0) && (l_bufferIndex <= 1))
                                    {
                                        m_parametersMethod.Invoke(l_playableController, new object[] { i, (m_gesturesData.m_handsPresenses[l_bufferIndex] ? 1.0f : 0.0f) });
                                    }
                                }
                                continue;
                            }
                            if(l_expParam.name.StartsWith("_FingerBend") && (l_expParam.valueType == VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Float))
                            {
                                int l_bufferIndex = -1;
                                if(Int32.TryParse(l_expParam.name.Substring(11), out l_bufferIndex))
                                {
                                    if((l_bufferIndex >= 0) && (l_bufferIndex <= 9))
                                    {
                                        m_parametersMethod.Invoke(l_playableController, new object[] { i, ((i < 5) ? m_gesturesData.m_leftFingersBends[i] : m_gesturesData.m_rightFingersBends[i - 5]) });
                                    }
                                }
                                continue;
                            }
                            if(l_expParam.name.StartsWith("_FingerSpread") && (l_expParam.valueType == VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Float))
                            {
                                int l_bufferIndex = -1;
                                if(Int32.TryParse(l_expParam.name.Substring(13), out l_bufferIndex))
                                {
                                    if((l_bufferIndex >= 0) && (l_bufferIndex <= 9))
                                    {
                                        m_parametersMethod.Invoke(l_playableController, new object[] { i, (i < 5) ? m_gesturesData.m_leftFingersSpreads[i] : m_gesturesData.m_rightFingersSpreads[i - 5] });
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
                        if(m_gesturesData.m_handsPresenses[0])
                        {
                            if(l_solver.leftArm?.target != null)
                            {
                                Vector3 l_newPos = new Vector3(m_gesturesData.m_handsPositons[0].x, m_gesturesData.m_handsPositons[0].y, -m_gesturesData.m_handsPositons[0].z) * 0.001f;
                                Quaternion l_newRot = new Quaternion(-m_gesturesData.m_handsRotations[0].x, -m_gesturesData.m_handsRotations[0].y, m_gesturesData.m_handsRotations[0].z, m_gesturesData.m_handsRotations[0].w);
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

                        if(m_gesturesData.m_handsPresenses[1])
                        {
                            if(l_solver.rightArm?.target != null)
                            {
                                Vector3 l_newPos = new Vector3(m_gesturesData.m_handsPositons[1].x, m_gesturesData.m_handsPositons[1].y, -m_gesturesData.m_handsPositons[1].z) * 0.001f;
                                Quaternion l_newRot = new Quaternion(-m_gesturesData.m_handsRotations[1].x, -m_gesturesData.m_handsRotations[1].y, m_gesturesData.m_handsRotations[1].z, m_gesturesData.m_handsRotations[1].w);
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
                        if(m_gesturesData.m_handsPresenses[i])
                        {
                            for(int j = 0; j < 5; j++)
                            {
                                int l_dataIndex = i * 5 + j;
                                l_handController.field_Private_ArrayOf_VRCInput_0[l_dataIndex].field_Public_Single_0 = 1.0f - ((i == 0) ? m_gesturesData.m_leftFingersBends[j] : m_gesturesData.m_rightFingersBends[j]); // Squeeze
                                l_handController.field_Private_ArrayOf_VRCInput_1[l_dataIndex].field_Public_Single_0 = ((i == 0) ? m_gesturesData.m_leftFingersSpreads[j] : m_gesturesData.m_rightFingersSpreads[j]); // Spread
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
                        if(m_gesturesData.m_handsPresenses[0])
                        {
                            if(l_solver.leftArm?.target != null)
                            {
                                l_solver.leftArm.positionWeight = 1f;
                                l_solver.leftArm.rotationWeight = 1f;
                                l_solver.leftArm.target.position = m_leftTargetPosition;
                                l_solver.leftArm.target.rotation = m_leftTargetRotation;
                            }
                        }

                        if(m_gesturesData.m_handsPresenses[1])
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
                        if(m_gesturesData.m_handsPresenses[i])
                        {
                            for(int j = 0; j < 5; j++)
                            {
                                int l_dataIndex = i * 5 + j;
                                l_handController.field_Private_ArrayOf_VRCInput_0[l_dataIndex].field_Public_Single_0 = 1.0f - ((i == 0) ? m_gesturesData.m_leftFingersBends[j] : m_gesturesData.m_rightFingersBends[j]); // Squeeze
                                l_handController.field_Private_ArrayOf_VRCInput_1[l_dataIndex].field_Public_Single_0 = ((i == 0) ? m_gesturesData.m_leftFingersSpreads[j] : m_gesturesData.m_rightFingersSpreads[j]); // Spread
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
                if(!m_leapActive)
                {
                    m_leapController.StartConnection();
                    m_leapActive = true;
                }
                if(m_vr)
                    m_leapController.SetPolicy(Leap.Controller.PolicyFlag.POLICY_OPTIMIZE_HMD);
                else
                    m_leapController.ClearPolicy(Leap.Controller.PolicyFlag.POLICY_OPTIMIZE_HMD);
            }
            else
            {
                if(m_leapActive)
                {
                    m_leapController.StopConnection();
                    m_leapActive = false;
                }

                // Reset HandGestureController, IKSolverVR resets hand by itself
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
            var l_height = (float)m_heightMethod.Invoke(null, null);
            pos += m_rootOffset;
            if(!m_useHeadRoot)
            {
                if(m_vr)
                {
                    pos.y += m_rootOffset.y;
                    pos.z -= m_rootOffset.z;
                }
                pos *= l_height;
                pos.y -= (l_height - (float)(ms_inVrMode ? m_vrEyeHeightMethod.Invoke(null, null) : m_eyeHeightMethod.Invoke(null, null)));
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

        public static float ZeroFloat()
        {
            return 0f;
        }

        public static bool FalseBoolean()
        {
            return false;
        }
    }
}
