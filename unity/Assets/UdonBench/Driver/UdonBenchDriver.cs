// Timing driver. A plain MonoBehaviour, not an Udon program. It lives in Assembly-CSharp
// (no asmdef) so that it sees VRC.Udon, and it is written in C# 7.3.
//
// In Play mode (with ClientSim, so that Udon runs), for every benchmark:
//   warm-up : each lane runs once, warmupRuns times
//   measured: measuredRounds rounds; each round runs every lane once, in an order rotated
//             by round/2 and reversed on odd rounds (two lanes: AB, BA, BA, AB, AB, ...)
//   one run : SendCustomEvent("Init") (not timed), then a Stopwatch around
//             stepsPerRun x SendCustomEvent("Step"), then SendCustomEvent("ComputeChecksum")
//             (not timed) and GetProgramVariable("checksum")
// It yields one frame between runs, so no single frame is blocked for long.
// The table goes to the `report` field, the Console, and
// <project>/Logs/UdonBench/result-<time>.txt and .json. `done` turns true at the end.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using VRC.Udon;
using Debug = UnityEngine.Debug;

public class UdonBenchDriver : MonoBehaviour
{
    [Serializable]
    public class Bench
    {
        public string name;
        [Tooltip("Step() calls per timed run")]
        public int stepsPerRun = 30;
        [Tooltip("One UdonBehaviour per lane, in the order of laneNames. Leave a slot empty to skip that lane.")]
        public UdonBehaviour[] lanes;
        [Tooltip("Expected checksum after Init + stepsPerRun x Step (from the plain .NET run). Empty = not checked.")]
        public string expected;
        [Tooltip("> 0: compare as float with this absolute tolerance")]
        public float tolerance;
    }

    public string[] laneNames = { "UdonSharp", "TsukimiSharp" };
    public Bench[] benches;
    public int warmupRuns = 3;
    public int measuredRounds = 31;
    public bool startOnPlay = true;
    [Tooltip("Frames to wait after Play starts, so every UdonBehaviour has finished its own start-up")]
    public int startDelayFrames = 30;

    // ---- results ----
    public bool running;
    public bool done;
    public string progress = "";
    [TextArea(10, 60)] public string report = "";
    public string resultPath = "";

    IEnumerator Start()
    {
        if (!startOnPlay) yield break;
        for (int i = 0; i < startDelayFrames; i++) yield return null;
        yield return RunAll();
    }

    /// <summary>Start a run by hand when startOnPlay is off.</summary>
    public void Begin()
    {
        if (!running) StartCoroutine(RunAll());
    }

