using System.Reflection;
using System.Text;
using AdamsHaven.Tower;
using UnityEditor;
using UnityEngine;

// Editor-only performance probe: times the Tower simulation on a checkpoint tower, per frame and per tick subsystem.
// Run from Tools > Adams Haven > Tower Perf Probe, or call TowerPerfProbe.Run() (returns the report).
public static class TowerPerfProbe
{
    private static readonly string[] Subsystems = {
        "TickConstruction", "TickNeeds", "FlushDeaths", "TickLife", "TickFamilies", "TickExploration", "TickPower",
        "TickSocial", "TickSteward", "PlanJobs", "TickTravel", "TickWork", "TickRooms", "TickVisitors", "TickIncidents",
        "TickEvents", "CheckHeartStage", "TickDistricts", "TickOutposts", "TickSiege"
    };

    [MenuItem("Tools/Adams Haven/Tower Perf Probe")]
    private static void Menu() { Debug.Log(Run()); }

    public static string Run(int checkpoint = 10, int frames = 300)
    {
        var report = new StringBuilder();
        var rules = new TowerRules(TowerMilestones.Create(checkpoint));
        rules.Advance(5, true);
        report.AppendLine("checkpoint " + checkpoint + ": " + rules.State.residents.Count + " residents, " + rules.State.rooms.Count + " rooms");
        foreach (float speed in new[] { 1f, 8f })
        {
            double t0 = EditorApplication.timeSinceStartup;
            for (int i = 0; i < frames; i++) rules.Advance(TowerRules.FrameSeconds(1f / 60f, speed), true);
            report.AppendLine("  Advance at " + speed + "x: " + Ms(t0, frames) + " ms/frame");
        }
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        const float dt = 1f / 60f;
        foreach (string name in Subsystems)
        {
            var method = typeof(TowerRules).GetMethod(name, flags);
            if (method == null) { report.AppendLine("  " + name + ": missing"); continue; }
            int count = method.GetParameters().Length;
            object[] args = count == 0 ? new object[0] : count == 1 ? new object[] { dt } : new object[] { dt, true };
            double t0 = EditorApplication.timeSinceStartup;
            for (int i = 0; i < frames; i++) method.Invoke(rules, args);
            report.AppendLine("  " + name + ": " + Ms(t0, frames) + " ms");
        }
        return report.ToString();
    }

    private static string Ms(double start, int frames)
    { return ((EditorApplication.timeSinceStartup - start) * 1000 / frames).ToString("0.000"); }
}
