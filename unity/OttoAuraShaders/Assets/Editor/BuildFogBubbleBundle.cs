using System;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering;

/// <summary>
/// Builds the asset bundle OttoAura embeds as assets/ottoaura_fogbubble.bundle.
/// Run through tools/build-fog-bubble-bundle.sh, which passes -bundleOutput.
/// The editor must be the exact Unity version Valheim ships with, or the compiled
/// shader may not load in the game.
/// </summary>
public static class BuildFogBubbleBundle
{
    private const string BundleName = "ottoaura_fogbubble.bundle";
    private const string ShaderPath = "Assets/OttoAura/Shaders/FogBubble.shader";

    public static void Build()
    {
        string output = ArgAfter("-bundleOutput") ?? "Build";
        Directory.CreateDirectory(output);

        // Valheim runs on Direct3D 11 by default and on Direct3D 12 or Vulkan when asked.
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[]
        {
            GraphicsDeviceType.Direct3D11,
            GraphicsDeviceType.Direct3D12,
            GraphicsDeviceType.Vulkan,
        });

        AssetBundleBuild[] builds =
        {
            new AssetBundleBuild { assetBundleName = BundleName, assetNames = new[] { ShaderPath } },
        };
        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
            output,
            builds,
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode,
            BuildTarget.StandaloneWindows64);

        if (manifest == null || !File.Exists(Path.Combine(output, BundleName)))
        {
            Console.Error.WriteLine("BuildFogBubbleBundle: the bundle was not built.");
            EditorApplication.Exit(1);
        }
    }

    private static string ArgAfter(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
