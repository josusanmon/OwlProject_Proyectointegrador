using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Menu "Owl Project > Iluminacion" para activar la iluminacion global (GI) dinamica.
///
/// Unity 6.6 (URP 17.6) trae dos funciones nuevas que este script activa si existen:
///  - Surface Cache Global Illumination: GI en tiempo real, sin hornear. La luz
///    rebota en las calles y edificios y cambia si se mueve el sol o un objeto.
///  - Screen Space Reflections (SSR): reflejos de lo que se ve en pantalla.
///
/// Si su version de Unity no las tiene, el script configura Adaptive Probe
/// Volumes (APV) con oclusion de cielo y hornea: el rebote de luz queda
/// horneado, pero la luz del cielo sigue siendo dinamica.
/// </summary>
public static class ConfigurarIluminacion
{
    const string Menu = "Owl Project/Iluminacion/";

    const string TipoSurfaceCacheFeature = "UnityEngine.Rendering.Universal.SurfaceCacheGIRendererFeature";
    const string TipoSurfaceCacheVolume = "UnityEngine.Rendering.Universal.SurfaceCacheGIVolumeOverride";
    const string TipoSSRFeature = "UnityEngine.Rendering.Universal.ScreenSpaceReflectionRendererFeature";
    const string TipoSSRVolume = "UnityEngine.Rendering.Universal.ScreenSpaceReflectionVolumeSettings";

    // Raices de la escena que son "entorno" (no se mueven)
    static readonly string[] RaicesEntorno = { "calles", "Banquetas2", "SEÑALICACION", "Terreno" };

    [MenuItem(Menu + "Activar GI dinamica")]
    public static void ActivarGIDinamica()
    {
        var reporte = new System.Text.StringBuilder();

        bool surfaceCache = AgregarRendererFeature(TipoSurfaceCacheFeature, "Surface Cache GI", reporte);
        if (surfaceCache)
        {
            AgregarOverride(TipoSurfaceCacheVolume, reporte);
            // Surface Cache GI no funciona con Static Batching
            foreach (var plataforma in new[] { BuildTarget.StandaloneWindows64, BuildTarget.StandaloneOSX, EditorUserBuildSettings.activeBuildTarget })
                PlayerSettings.SetStaticBatchingForPlatform(plataforma, false);
            reporte.AppendLine("• Static Batching desactivado (lo pide Surface Cache GI).");
        }

        if (AgregarRendererFeature(TipoSSRFeature, "Screen Space Reflections", reporte))
            AgregarOverride(TipoSSRVolume, reporte);

        if (!surfaceCache)
        {
            reporte.AppendLine("• Surface Cache GI no existe en esta version de Unity: se usara Adaptive Probe Volumes.");
            ConfigurarAPV(reporte);
            reporte.AppendLine("• Falta hornear: menu Owl Project > Iluminacion > Hornear APV.");
        }

        MarcarEntornoEstatico(reporte);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Mostrar("GI dinamica", reporte.ToString());
    }

    [MenuItem(Menu + "Hornear APV")]
    public static void HornearAPV()
    {
        var reporte = new System.Text.StringBuilder();
        var set = ConfigurarAPV(reporte);
        MarcarEntornoEstatico(reporte);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        // El horneo usa el Baking Set activo
        ProbeReferenceVolume.instance.SetActiveBakingSet(set);
        if (Application.isBatchMode) Lightmapping.Bake();
        else Lightmapping.BakeAsync();
    }

    // ---------------------------------------------------------------------
    // Renderer Features / Volume overrides por nombre de tipo (por si no existen)
    // ---------------------------------------------------------------------

