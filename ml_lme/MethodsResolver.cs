using System.Linq;

namespace ml_lme
{
    public static class MethodsResolver
    {
        public static System.Reflection.MethodInfo GetHeightMethod()
        {
            System.Reflection.MethodInfo l_result = null;

            var l_methodsList = typeof(VRCTrackingManager).GetMethods()
                .Where(m => m.Name.StartsWith("Method_Public_Static_Single_") && m.ReturnType == typeof(float) && m.GetParameters().Count() == 0 && UnhollowerRuntimeLib.XrefScans.XrefScanner.UsedBy(m)
                .Where(x => x.Type == UnhollowerRuntimeLib.XrefScans.XrefType.Method && x.TryResolve()?.DeclaringType == typeof(VRCVrCamera)).Count() >= 1 && UnhollowerRuntimeLib.XrefScans.XrefScanner.UsedBy(m)
                .Where(x => x.Type == UnhollowerRuntimeLib.XrefScans.XrefType.Method && x.TryResolve()?.DeclaringType == typeof(IkController)).Count() == 0);

            if(l_methodsList.Count() != 0)
                l_result = l_methodsList.First();

            return l_result;
        }

        public static System.Reflection.MethodInfo GetEyeHeightMethod()
        {
            System.Reflection.MethodInfo l_result = null;

            var l_methodsList = typeof(VRCTrackingManager).GetMethods()
                .Where(m => m.Name.StartsWith("Method_Public_Static_Single_") && m.ReturnType == typeof(float) && m.GetParameters().Count() == 0 && UnhollowerRuntimeLib.XrefScans.XrefScanner.XrefScan(m)
                .Where(x => x.Type == UnhollowerRuntimeLib.XrefScans.XrefType.Method && x.TryResolve()?.DeclaringType == typeof(VRCPlayer)).Count() >= 1 && UnhollowerRuntimeLib.XrefScans.XrefScanner.UsedBy(m)
                .Where(x => x.Type == UnhollowerRuntimeLib.XrefScans.XrefType.Method && x.TryResolve()?.DeclaringType == typeof(VRCVrCameraUnity)).Count() >= 3);

            if(l_methodsList.Count() != 0)
                l_result = l_methodsList.First();

            return l_result;
        }

        public static System.Reflection.MethodInfo GetVREyeHeightMethod()
        {
            System.Reflection.MethodInfo l_result = null;

            var l_methodsList = typeof(VRCTrackingManager).GetMethods()
                .Where(m => m.Name.StartsWith("Method_Public_Static_Single_") && m.ReturnType == typeof(float) && m.GetParameters().Count() == 0 && UnhollowerRuntimeLib.XrefScans.XrefScanner.XrefScan(m)
                .Where(x => x.Type == UnhollowerRuntimeLib.XrefScans.XrefType.Method && x.TryResolve()?.DeclaringType == typeof(VRCVrCamera)).Count() >= 2
                && UnhollowerRuntimeLib.XrefScans.XrefScanner.UsedBy(m).Count() == 0);

            if(l_methodsList.Count() != 0)
                l_result = l_methodsList.First();

            return l_result;
        }

        public static System.Reflection.MethodInfo GetVRCheckMethod()
        {
            System.Reflection.MethodInfo l_result = null;

            var l_methodsList = typeof(VRCTrackingManager).GetMethods()
                .Where(m => m.Name.StartsWith("Method_Public_Static_Boolean_") && m.ReturnType == typeof(bool) && m.GetParameters().Count() == 0 && UnhollowerRuntimeLib.XrefScans.XrefScanner.UsedBy(m)
                .Where(x => x.Type == UnhollowerRuntimeLib.XrefScans.XrefType.Method && x.TryResolve()?.DeclaringType == typeof(AvatarDebugConsole)).Count() > 1 && UnhollowerRuntimeLib.XrefScans.XrefScanner.UsedBy(m)
                .Where(x => x.Type == UnhollowerRuntimeLib.XrefScans.XrefType.Method && x.TryResolve()?.DeclaringType == typeof(VRCFlowManager)).Count() >= 1 && UnhollowerRuntimeLib.XrefScans.XrefScanner.UsedBy(m)
                .Where(x => x.Type == UnhollowerRuntimeLib.XrefScans.XrefType.Method && x.TryResolve()?.DeclaringType == typeof(SpawnManager)).Count() >= 1);

            if(l_methodsList.Count() != 0)
                l_result = l_methodsList.First();

            return l_result;

        }

        public static System.Reflection.MethodInfo GetSDK3ParameterSetMethod()
        {
            System.Reflection.MethodInfo l_result = null;


            var l_methodsList = typeof(AvatarPlayableController).GetMethods()
                .Where(m => m.Name.StartsWith("Method_Public_Boolean_Int32_Single_") && m.ReturnType == typeof(bool) && m.GetParameters().Count() == 2 && UnhollowerRuntimeLib.XrefScans.XrefScanner.UsedBy(m)
                .Where(x => x.Type == UnhollowerRuntimeLib.XrefScans.XrefType.Method && x.TryResolve()?.DeclaringType == typeof(ActionMenu)).Count() == 0 && UnhollowerRuntimeLib.XrefScans.XrefScanner.XrefScan(m)
                .Where(x => x.Type == UnhollowerRuntimeLib.XrefScans.XrefType.Method && x.TryResolve()?.DeclaringType == typeof(Numerics)).Count() > 0);

            if(l_methodsList.Count() != 0)
                l_result = l_methodsList.First();

            return l_result;
        }
    }
}
