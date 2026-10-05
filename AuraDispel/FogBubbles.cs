using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.Rendering;

namespace OttoAura.AuraDispel
{
    /// <summary>
    /// Clears the game's distance fog inside the radius where a lit Wisp Torch clears the Mists.
    ///
    /// The main camera renders deferred, so the fog on solid surfaces comes from one
    /// full-screen pass, Post-processing Stack v1's FogComponent, which the game rebuilds every
    /// frame. When a Wisp Torch is in range this class rebuilds that pass itself: it runs the
    /// game's own fog material into a spare buffer, then a second shader blends the unfogged
    /// image back in for the part of each view ray that lies inside a torch's radius.
    /// See FogBubbleMath for the blend.
    ///
    /// Transparent things (water, particles, the Mists themselves) fog in their own shaders
    /// after this pass, so they keep their fog.
    /// </summary>
    internal static class FogBubbles
    {
        /// <summary>Must match the array size in FogBubble.shader.</summary>
        internal const int MaxBubbles = 16;

        private const string BundleResource = "ottoaura_fogbubble.bundle";
        private const string ShaderName = "Hidden/OttoAura/FogBubble";

        private static readonly int TempRT = Shader.PropertyToID("_TempRT");
        private static readonly int FoggedRT = Shader.PropertyToID("_OttoAuraFogged");
        private static readonly int PreFogTex = Shader.PropertyToID("_OttoAuraPreFog");
        private static readonly int RayTopLeft = Shader.PropertyToID("_OttoAuraRayTL");
        private static readonly int RayTopRight = Shader.PropertyToID("_OttoAuraRayTR");
        private static readonly int RayBottomLeft = Shader.PropertyToID("_OttoAuraRayBL");
        private static readonly int RayBottomRight = Shader.PropertyToID("_OttoAuraRayBR");
        private static readonly int CameraPosition = Shader.PropertyToID("_OttoAuraCamPos");
        private static readonly int CameraForward = Shader.PropertyToID("_OttoAuraCamFwd");
        private static readonly int FogParams = Shader.PropertyToID("_OttoAuraFogParams");
        private static readonly int BubbleCount = Shader.PropertyToID("_OttoAuraBubbleCount");
        private static readonly int BubbleArray = Shader.PropertyToID("_OttoAuraBubbles");

        private static readonly Vector4[] Bubbles = new Vector4[MaxBubbles];
        private static readonly List<KeyValuePair<float, Vector4>> Candidates = new();

        private static AssetBundle? _bundle;
        private static Material? _material;
        private static bool _initialized;

        /// <summary>
        /// Loads the bubble shader from the embedded asset bundle. A dedicated server, a
        /// build without the bundle, or a graphics API the shader was not compiled for leaves
        /// the feature off and the game's fog untouched.
        /// </summary>
        internal static void Init()
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                return;
            }

            Assembly assembly = Assembly.GetExecutingAssembly();
            using Stream? resource = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.assets.{BundleResource}");
            if (resource == null)
            {
                OttoAuraPlugin.OttoAuraLogger.LogWarning("The fog bubble shader is not in this build, so Wisp Torches will not clear fog.");
                return;
            }

            using MemoryStream bytes = new();
            resource.CopyTo(bytes);
            _bundle = AssetBundle.LoadFromMemory(bytes.ToArray());
            Shader? shader = null;
            if (_bundle != null)
            {
                foreach (Shader candidate in _bundle.LoadAllAssets<Shader>())
                {
                    if (candidate.name == ShaderName)
                    {
                        shader = candidate;
                    }
                }
            }

            if (shader == null || !shader.isSupported)
            {
                OttoAuraPlugin.OttoAuraLogger.LogWarning($"The fog bubble shader does not run on {SystemInfo.graphicsDeviceType}, so Wisp Torches will not clear fog.");
                return;
            }

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        /// <summary>Turns the feature off for the session after the pass has thrown once.</summary>
        internal static void Fail()
        {
            _material = null;
        }

        internal static void Shutdown()
        {
            if (_material != null)
            {
                UnityEngine.Object.Destroy(_material);
                _material = null;
            }
            if (_bundle != null)
            {
                _bundle.Unload(true);
                _bundle = null;
            }
        }

