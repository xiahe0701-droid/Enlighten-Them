using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class FiveAssetMaterialRepair
{
    private const string Root = "Assets/3DModel/";
    private static readonly string[] Folders = { "lamp-incandescent-lamp", "light-gun", "round-platform", "steam-punk-lamp", "steam-punk-lamp-post" };

    // Explicit, scoped repair. Does not change scenes or source meshes.
    [MenuItem("Tools/Art/Repair Five Imported Asset Materials")]
    public static void Repair()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) throw new InvalidOperationException("URP Lit shader missing.");
        foreach (var folder in Folders)
        {
            var source = Root + folder + "/source/";
            var textures = Root + folder + "/textures/";
            foreach (var path in Directory.GetFiles(textures).Where(p => !p.EndsWith(".meta")))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer) continue;
                var name = Path.GetFileName(path).ToLowerInvariant();
                var normal = name.Contains("normal") || name.Contains("_norma");
                var linear = normal || name.Contains("roughness") || name.Contains("metallic") || name.Contains("_ao") || name.Contains("alpha");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !linear;
                importer.flipGreenChannel = normal && name.Contains("directx");
                importer.SaveAndReimport();
            }
            var modelPath = Directory.GetFiles(source).First(p => p.EndsWith(".fbx") || p.EndsWith(".obj")).Replace('\\', '/');
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var slots = model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m).GroupBy(m => m.name).Select(g => g.First()).ToArray();
            var importerModel = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            foreach (var original in slots)
            {
                var matPath = source + original.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!mat) { mat = new Material(original); AssetDatabase.CreateAsset(mat, matPath); }
                mat.shader = shader;
                mat.shaderKeywords = Array.Empty<string>();
                mat.SetFloat("_Surface", 0); mat.SetFloat("_Blend", 0);
                mat.SetFloat("_AlphaClip", 0); mat.SetFloat("_WorkflowMode", 1);
                mat.SetFloat("_Smoothness", 0.35f);
                mat.SetTexture("_ParallaxMap", null);
                if (folder == "round-platform")
                {
                    mat.SetColor("_BaseColor", Color.white);
                    mat.SetTexture("_BaseMap", Load(textures, "round_platform_defaultPolygonShader1_BaseM.png"));
                    mat.SetTexture("_BumpMap", Load(textures, "round_platform_defaultPolygonShader1_Norma.png"));
                }
                else if (folder == "steam-punk-lamp-post")
                {
                    mat.SetColor("_BaseColor", Color.white);
                    mat.SetTexture("_BaseMap", Load(textures, "lambert1_Base_color.png"));
                    mat.SetTexture("_BumpMap", Load(textures, "lambert1_Normal_OpenGL.png"));
                    mat.SetTexture("_OcclusionMap", Load(textures, "lambert1_Mixed_AO.png"));
                    mat.SetTexture("_MetallicGlossMap", Pack(textures, "lambert1_Metallic.png", "lambert1_Roughness.png", false));
                    mat.SetFloat("_Smoothness", 1);
                }
                else if (folder == "steam-punk-lamp")
                {
                    bool glass = original.name == "glass";
                    mat.SetColor("_BaseColor", glass ? new Color(0.9f, 0.96f, 1, 0.2f) : Color.white);
                    mat.SetTexture("_BaseMap", glass ? null : Load(textures, "Mesh_Base_Color.png"));
                    mat.SetTexture("_BumpMap", glass ? null : Load(textures, "Mesh_Normal_DirectX.png"));
                    mat.SetTexture("_MetallicGlossMap", glass ? null : Pack(textures, "Mesh_Metallic.png", "Mesh_Roughness.png", false));
                    mat.SetFloat("_Smoothness", glass ? 0.95f : 1);
                    mat.SetFloat("_Metallic", 0);
                    mat.SetFloat("_Surface", glass ? 1 : 0);
                    mat.SetTexture("_EmissionMap", glass ? null : Load(textures, "filament_emis1_Emissive.png"));
                    mat.SetColor("_EmissionColor", glass ? Color.black : Color.white * 2);
                }
                else if (folder == "lamp-incandescent-lamp")
                {
                    mat.SetColor("_BaseColor", Color.white);
                    mat.SetTexture("_BaseMap", Pack(textures, "lightbulb_01_diff_4k.jpg", "lightbulb_01_alpha_4k.png", true));
                    mat.SetTexture("_BumpMap", Load(textures, "لامپ_default_Normal.png"));
                    mat.SetTexture("_MetallicGlossMap", Pack(textures, null, "لامپ_default_Roughness.png", false));
                    mat.SetFloat("_Smoothness", 1);
                    mat.SetFloat("_Surface", 1);
                }
                else if (folder == "light-gun")
                {
                    // Keep the source's per-part colors; only the energy plane uses the lightning atlas.
                    if (original.name == "Material.005")
                    {
                        var lightning = Pack(textures, "BlueLightning.png", "BlueLightning.png", true);
                        mat.SetTexture("_BaseMap", lightning);
                        mat.SetTexture("_EmissionMap", lightning);
                        mat.SetColor("_BaseColor", Color.white);
                        mat.SetColor("_EmissionColor", Color.white * 2);
                        mat.SetFloat("_Surface", 1);
                        mat.SetFloat("_Cull", 0);
                        mat.SetFloat("_Smoothness", 0);
                        mat.SetFloat("_SpecularHighlights", 0);
                        mat.SetFloat("_EnvironmentReflections", 0);
                    }
                }
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", mat.GetTexture("_BaseMap"));
                var guiType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.Rendering.Universal.ShaderGUI.LitShader")).First(t => t != null);
                ((ShaderGUI)Activator.CreateInstance(guiType, true)).ValidateMaterial(mat);
                EditorUtility.SetDirty(mat);
                importerModel.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), original.name), mat);
            }
            AssetDatabase.SaveAssets();
            importerModel.SaveAndReimport();
        }
        Inspect();
        Validate();
        File.WriteAllText("Logs/FiveAssetMaterialsFixed.txt", "Validated five models: all renderer materials use external URP Lit materials.\n");
        Debug.Log("Five asset material repair completed and validated.");
    }

    private static Texture2D Load(string directory, string name)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(directory + name);
        if (!texture) throw new FileNotFoundException(directory + name);
        return texture;
    }


    // URP expects metallic in R and smoothness (1 - roughness) in A.
    // Base-color opacity maps instead use RGB from color and A from the opacity image.
    private static Texture2D Pack(string directory, string rgbName, string maskName, bool opacity)
    {
        string path = directory + (opacity ? "BaseColorOpacity_URP.png" : "MetallicSmoothness_URP.png");
        var mask = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        var rgb = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        try
        {
            if (!mask.LoadImage(File.ReadAllBytes(directory + maskName))) throw new IOException(maskName);
            if (rgbName != null && !rgb.LoadImage(File.ReadAllBytes(directory + rgbName))) throw new IOException(rgbName);
            var output = new Texture2D(mask.width, mask.height, TextureFormat.RGBA32, false, !opacity);
            try
            {
                var pixels = mask.GetPixels();
                for (int y = 0; y < mask.height; y++) for (int x = 0; x < mask.width; x++)
                {
                    int i = y * mask.width + x;
                    Color c = rgbName == null ? Color.black : rgb.GetPixelBilinear((x + 0.5f) / mask.width, (y + 0.5f) / mask.height);
                    c.a = opacity ? pixels[i].r : 1 - pixels[i].r;
                    pixels[i] = c;
                }
                output.SetPixels(pixels); output.Apply();
                File.WriteAllBytes(path, output.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(output); }
        }
        finally { UnityEngine.Object.DestroyImmediate(mask); UnityEngine.Object.DestroyImmediate(rgb); }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture = opacity;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = opacity;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    public static void Validate()
    {
        foreach (var folder in Folders)
        {
            var modelPath = Directory.GetFiles(Root + folder + "/source").First(p => p.EndsWith(".fbx") || p.EndsWith(".obj"));
            foreach (var renderer in AssetDatabase.LoadAssetAtPath<GameObject>(modelPath).GetComponentsInChildren<Renderer>(true))
                foreach (var mat in renderer.sharedMaterials)
                    if (!mat || mat.shader.name != "Universal Render Pipeline/Lit" || !AssetDatabase.GetAssetPath(mat).EndsWith(".mat"))
                        throw new InvalidOperationException("Unrepaired material on " + folder + "/" + renderer.name);
        }
    }

    [MenuItem("Tools/Art/Preview Five Imported Assets")]
    public static void Preview()
    {
        Validate();
        foreach (var folder in Folders)
        {
            var path = Directory.GetFiles(Root + folder + "/source").First(p => p.EndsWith(".fbx") || p.EndsWith(".obj"));
            var preview = new PreviewRenderUtility();
            try
            {
                var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                preview.AddSingleGO(instance);
                var renderers = instance.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var radius = Mathf.Max(bounds.extents.magnitude, 0.001f);
                preview.camera.transform.position = bounds.center + new Vector3(1, 0.6f, 1).normalized * radius * 3.3f;
                preview.camera.transform.LookAt(bounds.center);
                preview.camera.nearClipPlane = radius * 0.01f;
                preview.camera.farClipPlane = radius * 10;
                preview.camera.fieldOfView = 40;
                preview.camera.clearFlags = CameraClearFlags.Color;
                preview.camera.backgroundColor = new Color(0.15f, 0.17f, 0.20f, 1);
                preview.lights[0].intensity = 2;
                preview.lights[0].transform.rotation = Quaternion.Euler(40, 30, 0);
                preview.lights[1].intensity = 1;
                preview.lights[1].transform.rotation = Quaternion.Euler(340, 210, 0);
                preview.ambientColor = new Color(0.4f, 0.4f, 0.4f);
                preview.BeginStaticPreview(new Rect(0, 0, 640, 640));
                preview.Render(true);
                var image = preview.EndStaticPreview();
                File.WriteAllBytes("Logs/" + folder + "-preview.png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally { preview.Cleanup(); }
        }
        File.WriteAllText("Logs/FiveAssetPreviewComplete.txt", "Rendered all five assets.");
    }

    public static void Inspect()
    {
        var report = new StringBuilder();
        foreach (var folder in Folders)
        {
            var path = Directory.GetFiles(Root + folder + "/source").First(p => p.EndsWith(".fbx") || p.EndsWith(".obj")).Replace('\\', '/');
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            report.AppendLine(path);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    report.AppendLine("  " + renderer.name + " -> " + (material ? material.name + " | " + material.shader.name + " | " + AssetDatabase.GetAssetPath(material) : "NULL"));
                    if (!material) continue;
                    foreach (var prop in material.GetTexturePropertyNames())
                        if (material.GetTexture(prop)) report.AppendLine("    " + prop + " = " + AssetDatabase.GetAssetPath(material.GetTexture(prop)));
                }
        }
        File.WriteAllText("Logs/FiveAssetMaterialInspection.txt", report.ToString());
        Debug.Log(report.ToString());
    }
}
