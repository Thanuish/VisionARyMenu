using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

namespace VisionARyMenu.Editor
{
    // Android/ARCore equivalent of MenuViewer's MenuViewerProjectSetup.ConfigureIosArKit,
    // following the same pattern: one idempotent command that captures the Android/ARCore
    // gotchas already discovered getting MenuViewer running on-device (min API level, engine
    // code stripping, the Standard shader fix) as reproducible code instead of manual Editor
    // clicks that are easy to lose.
    public static class VisionARyProjectSetup
    {
        private const string ArCoreLoaderType = "UnityEngine.XR.ARCore.ARCoreLoader";
        internal const string AndroidBundleIdentifier = "com.thanuish.visionarymenu";
        internal const int MinimumAndroidApiLevel = 29;

        [MenuItem("VisionARy/Configure Android ARCore")]
        public static void ConfigureAndroidArCore()
        {
            var settings = GetOrCreateBuildTargetSettings();
            if (!settings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            {
                settings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            }

            var generalSettings = settings.SettingsForBuildTarget(BuildTargetGroup.Android);
            var managerSettings = settings.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            generalSettings.InitManagerOnStart = true;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidBundleIdentifier);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinimumAndroidApiLevel;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

            // IL2CPP strips UnityEngine.CapsuleCollider (and any other engine type nothing in
            // project scripts references directly) when this is left on, since AR
            // Foundation/XR Origin add some components dynamically at runtime that static
            // analysis can't detect — that produced a real on-device crash ("Can't add
            // component because the type doesn't exist!") getting MenuViewer running on
            // Android earlier. Keep this off.
            PlayerSettings.stripEngineCode = false;

            // Fixed portrait keeps the interface orientation unambiguous from the first frame,
            // avoiding a stale-orientation-latch class of AR pose bugs (see
            // ArCameraPoseDriver.SensorToScreenRotation) while that behavior is verified on
            // ARCore. Revisit once confirmed.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // ProceduralFoodModelFactory builds runtime-only primitive dishes with no scene
            // reference to the Standard shader, so IL2CPP strips it and primitives render
            // magenta on device without this.
            EnsureAlwaysIncludedShader("Standard");

            if (!XRPackageMetadataStore.AssignLoader(managerSettings, ArCoreLoaderType, BuildTargetGroup.Android))
            {
                Debug.LogWarning("VisionARy could not assign the ARCore loader. Confirm the ARCore XR Plugin package and Android Build Support are installed.");
            }

            EditorUtility.SetDirty(generalSettings);
            EditorUtility.SetDirty(managerSettings);
            AssetDatabase.SaveAssets();
            Debug.Log("VisionARy Android ARCore XR settings are configured.");
        }

        private static void EnsureAlwaysIncludedShader(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"VisionARy could not find shader '{shaderName}' to add to Always Included Shaders.");
                return;
            }

            var graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
            var serializedSettings = new SerializedObject(graphicsSettings);
            var alwaysIncluded = serializedSettings.FindProperty("m_AlwaysIncludedShaders");
            for (var i = 0; i < alwaysIncluded.arraySize; i++)
            {
                if (alwaysIncluded.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    return;
                }
            }

            alwaysIncluded.InsertArrayElementAtIndex(alwaysIncluded.arraySize);
            alwaysIncluded.GetArrayElementAtIndex(alwaysIncluded.arraySize - 1).objectReferenceValue = shader;
            serializedSettings.ApplyModifiedProperties();
        }

        private static XRGeneralSettingsPerBuildTarget GetOrCreateBuildTargetSettings()
        {
            var method = typeof(XRGeneralSettingsPerBuildTarget).GetMethod("GetOrCreate", BindingFlags.NonPublic | BindingFlags.Static);
            return (XRGeneralSettingsPerBuildTarget)method.Invoke(null, null);
        }
    }
}