    static Type BuscarTipo(string nombreCompleto)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(nombreCompleto, false))
            .FirstOrDefault(t => t != null);
    }

    static ScriptableRendererData RendererPorDefecto()
    {
        var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (asset == null) return null;
        var so = new SerializedObject(asset);
        var lista = so.FindProperty("m_RendererDataList");
        int indice = so.FindProperty("m_DefaultRendererIndex").intValue;
        if (lista == null || indice < 0 || indice >= lista.arraySize) return null;
        return lista.GetArrayElementAtIndex(indice).objectReferenceValue as ScriptableRendererData;
    }

    static bool AgregarRendererFeature(string nombreTipo, string nombreVisible, System.Text.StringBuilder reporte)
    {
        Type tipo = BuscarTipo(nombreTipo);
        if (tipo == null) return false;

        var datos = RendererPorDefecto();
        if (datos == null)
        {
            reporte.AppendLine("• No se encontro el renderer de URP.");
            return false;
        }

        if (datos.rendererFeatures.Any(f => f != null && f.GetType() == tipo))
        {
            reporte.AppendLine($"• {nombreVisible}: ya estaba en {datos.name}.");
            return true;
        }

        var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(tipo);
        feature.name = nombreVisible;
        AssetDatabase.AddObjectToAsset(feature, datos);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long idLocal);

        var so = new SerializedObject(datos);
        var features = so.FindProperty("m_RendererFeatures");
        var mapa = so.FindProperty("m_RendererFeatureMap");
        features.arraySize++;
        features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
        mapa.arraySize++;
        mapa.GetArrayElementAtIndex(mapa.arraySize - 1).longValue = idLocal;
        so.ApplyModifiedProperties();

        datos.SetDirty();
        EditorUtility.SetDirty(datos);
        reporte.AppendLine($"• {nombreVisible}: agregado a {datos.name}.");
        return true;
    }

    static VolumeProfile PerfilGlobal()
    {
        var volumen = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None)
            .FirstOrDefault(v => v.isGlobal && v.sharedProfile != null);
        return volumen != null ? volumen.sharedProfile : null;
    }

    static void AgregarOverride(string nombreTipo, System.Text.StringBuilder reporte)
    {
        Type tipo = BuscarTipo(nombreTipo);
        var perfil = PerfilGlobal();
        if (tipo == null || perfil == null) return;
        if (perfil.Has(tipo)) return;

        var componente = perfil.Add(tipo, true);
        componente.name = tipo.Name;
        AssetDatabase.AddObjectToAsset(componente, perfil);
        EditorUtility.SetDirty(perfil);
        reporte.AppendLine($"• Override {tipo.Name} agregado a {perfil.name}.");
    }

    // ---------------------------------------------------------------------
    // Adaptive Probe Volumes (respaldo si no hay Surface Cache GI)
    // ---------------------------------------------------------------------

    static ProbeVolumeBakingSet ConfigurarAPV(System.Text.StringBuilder reporte)
    {
        // 1. El URP Asset debe usar Probe Volumes como sistema de Light Probes
        var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (asset != null)
        {
            var so = new SerializedObject(asset);
            so.FindProperty("m_LightProbeSystem").intValue = 1;     // Adaptive Probe Volumes
            var bandas = so.FindProperty("m_ProbeVolumeSHBands");
            if (bandas != null) bandas.intValue = 2;                // L2: mas detalle
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }

        // 2. Un Probe Volume global que cubre toda la escena
        var pv = Object.FindAnyObjectByType<ProbeVolume>();
        if (pv == null)
        {
            var go = new GameObject("GI - Adaptive Probe Volume");
            pv = go.AddComponent<ProbeVolume>();
            Undo.RegisterCreatedObjectUndo(go, "Crear Probe Volume");
        }
        pv.mode = ProbeVolume.Mode.Global;

        // 3. Baking Set con oclusion de cielo (la luz del cielo se actualiza en tiempo real)
        var escena = SceneManager.GetActiveScene();
        string guidEscena = AssetDatabase.AssetPathToGUID(escena.path);
        var set = AssetDatabase.FindAssets("t:ProbeVolumeBakingSet")
            .Select(g => AssetDatabase.LoadAssetAtPath<ProbeVolumeBakingSet>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(s => s != null && s.sceneGUIDs.Contains(guidEscena));
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<ProbeVolumeBakingSet>();
            typeof(ProbeVolumeBakingSet).GetMethod("SetDefaults",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.Invoke(set, null);
            set.name = escena.name + " Baking Set";
            set.TryAddScene(guidEscena);
            string carpeta = System.IO.Path.GetDirectoryName(escena.path);
            AssetDatabase.CreateAsset(set, AssetDatabase.GenerateUniqueAssetPath($"{carpeta}/{escena.name} Baking Set.asset"));
        }
        set.skyOcclusion = true;
        EditorUtility.SetDirty(set);

        // 4. Luz direccional en Mixed: sombras en tiempo real + rebote horneado
        foreach (var luz in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (luz.type == LightType.Directional)
            {
                luz.lightmapBakeType = LightmapBakeType.Mixed;
                EditorUtility.SetDirty(luz);
            }

        // 5. Lighting Settings: solo GI horneada a probes, modo Baked Indirect
        LightingSettings ajustes;
        bool nuevo = false;
        // Lightmapping.lightingSettings lanza excepcion si la escena no tiene uno asignado
        try { ajustes = Lightmapping.lightingSettings; } catch (Exception) { ajustes = null; }
        if (ajustes == null)
        {
            ajustes = new LightingSettings { name = "Iluminacion " + escena.name };
            nuevo = true;
        }
        ajustes.bakedGI = true;
        ajustes.realtimeGI = false;
        ajustes.mixedBakeMode = MixedLightingMode.IndirectOnly;
        ajustes.directionalityMode = LightmapsMode.NonDirectional;
        if (nuevo)
        {
            string carpeta = System.IO.Path.GetDirectoryName(escena.path);
            AssetDatabase.CreateAsset(ajustes, AssetDatabase.GenerateUniqueAssetPath($"{carpeta}/Iluminacion {escena.name}.lighting"));
            Lightmapping.lightingSettings = ajustes;
        }
        EditorUtility.SetDirty(ajustes);

        reporte.AppendLine("• APV configurado: Probe Volume global, oclusion de cielo, luz Mixed (Baked Indirect).");
        return set;
    }

    static void MarcarEntornoEstatico(System.Text.StringBuilder reporte)
    {
        int n = 0;
        foreach (var raiz in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!RaicesEntorno.Contains(raiz.name)) continue;
            foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                flags |= StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
                         StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags);
                // Recibir GI desde probes (no hacen falta UVs de lightmap)
                r.receiveGI = ReceiveGI.LightProbes;
                n++;
            }
        }
        reporte.AppendLine($"• {n} objetos del entorno marcados como estaticos para GI.");
    }

    static void Mostrar(string titulo, string texto)
    {
        Debug.Log($"[Owl Project] {titulo}\n{texto}");
        if (!Application.isBatchMode)
            EditorUtility.DisplayDialog(titulo, texto, "OK");
    }
}
