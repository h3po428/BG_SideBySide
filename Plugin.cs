using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace BG_SideBySide
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource sharedLogger;
        
        ConditionalWeakTable<Camera, WeakReference<Camera>> cameraToRightEyeCamera = new();
        

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            Logger.LogInfo("Patch Applied!");
            sharedLogger = this.Logger;

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (this.cameraToRightEyeCamera.TryGetValue(camera, out var _))
            {
            }
            else if (camera.aspect > 1.5f && camera.aspect < 1.9f)
            {
                if (camera.gameObject.GetComponent<StereoCameraProvider>() != null) return;
                camera.gameObject.AddComponent<StereoCameraProvider>();
                this.cameraToRightEyeCamera.Add(camera, new (camera));
            }
        }
    }

    class StereoCameraProvider: MonoBehaviour
    {
        public Camera leftEyeCamera;
        public Camera rightEyeCamera;

        void Start()
        {
            if (this.gameObject.name.EndsWith("_RightEyeCamera"))
            {
                Destroy(this);
                return;
            }
            // UniversalAdditionalCameraData も必要だが Instantiate でしかコピーできないので Instantiate を使っている
            var rightEyeCameraObj = Instantiate(this.gameObject, this.transform);
            rightEyeCameraObj.name += "_RightEyeCamera";
            rightEyeCameraObj.transform.SetLocalPositionAndRotation(new Vector3(0.063f, 0, 0), Quaternion.identity);
            rightEyeCamera = rightEyeCameraObj.GetComponent<Camera>();
            rightEyeCamera.rect = new Rect(0.5f, 0, 0.5f, 1);

            leftEyeCamera = this.GetComponent<Camera>();
            leftEyeCamera.rect = new Rect(0, 0, 0.5f, 1);

            leftEyeCamera.ResetAspect();
            rightEyeCamera.ResetAspect();

            leftEyeCamera.aspect *= 2;
            rightEyeCamera.aspect *= 2;
        }
    }

    [HarmonyPatch]
    class PatchHiresFullscreen
    {
        const int ORIGINAL_WIDTH = 1920;
        const int ORIGINAL_HEIGHT = 1080;
        const float ORIGINAL_ASPECT_RATIO = (float)ORIGINAL_WIDTH / (float)ORIGINAL_HEIGHT;
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("GB.GBSystem");
            var method = AccessTools.Method(type, "CalcFullScreenResolution");
            return method;
        }

        static bool Prefix(ref (int width, int height, bool) __result)
        {
            var displayInfo = Screen.mainWindowDisplayInfo;

            float displayAspectRatio = (float)displayInfo.width / (float)displayInfo.height;

            if (displayAspectRatio > ORIGINAL_ASPECT_RATIO)
            {
                // Adjusted by height
                __result.width = (int)(displayInfo.height * displayAspectRatio);
                __result.height = displayInfo.height;
            } else
            {
                // Adjusted by width
                __result.width = displayInfo.width;
                __result.height = (int)(displayInfo.width / displayAspectRatio);
            }

            return false;
        }
    }

    [HarmonyPatch]
    class PatchFPSLimit
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("GB.GBSystem");
            var method = AccessTools.Method(type, "Setup");
            return method;
        }

        static void Postfix()
        {
            Application.targetFrameRate = 0;
            QualitySettings.vSyncCount = 1;
        }
    }
}
