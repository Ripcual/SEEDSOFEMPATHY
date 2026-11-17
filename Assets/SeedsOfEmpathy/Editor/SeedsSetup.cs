using System.Linq;
using HappyHarvest;
using SeedsOfEmpathy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú Seeds of Empathy > Preparar prueba de crisis:
/// 1) ajusta el GameManager.prefab (día de 480 s, empieza a las 02:24)
/// 2) crea un CrisisSite con WorkPoint y HomePoint en la escena abierta (en el centro de la vista de escena).
/// </summary>
public static class SeedsSetup
{
    [MenuItem("Seeds of Empathy/Preparar prueba de crisis")]
    public static void Setup()
    {
        // --- GameManager.prefab ---
        var guid = AssetDatabase.FindAssets("GameManager t:Prefab")
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => p.EndsWith("/Resources/GameManager.prefab"));

        if (guid == null)
        {
            Debug.LogError("No encuentro Resources/GameManager.prefab");
        }
        else
        {
            var root = PrefabUtility.LoadPrefabContents(guid);
            var gm = root.GetComponent<GameManager>();
            gm.DayDurationInSeconds = 480f;
            gm.StartingTime = 48f; // 02:24
            PrefabUtility.SaveAsPrefabAsset(root, guid);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("GameManager.prefab: DayDurationInSeconds=480, StartingTime=48");
        }

        // --- CrisisSite ---
        if (Object.FindFirstObjectByType<CrisisSite>() != null)
        {
            Debug.Log("Ya hay un CrisisSite en la escena; no creo otro.");
            return;
        }

        Vector3 pos = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        pos.z = 0f;

        var go = new GameObject("CrisisSite");
        Undo.RegisterCreatedObjectUndo(go, "Crear CrisisSite");
        go.transform.position = pos;

        var work = new GameObject("WorkPoint");
        work.transform.SetParent(go.transform, false);

        var home = new GameObject("HomePoint");
        home.transform.SetParent(go.transform, false);
        home.transform.localPosition = new Vector3(-5f, 0f, 0f);

        var site = go.AddComponent<CrisisSite>();
        site.WorkPoint = work.transform;
        site.HomePoint = home.transform;

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeObject = go;
        Debug.Log("CrisisSite creado. Muévelo sobre el huerto y coloca HomePoint en la puerta del taller.");
    }
}