        /// <summary>
        /// Builds the fog pass with bubbles. Returns false, and leaves the command buffer for
        /// the game to fill as usual, when the feature is off or no torch is in range.
        /// </summary>
        internal static bool TryPopulate(FogComponent fog, CommandBuffer cb)
        {
            if (_material == null || OttoAuraPlugin.AuraDispelEnabled.Value == OttoAuraPlugin.Toggle.Off)
            {
                return false;
            }

            PostProcessingContext context = fog.context;
            Camera camera = context.camera;
            int count = CollectBubbles(camera.transform.position, camera.farClipPlane, OttoAuraPlugin.AuraDispelRadiusScale.Value);
            if (count == 0)
            {
                return false;
            }

            // The game's fog material, set up exactly as FogComponent.PopulateCommandBuffer does.
            Material fogMaterial = context.materialFactory.Get("Hidden/Post FX/Fog");
            fogMaterial.shaderKeywords = null;
            fogMaterial.SetColor("_FogColor", GraphicsUtils.isLinearColorSpace ? RenderSettings.fogColor.linear : RenderSettings.fogColor);
            fogMaterial.SetFloat("_Density", RenderSettings.fogDensity);
            fogMaterial.SetFloat("_Start", RenderSettings.fogStartDistance);
            fogMaterial.SetFloat("_End", RenderSettings.fogEndDistance);
            Vector3 topLeft = camera.ViewportPointToRay(new Vector3(0f, 1f, 0f)).direction;
            Vector3 topRight = camera.ViewportPointToRay(new Vector3(1f, 1f, 0f)).direction;
            Vector3 bottomLeft = camera.ViewportPointToRay(new Vector3(0f, 0f, 0f)).direction;
            Vector3 bottomRight = camera.ViewportPointToRay(new Vector3(1f, 0f, 0f)).direction;
            fogMaterial.SetVector("_TopLeft", topLeft);
            fogMaterial.SetVector("_TopRight", topRight);
            fogMaterial.SetVector("_BottomLeft", bottomLeft);
            fogMaterial.SetVector("_BottomRight", bottomRight);

            _material.shaderKeywords = null;
            switch (RenderSettings.fogMode)
            {
                case FogMode.Linear:
                    fogMaterial.EnableKeyword("FOG_LINEAR");
                    _material.EnableKeyword("FOG_LINEAR");
                    break;
                case FogMode.Exponential:
                    fogMaterial.EnableKeyword("FOG_EXP");
                    _material.EnableKeyword("FOG_EXP");
                    break;
                case FogMode.ExponentialSquared:
                    fogMaterial.EnableKeyword("FOG_EXP2");
                    _material.EnableKeyword("FOG_EXP2");
                    break;
            }

            _material.SetVector(RayTopLeft, topLeft);
            _material.SetVector(RayTopRight, topRight);
            _material.SetVector(RayBottomLeft, bottomLeft);
            _material.SetVector(RayBottomRight, bottomRight);
            _material.SetVector(CameraPosition, camera.transform.position);
            _material.SetVector(CameraForward, camera.transform.forward);
            _material.SetVector(FogParams, new Vector4(RenderSettings.fogDensity, RenderSettings.fogStartDistance, RenderSettings.fogEndDistance, camera.nearClipPlane));
            _material.SetInt(BubbleCount, count);
            _material.SetVectorArray(BubbleArray, Bubbles);

            RenderTextureFormat format = context.isHdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            int pass = fog.model.settings.excludeSkybox ? 1 : 0;
            cb.GetTemporaryRT(TempRT, context.width, context.height, 24, FilterMode.Bilinear, format);
            cb.GetTemporaryRT(FoggedRT, context.width, context.height, 0, FilterMode.Bilinear, format);
            cb.Blit(BuiltinRenderTextureType.CameraTarget, TempRT);
            cb.Blit(TempRT, FoggedRT, fogMaterial, pass);
            cb.SetGlobalTexture(PreFogTex, TempRT);
            cb.Blit(FoggedRT, BuiltinRenderTextureType.CameraTarget, _material, 0);
            cb.ReleaseTemporaryRT(FoggedRT);
            cb.ReleaseTemporaryRT(TempRT);
            return true;
        }

        /// <summary>
        /// Fills Bubbles with the lit Wisp Torches nearest the camera, as center and radius,
        /// and returns how many it found. A torch counts while its mist-clearing force field
        /// is active, which is the same test the game uses for the Mists.
        /// </summary>
        private static int CollectBubbles(Vector3 cameraPosition, float maxDistance, float radiusScale)
        {
            Candidates.Clear();
            foreach (Demister demister in Demister.GetDemisters())
            {
                if (demister == null || !demister.isActiveAndEnabled)
                {
                    continue;
                }

                ParticleSystemForceField field = demister.m_forceField;
                if (field == null || !field.enabled)
                {
                    continue;
                }

                // A built piece, such as the Wisp Torch. The Wisplight a player carries is not.
                if (demister.GetComponentInParent<Piece>() == null)
                {
                    continue;
                }

                float radius = field.endRange * radiusScale;
                Vector3 center = demister.transform.position;
                float surfaceDistance = Vector3.Distance(center, cameraPosition) - radius;
                if (radius <= 0f || surfaceDistance > maxDistance)
                {
                    continue;
                }

                Candidates.Add(new KeyValuePair<float, Vector4>(surfaceDistance, new Vector4(center.x, center.y, center.z, radius)));
            }

            Candidates.Sort((a, b) => a.Key.CompareTo(b.Key));
            int count = Math.Min(Candidates.Count, MaxBubbles);
            for (int i = 0; i < MaxBubbles; i++)
            {
                Bubbles[i] = i < count ? Candidates[i].Value : Vector4.zero;
            }
            return count;
        }
    }

    [HarmonyPatch(typeof(FogComponent), nameof(FogComponent.PopulateCommandBuffer))]
    internal static class FogComponentPopulatePatch
    {
        private static bool Prefix(FogComponent __instance, CommandBuffer cb)
        {
            try
            {
                return !FogBubbles.TryPopulate(__instance, cb);
            }
            catch (Exception e)
            {
                OttoAuraPlugin.OttoAuraLogger.LogError($"The fog bubble pass failed, so Wisp Torches stop clearing fog until the game restarts: {e}");
                FogBubbles.Fail();
                cb.Clear();
                return true;
            }
        }
    }
}
