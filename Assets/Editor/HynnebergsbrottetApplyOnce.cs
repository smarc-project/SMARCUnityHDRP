using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Askö-style Hynnebergsbrottet: Unity Terrain lakebed + MeshLab BPA lattice.
/// Heightmap is the class-0 RAW (no punched hole). Tower is loading_tower.bin
/// (Unity Y-up, local verts, GameObject at the surveyed lattice centroid).
/// Batch: Unity -batchmode -nographics -quit -projectPath SMARCUnity
///        -executeMethod HynnebergsbrottetApplyOnce.ApplyAndSave
/// </summary>
[InitializeOnLoad]
public static class HynnebergsbrottetApplyOnce
{
    const string ScenePath = "Assets/Scenes/Hynnebergsbrottet.unity";
    const string BakeDst = "Assets/Scenes/Hynnebergsbrottet";
    const string TerrainAsset = BakeDst + "/Terrain/HynnebergsbrottetBathymetry.asset";
    const string Stamp = BakeDst + "/Terrain/heights_applied.txt";
    const string TowerBin = BakeDst + "/Objects/loading_tower.bin";
    const string TowerJson = BakeDst + "/Objects/loading_tower.json";
    const string TowerMeshAsset = BakeDst + "/Objects/loading_tower_mesh.asset";
    const string TowerStamp = BakeDst + "/Objects/tower_applied.txt";
    const int MinTowerVerts = 8;
    const int MaxTowerVerts = 200000;
    const int FullPoissonVerts = 6289893;

    static HynnebergsbrottetApplyOnce()
    {
        EditorApplication.delayCall += ApplyIfNeeded;
        EditorSceneManager.sceneOpened += (_, __) => ApplyIfNeeded();
    }