    IEnumerator RunAll()
    {
        running = true;
        done = false;
        var sb = new StringBuilder();
        var json = new StringBuilder();
        var started = DateTime.Now;
        sb.AppendLine("UdonBench " + started.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                      "  warmup=" + warmupRuns + " rounds=" + measuredRounds + "  lanes=" + string.Join(",", laneNames));
        sb.AppendLine(EnvironmentLine());
        sb.AppendLine("bench | lane | steps/run | min ms | median ms | max ms | median ms/Step | Step/s (median) | checksum | expected | ok");
        json.Append("{\"started\":\"").Append(started.ToString("o", CultureInfo.InvariantCulture))
            .Append("\",\"environment\":\"").Append(EnvironmentLine().Replace("\\", "\\\\").Replace("\"", "\\\""))
            .Append("\",\"warmupRuns\":").Append(warmupRuns).Append(",\"measuredRounds\":").Append(measuredRounds)
            .Append(",\"rows\":[");
        var firstRow = true;

        foreach (var b in benches)
        {
            if (b == null || b.lanes == null) continue;
            var laneCount = Math.Min(b.lanes.Length, laneNames.Length);
            var times = new List<double>[laneCount];
            var sums = new string[laneCount];
            var sumsStable = new bool[laneCount];
            for (int l = 0; l < laneCount; l++) { times[l] = new List<double>(); sumsStable[l] = true; }

            for (int w = 0; w < warmupRuns; w++)
                for (int l = 0; l < laneCount; l++)
                {
                    if (b.lanes[l] == null) continue;
                    progress = b.name + ": warm-up " + (w + 1) + "/" + warmupRuns + " " + laneNames[l];
                    string ignored;
                    RunOnce(b.lanes[l], b.stepsPerRun, out ignored);
                    yield return null;
                }

            for (int r = 0; r < measuredRounds; r++)
            {
                // Rotate by r/2 and reverse on odd rounds: two lanes go AB, BA, BA, AB, ...;
                // three lanes walk all six orders in six rounds.
                var order = Enumerable.Range(0, laneCount).Select(i => (r / 2 + i) % laneCount).ToList();
                if (r % 2 == 1) order.Reverse();
                foreach (var l in order)
                {
                    if (b.lanes[l] == null) continue;
                    progress = b.name + ": round " + (r + 1) + "/" + measuredRounds + " " + laneNames[l];
                    string cs;
                    var ms = RunOnce(b.lanes[l], b.stepsPerRun, out cs);
                    times[l].Add(ms);
                    if (sums[l] == null) sums[l] = cs;
                    else if (sums[l] != cs) sumsStable[l] = false;
                    yield return null;
                }
            }

            for (int l = 0; l < laneCount; l++)
            {
                if (b.lanes[l] == null || times[l].Count == 0) continue;
                var t = times[l].OrderBy(x => x).ToList();
                var min = t[0];
                var max = t[t.Count - 1];
                var med = t.Count % 2 == 1 ? t[t.Count / 2] : (t[t.Count / 2 - 1] + t[t.Count / 2]) / 2.0;
                var perStep = med / b.stepsPerRun;
                var stepsPerSec = b.stepsPerRun / (med / 1000.0);
                var ok = Check(b, sums[l]) && sumsStable[l];
                sb.AppendLine(string.Join(" | ", b.name, laneNames[l], b.stepsPerRun.ToString(CultureInfo.InvariantCulture),
                    F(min), F(med), F(max), F(perStep), stepsPerSec.ToString("F1", CultureInfo.InvariantCulture),
                    sums[l] + (sumsStable[l] ? "" : " (varied!)"), string.IsNullOrEmpty(b.expected) ? "-" : b.expected,
                    ok ? "yes" : "NO"));
                if (!firstRow) json.Append(',');
                firstRow = false;
                json.Append("{\"bench\":\"").Append(b.name).Append("\",\"lane\":\"").Append(laneNames[l])
                    .Append("\",\"stepsPerRun\":").Append(b.stepsPerRun)
                    .Append(",\"msRuns\":[").Append(string.Join(",", times[l].Select(F))).Append(']')
                    .Append(",\"minMs\":").Append(F(min)).Append(",\"medianMs\":").Append(F(med))
                    .Append(",\"maxMs\":").Append(F(max)).Append(",\"medianMsPerStep\":").Append(F(perStep))
                    .Append(",\"checksum\":\"").Append(sums[l]).Append("\",\"expected\":\"").Append(b.expected)
                    .Append("\",\"ok\":").Append(ok ? "true" : "false").Append('}');
            }
            report = sb.ToString();
            yield return null;
        }

        json.Append("]}");
        sb.AppendLine("elapsed " + (DateTime.Now - started).TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s");
        report = sb.ToString();
        try
        {
            var dir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs", "UdonBench");
            Directory.CreateDirectory(dir);
            resultPath = Path.Combine(dir, "result-" + started.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json");
            File.WriteAllText(resultPath, json.ToString());
            File.WriteAllText(Path.ChangeExtension(resultPath, ".txt"), report);
        }
        catch (Exception e)
        {
            resultPath = "write failed: " + e.Message;
        }
        Debug.Log("[UdonBench]\n" + report + "\n" + resultPath);
        progress = "done";
        running = false;
        done = true;
    }

    static double RunOnce(UdonBehaviour ub, int steps, out string checksum)
    {
        ub.SendCustomEvent("Init");
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < steps; i++) ub.SendCustomEvent("Step");
        sw.Stop();
        ub.SendCustomEvent("ComputeChecksum");
        var v = ub.GetProgramVariable("checksum");
        if (v is float) checksum = ((float)v).ToString("R", CultureInfo.InvariantCulture);
        else if (v is IFormattable) checksum = ((IFormattable)v).ToString(null, CultureInfo.InvariantCulture);
        else checksum = v != null ? v.ToString() : "null";
        return sw.Elapsed.TotalMilliseconds;
    }

    static bool Check(Bench b, string got)
    {
        if (string.IsNullOrEmpty(b.expected)) return true;
        if (got == null) return false;
        if (b.tolerance > 0f)
        {
            float g, e;
            return float.TryParse(got, NumberStyles.Float, CultureInfo.InvariantCulture, out g)
                && float.TryParse(b.expected, NumberStyles.Float, CultureInfo.InvariantCulture, out e)
                && Mathf.Abs(g - e) <= b.tolerance;
        }
        return got == b.expected;
    }

    static string F(double v)
    {
        return v.ToString("F3", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// One line that says what the numbers were taken on: Unity, the two compiler packages, the machine.
    /// </summary>
    public static string EnvironmentLine()
    {
        return "Unity " + Application.unityVersion +
               "  com.vrchat.worlds " + PackageVersion("UdonSharp.UdonSharpBehaviour") +
               "  com.tsukimicode.udon-tools " + PackageVersion("Tsukimi.TsukimiBehaviour") +
               "  CPU " + SystemInfo.processorType + " (" + SystemInfo.processorCount + " threads)" +
               "  RAM " + SystemInfo.systemMemorySize + " MB" +
               "  OS " + SystemInfo.operatingSystem;
    }

    static string PackageVersion(string typeName)
    {
#if UNITY_EDITOR
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t;
            try { t = a.GetType(typeName, false); }
            catch (Exception) { continue; }
            if (t == null) continue;
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(t.Assembly);
            return info != null ? info.version : "(not from a package)";
        }
        return "(not found)";
#else
        return "(unknown)";
#endif
    }
}
