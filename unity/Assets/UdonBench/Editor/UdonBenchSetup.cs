// Editor menus for the benchmark (Assembly-CSharp-Editor, C# 7.3).
//
//   UdonBench/1 Create Program Assets : one UdonSharpProgramAsset and one TsukimiProgramAsset per
//                                       benchmark, next to its script, then compiles all of them.
//   UdonBench/2 Create Bench Scene    : a new scene with a VRCSceneDescriptor, a floor and the timing
//                                       driver wired to every program; saved as Assets/UdonBench/Bench.unity.
//
// Menus 1 and 2 can be run again; existing assets are reused and the scene is replaced.
// The UdonSharp and TsukimiSharp editor types are reached by reflection, so this file compiles
// whatever assemblies those packages put them in.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.Udon;

public static class UdonBenchSetup
{
    const string Root = "Assets/UdonBench";
    const string ScenePath = Root + "/Bench.unity";
    const string Tag = "[UdonBench] ";
    static readonly string[] LaneNames = { "UdonSharp", "TsukimiSharp" };

    static string ScriptPath(string bench, int lane)
    {
        return lane == 0 ? Root + "/USharp/" + bench + "U.cs" : Root + "/TSharp/" + bench + "T/" + bench + "T.cs";
    }

    static string AssetPath(string bench, int lane)
    {
        return lane == 0 ? Root + "/USharp/" + bench + "U.asset" : Root + "/TSharp/" + bench + "T/" + bench + "T.asset";
    }

