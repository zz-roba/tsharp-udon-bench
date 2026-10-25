// Runs every benchmark under plain .NET: Init, Step x K, ComputeChecksum, for several K,
// and writes the public result fields to expected.json (path = first argument,
// default expected.json next to this project).
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

static class Program
{
    // Step counts to record. Every steps-per-run value in tools/gen.py must be one of these.
    static readonly int[] Ks = { 1, 2, 3, 5, 10, 21, 30, 100 };

    static readonly string[] ResultFields = { "checksum", "population", "maxFlips", "cycles", "lastDistance" };

    static int Main(string[] args)
    {
        var outPath = args.Length > 0 ? args[0] : "expected.json";
        var types = typeof(Program).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(BenchRefBase)))
            .OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
        var sb = new StringBuilder();
        sb.Append("{\n");
        for (var ti = 0; ti < types.Count; ti++)
        {
            var t = types[ti];
            var name = t.Name.Substring(0, t.Name.Length - 1);
            sb.Append($"  \"{name}\": {{\n");
            for (var ki = 0; ki < Ks.Length; ki++)
            {
                var k = Ks[ki];
                var o = Activator.CreateInstance(t);
                Call(o, "Init");
                for (var i = 0; i < k; i++) Call(o, "Step");
                Call(o, "ComputeChecksum");
                var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => ResultFields.Contains(f.Name))
                    .OrderBy(f => f.Name, StringComparer.Ordinal)
                    .Select(f => $"\"{f.Name}\": {Fmt(f.GetValue(o))}");
                sb.Append($"    \"{k}\": {{ {string.Join(", ", fields)} }}{(ki + 1 < Ks.Length ? "," : "")}\n");
            }
            sb.Append($"  }}{(ti + 1 < types.Count ? "," : "")}\n");
            Console.WriteLine($"{name,-10} done");
        }
        sb.Append("}\n");
        File.WriteAllText(outPath, sb.ToString());
        Console.WriteLine("wrote " + Path.GetFullPath(outPath));
        return 0;
    }

    static void Call(object o, string m) =>
        o.GetType().GetMethod(m, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, null);

    static string Fmt(object v) => v switch
    {
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        IFormattable x => x.ToString(null, CultureInfo.InvariantCulture),
        _ => v?.ToString() ?? "null",
    };
}
