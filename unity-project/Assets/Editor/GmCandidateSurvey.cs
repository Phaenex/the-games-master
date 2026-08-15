// Measures the three purchased candidate scenes against the things that actually decide which one is
// cheapest to build the prologue in. Read-only; opens each scene, counts, and never saves.
//
// The prologue is a ROAD: drive up, gate, walk past buildings, reach the mansion. So the question is
// not "which is prettiest" but:
//   * does it already have a road and buildings arranged along one, or would that have to be carved;
//   * does it ship PRACTICAL lights (lamps, lanterns, fires)? Those are motivated light sources
//     placed by the pack's own artist, and they are exactly what the current build was missing;
//   * how dark does it ship, i.e. how much relighting risk is being taken on;
//   * how big is the scene to open and save, since every edit pays that cost.
//
// Written because the last environment choice was made from a handful of stills and cost days.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GmCandidateSurvey
{
    const string LogTag = "GmCandidates";

    static readonly (string label, string path)[] Candidates =
    {
        ("haunted-village",   "Assets/LeartesStudios/HauntedVillage/Scene/Showcase.unity"),
        ("abandoned-village", "Assets/LeartesStudios/Abandoned Village/HDRP/Scene/HDRP_Abandoned_Village.unity"),
        ("witch-village",     "Assets/LeartesStudios/WitchVillage/HDRP/Scene/HDRP_WitchVillage.unity"),
    };

    static readonly Regex BuildingPattern =
        new Regex(@"House|Church|Barn|Shed|Hut|Cabin|Chapel|Tower|Mill|Building", RegexOptions.IgnoreCase);
    static readonly Regex RoadPattern =
        new Regex(@"Road|Path|Street|Track|Lane|Trail|Cobble", RegexOptions.IgnoreCase);
    static readonly Regex LampPattern =
        new Regex(@"Lamp|Lantern|Torch|Brazier|Candle|Fire|LightPole", RegexOptions.IgnoreCase);
    static readonly Regex LodSuffix = new Regex(@"_LOD[1-9]$");

    public static string ReportPath =>
        Path.Combine(Directory.GetCurrentDirectory(), "Screens", "Review", "candidate-survey.txt");

    [MenuItem("GamesMaster/Restart/Survey Candidate Scenes")]
    public static void Run()
    {
        var report = new StringBuilder();
        report.AppendLine("PURCHASED SCENE COMPARISON (read-only, nothing modified)");
        report.AppendLine();

        foreach ((string label, string path) in Candidates)
        {
            if (!File.Exists(path))
            {
                report.AppendLine($"{label}: MISSING at {path}");
                continue;
            }

            long bytes = new FileInfo(path).Length;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            Renderer[] all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                .Where(r => r.gameObject.activeInHierarchy && r.GetComponent<Terrain>() == null)
                .ToArray();
            Renderer[] unique = all.Where(r => !LodSuffix.IsMatch(r.name)).ToArray();

            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            Light[] practicals = lights.Where(l => l.type != LightType.Directional).ToArray();
            Light[] directionals = lights.Where(l => l.type == LightType.Directional).ToArray();

            int buildings = unique.Count(r => BuildingPattern.IsMatch(r.name));
            int roadBits = unique.Count(r => RoadPattern.IsMatch(r.name));
            int lampProps = unique.Count(r => LampPattern.IsMatch(r.name));

            var terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();

            Bounds content = all.Length > 0 ? all[0].bounds : new Bounds();
            foreach (Renderer r in all) content.Encapsulate(r.bounds);

            report.AppendLine($"=== {label} ===");
            report.AppendLine($"  scene file        {bytes / 1024 / 1024.0:0.0} MB   (edit/save cost)");
            report.AppendLine($"  renderers         {all.Length} ({unique.Length} excluding LOD copies)");
            report.AppendLine($"  content extent    {content.size.x:0} x {content.size.z:0} m");
            report.AppendLine($"  terrain           {(terrain != null ? "yes" : "NO")}");
            report.AppendLine($"  buildings         {buildings}");
            report.AppendLine($"  road/path meshes  {roadBits}");
            report.AppendLine($"  lamp/fire props   {lampProps}");
            report.AppendLine($"  directional lights {directionals.Length}");
            report.AppendLine($"  PRACTICAL lights  {practicals.Length}   <- motivated sources already placed");
            if (practicals.Length > 0)
            {
                var byType = practicals.GroupBy(l => l.type)
                    .Select(g => $"{g.Count()}x{g.Key}");
                report.AppendLine($"    types           {string.Join(", ", byType)}");
                report.AppendLine($"    intensity range {practicals.Min(l => l.intensity):0.#} .. {practicals.Max(l => l.intensity):0.#}");
            }

            // Name the most common non-LOD props: a quick read on what the pack is actually made of.
            var vocab = unique.GroupBy(r => Regex.Replace(r.name, @"_?\d+$", ""))
                .OrderByDescending(g => g.Count()).Take(6)
                .Select(g => $"{g.Key}({g.Count()})");
            report.AppendLine($"  common props      {string.Join(", ", vocab)}");
            report.AppendLine();

            Debug.Log($"[{LogTag}] {label}: renderers={all.Length} buildings={buildings} " +
                      $"roads={roadBits} lamps={lampProps} practicals={practicals.Length} " +
                      $"directional={directionals.Length} size={bytes / 1024 / 1024.0:0.0}MB");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log($"[{LogTag}] wrote {ReportPath}");
        EditorApplication.Exit(0);
    }
}