    static Type FindType(string fullName)
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t;
            try { t = a.GetType(fullName, false); }
            catch (Exception) { continue; }
            if (t != null) return t;
        }
        return null;
    }

    // ------------------------------------------------------------------ 1

    [MenuItem("UdonBench/1 Create Program Assets", false, 1)]
    public static void CreateProgramAssets()
    {
        var usharpAssetType = FindType("UdonSharp.UdonSharpProgramAsset");
        var tsharpAssetType = FindType("TsukimiCode.UdonTools.Editor.TsukimiProgramAsset");
        if (usharpAssetType == null || tsharpAssetType == null)
        {
            Debug.LogError(Tag + "UdonSharpProgramAsset or TsukimiProgramAsset was not found. " +
                           "Are com.vrchat.worlds and com.tsukimicode.udon-tools installed?");
            return;
        }

        var compile = tsharpAssetType.GetMethod("CompileFromSource", Type.EmptyTypes);
        var failed = new List<string>();
        foreach (var row in UdonBenchExpected.Rows)
        {
            MakeProgramAsset(usharpAssetType, ScriptPath(row.Name, 0), AssetPath(row.Name, 0));
            var t = MakeProgramAsset(tsharpAssetType, ScriptPath(row.Name, 1), AssetPath(row.Name, 1));
            if (t == null) { failed.Add(row.Name + "T"); continue; }
            var ok = compile != null ? compile.Invoke(t, null) : null;
            if (!(ok is bool) || !(bool)ok) failed.Add(row.Name + "T");
        }
        AssetDatabase.SaveAssets();

        // UdonSharp 1.x compiles every program at once.
        var compileAll = usharpAssetType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "CompileAllCsPrograms");
        if (compileAll != null)
        {
            var args = compileAll.GetParameters()
                .Select(p => p.ParameterType == typeof(bool) ? (object)true : p.DefaultValue).ToArray();
            compileAll.Invoke(null, args);
        }
        else
        {
            Debug.LogWarning(Tag + "UdonSharpProgramAsset.CompileAllCsPrograms was not found; " +
                             "compile the UdonSharp programs from the UdonSharp menu.");
        }

        if (failed.Count == 0)
            Debug.Log(Tag + UdonBenchExpected.Rows.Length * 2 + " program assets created and compiled. " +
                      "Check the Console for UdonSharp compile errors, then run UdonBench/2.");
        else
            Debug.LogError(Tag + "TsukimiSharp compile failed for: " + string.Join(", ", failed.ToArray()));
    }

    static ScriptableObject MakeProgramAsset(Type assetType, string csPath, string assetPath)
    {
        var mono = AssetDatabase.LoadAssetAtPath<MonoScript>(csPath);
        if (mono == null)
        {
            Debug.LogError(Tag + "script not found: " + csPath);
            return null;
        }
        var field = assetType.GetField("sourceCsScript", BindingFlags.Public | BindingFlags.Instance);
        var asset = AssetDatabase.LoadAssetAtPath(assetPath, assetType) as ScriptableObject;
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance(assetType);
            if (field != null) field.SetValue(asset, mono);
            AssetDatabase.CreateAsset(asset, assetPath);
        }
        else
        {
            if (field != null) field.SetValue(asset, mono);
            EditorUtility.SetDirty(asset);
        }
        return asset;
    }

    // ------------------------------------------------------------------ 2

    [MenuItem("UdonBench/2 Create Bench Scene", false, 2)]
    public static void CreateBenchScene()
    {
        // Every program asset must exist before the scene refers to it.
        var missing = new List<string>();
        foreach (var row in UdonBenchExpected.Rows)
            for (int lane = 0; lane < LaneNames.Length; lane++)
                if (AssetDatabase.LoadAssetAtPath<AbstractUdonProgramSource>(AssetPath(row.Name, lane)) == null)
                    missing.Add(AssetPath(row.Name, lane));
        if (missing.Count > 0)
        {
            Debug.LogError(Tag + "program assets are missing (run UdonBench/1 first):\n" + string.Join("\n", missing.ToArray()));
            return;
        }
        var descriptorType = FindType("VRC.SDK3.Components.VRCSceneDescriptor");
        if (descriptorType == null)
        {
            Debug.LogError(Tag + "VRCSceneDescriptor was not found. Is com.vrchat.worlds installed?");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        var spawn = new GameObject("Spawn");
        var world = new GameObject("VRCWorld");
        var descriptor = world.AddComponent(descriptorType);
        var spawns = descriptorType.GetField("spawns", BindingFlags.Public | BindingFlags.Instance);
        if (spawns != null) spawns.SetValue(descriptor, new[] { spawn.transform });
        else Debug.LogWarning(Tag + "VRCSceneDescriptor.spawns was not found; set a spawn point by hand.");

        var root = new GameObject("UdonBench");
        var driver = root.AddComponent<UdonBenchDriver>();
        driver.laneNames = (string[])LaneNames.Clone();
        driver.warmupRuns = 3;
        driver.measuredRounds = 31;
        var benches = new List<UdonBenchDriver.Bench>();
        foreach (var row in UdonBenchExpected.Rows)
        {
            var lanes = new UdonBehaviour[LaneNames.Length];
            for (int lane = 0; lane < LaneNames.Length; lane++)
                lanes[lane] = MakeLane(root, row.Name + (lane == 0 ? "U" : "T"), AssetPath(row.Name, lane));
            benches.Add(new UdonBenchDriver.Bench
            {
                name = row.Name,
                stepsPerRun = row.Steps,
                expected = row.Expected,
                tolerance = row.Tolerance,
                lanes = lanes,
            });
        }
        driver.benches = benches.ToArray();

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            Debug.LogError(Tag + "could not save " + ScenePath);
            return;
        }
        Selection.activeGameObject = root;
        Debug.Log(Tag + "saved " + ScenePath + " with " + benches.Count + " benchmarks. " +
                  "Enter Play with ClientSim enabled; the run starts by itself after " + driver.startDelayFrames + " frames.");
    }

    static UdonBehaviour MakeLane(GameObject root, string name, string assetPath)
    {
        var src = AssetDatabase.LoadAssetAtPath<AbstractUdonProgramSource>(assetPath);
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var ub = go.AddComponent<UdonBehaviour>();
        ub.programSource = src;
        return ub;
    }
}