    public static void ApplyAndSave()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Apply();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("HynnebergsbrottetApplyOnce.ApplyAndSave: scene saved.");
    }

    static void ApplyIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        var rawPath = Path.Combine(BakeDst, "Terrain", "hynnebergsbrottet_heightmap.raw");
        if (File.Exists(rawPath)
            && (!File.Exists(Stamp)
                || File.GetLastWriteTimeUtc(Stamp) < File.GetLastWriteTimeUtc(rawPath)))
        {
            ApplyTerrain();
            File.WriteAllText(Stamp, DateTime.UtcNow.ToString("o"));
        }
        if (!File.Exists(TowerBin))
            return;
        var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(TowerMeshAsset);
        bool stale = !File.Exists(TowerStamp)
            || File.GetLastWriteTimeUtc(TowerStamp) < File.GetLastWriteTimeUtc(TowerBin)
            || existingMesh == null
            || existingMesh.vertexCount < 100
            || existingMesh.vertexCount == FullPoissonVerts;
        if (stale)
            ApplyTower();
    }

    public static void Apply()
    {
        ApplyTerrain();
        ApplyTower();
    }

    static void ApplyTower()
    {
        if (!File.Exists(TowerBin) || !File.Exists(TowerJson))
        {
            Debug.LogError("HynnebergsbrottetApplyOnce: missing loading_tower.bin/json");
            return;
        }

        var meta = JsonUtility.FromJson<TowerMeta>(File.ReadAllText(TowerJson));
        var mesh = ReadTowerBin(TowerBin);
        if (mesh == null)
            return;

        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(TowerMeshAsset);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, TowerMeshAsset);
            existing = mesh;
        }
        else
        {
            existing.Clear();
            existing.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.triangles = mesh.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);
        }
        AssetDatabase.SaveAssets();

        var pos = new Vector3(meta.unity_x, meta.unity_y, meta.unity_z);
        int assigned = 0;
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || (t.name != "loading_tower" && t.name != "survey_mesh"))
                continue;
            t.name = "loading_tower";
            t.localPosition = pos;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.smarc.assets/Runtime/Materials/Concrete-ish.mat");
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
            {
                mf.sharedMesh = existing;
                assigned++;
                EditorUtility.SetDirty(mf);
            }
            foreach (var mr in t.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mat != null)
                    mr.sharedMaterial = mat;
                EditorUtility.SetDirty(mr);
            }
            foreach (var col in t.GetComponentsInChildren<MeshCollider>(true))
            {
                col.sharedMesh = existing;
                col.enabled = true;
                EditorUtility.SetDirty(col);
            }
            EditorUtility.SetDirty(t);
            var scene = t.gameObject.scene;
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                $"Hynnebergsbrottet: {t.name} at ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})  "
                + $"mesh {existing.vertexCount} verts / {existing.triangles.Length / 3} tris  bounds {existing.bounds}");
        }
        if (assigned == 0)
        {
            Debug.LogWarning(
                "HynnebergsbrottetApplyOnce: loading_tower not in a loaded scene; mesh asset written.");
            return;
        }

        foreach (var terrain in UnityEngine.Object.FindObjectsByType<Terrain>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (terrain == null || terrain.name != "HynnebergsbrottetTerrain")
                continue;
            terrain.drawHeightmap = true;
            EditorUtility.SetDirty(terrain);
        }

        File.WriteAllText(TowerStamp, DateTime.UtcNow.ToString("o"));
    }

    static Mesh ReadTowerBin(string path)
    {
        var bytes = File.ReadAllBytes(path);
        int o = 0;
        uint nv = BitConverter.ToUInt32(bytes, o); o += 4;
        uint nt = BitConverter.ToUInt32(bytes, o); o += 4;
        o += 12;
        if (nv < MinTowerVerts || nv > MaxTowerVerts || nt == 0 || nt > MaxTowerVerts * 4
            || nv == FullPoissonVerts)
        {
            Debug.LogError(
                $"HynnebergsbrottetApplyOnce: refusing bin nv={nv} nt={nt}");
            return null;
        }
        int nFloats = (int)nv * 3;
        var vf = new float[nFloats];
        var nf = new float[nFloats];
        Buffer.BlockCopy(bytes, o, vf, 0, nFloats * 4); o += nFloats * 4;
        Buffer.BlockCopy(bytes, o, nf, 0, nFloats * 4); o += nFloats * 4;
        var verts = new Vector3[nv];
        var nrm = new Vector3[nv];
        for (int i = 0, k = 0; i < nv; i++, k += 3)
        {
            verts[i] = new Vector3(vf[k], vf[k + 1], vf[k + 2]);
            nrm[i] = new Vector3(nf[k], nf[k + 1], nf[k + 2]);
        }
        var idx = new int[nt * 3];
        Buffer.BlockCopy(bytes, o, idx, 0, (int)nt * 12);
        var mesh = new Mesh { name = "loading_tower" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.triangles = idx;
        mesh.normals = nrm;
        mesh.RecalculateBounds();
        return mesh;
    }

    static void ApplyTerrain()
    {
        var json = File.ReadAllText(Path.Combine(BakeDst, "Terrain", "unity_import.json"));
        var bake = JsonUtility.FromJson<Bake>(json);

        var heights = ReadRaw16(
            Path.Combine(BakeDst, "Terrain", "hynnebergsbrottet_heightmap.raw"),
            bake.heightmapResolution);
        var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainAsset);
        if (td == null)
        {
            Debug.LogWarning("HynnebergsbrottetApplyOnce: no TerrainData yet.");
            return;
        }
        td.heightmapResolution = bake.heightmapResolution;
        td.size = new Vector3(bake.widthMeters, bake.heightMeters, bake.lengthMeters);
        td.SetHeights(0, 0, heights);
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
        Debug.Log("Hynnebergsbrottet: TerrainData filled from cleaned UTM RAW.");
    }

    [Serializable]
    class Bake
    {
        public int heightmapResolution;
        public float widthMeters, lengthMeters, heightMeters;
    }

    [Serializable]
    class TowerMeta
    {
        public float unity_x, unity_y, unity_z;
    }

    static float[,] ReadRaw16(string path, int res)
    {
        var bytes = File.ReadAllBytes(path);
        var heights = new float[res, res];
        int i = 0;
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                ushort v = (ushort)(bytes[i] | (bytes[i + 1] << 8));
                i += 2;
                heights[z, x] = v / 65535f;
            }
        }
        return heights;
    }
}