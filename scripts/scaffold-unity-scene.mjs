#!/usr/bin/env node
// Creates the repeatable source pack for a new Unity scene. Dry-run is the default. --write adds
// the source pack below unity/scenes/<id>/ and registers its build, audit and review-tour commands.
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import {
  loadSceneRegistry,
  REGISTRY_PATH,
  validateSceneRegistry,
} from './unity-scene-registry.mjs';

export const REPO_ROOT = path.dirname(path.dirname(fileURLToPath(import.meta.url)));

function pascalFromId(id) {
  return id.split('-').map((part) => part[0].toUpperCase() + part.slice(1)).join('');
}

function titleFromId(id) {
  return id.split('-').map((part) => part[0].toUpperCase() + part.slice(1)).join(' ');
}

function csharp(value) {
  return value.replaceAll('\\', '\\\\').replaceAll('"', '\\"');
}

export function createSceneSpec(options) {
  const id = options.id;
  if (typeof id !== 'string' || !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(id)) {
    throw new Error("--id must use lowercase kebab-case, for example 'entry-hall'");
  }
  const stem = pascalFromId(id);
  const className = options.className ?? `Gm${stem}`;
  const sceneName = options.sceneName ?? stem;
  const displayName = options.displayName ?? titleFromId(id);
  const phase = Number(options.phase);
  const shots = Number(options.shots ?? 5);
  if (!/^Gm[A-Z][A-Za-z0-9]*$/.test(className)) {
    throw new Error("--class must be a C# type prefix beginning with 'Gm', for example GmEntryHall");
  }
  if (!/^[A-Za-z][A-Za-z0-9_]*$/.test(sceneName)) {
    throw new Error('--scene-name must be a valid Unity scene name');
  }
  if (typeof displayName !== 'string' || displayName.trim().length < 2 || displayName !== displayName.trim()) {
    throw new Error('--name must be a trimmed display name');
  }
  if (!Number.isInteger(phase) || phase < 0 || phase > 10) {
    throw new Error('--phase must be an integer from 0 to 10');
  }
  if (!Number.isInteger(shots) || shots < 3 || shots > 24) {
    throw new Error('--shots must be an integer from 3 to 24');
  }
  return { id, stem, className, sceneName, displayName, phase, shots };
}

function builderTemplate(spec) {
  const { className: c, id, displayName, sceneName } = spec;
  return `// Generated scene entry point. Keep construction deterministic and split large passes into\n` +
`// named methods. Do not hand-edit the generated .unity file as the source of truth.\n` +
`using UnityEditor;\nusing UnityEngine;\nusing UnityEngine.Rendering.HighDefinition;\nusing UnityEngine.SceneManagement;\n\n` +
`public static class ${c}Builder\n{\n` +
`    public const string SceneId = "${csharp(id)}";\n` +
`    public const string DisplayName = "${csharp(displayName)}";\n` +
`    public const string ScenePath = "Assets/Scenes/${csharp(sceneName)}.unity";\n\n` +
`    [MenuItem("GamesMaster/Scenes/Rebuild ${csharp(displayName)}")]\n` +
`    public static void Build()\n    {\n` +
`        Scene scene = GmSceneBuildUtility.CreateEmptyScene();\n` +
`        var systems = new GameObject("SceneSystems");\n` +
`        GmSceneBuildUtility.MarkScene(systems, SceneId, DisplayName);\n` +
`        new GameObject("Environment");\n` +
`        new GameObject("Gameplay");\n` +
`        new GameObject("Lighting");\n\n` +
`        var composition = new GameObject("Composition");\n` +
`        ${c}CompositionPlan.Author(composition);\n\n` +
`        var cameraRig = new GameObject("ReviewCamera");\n` +
`        cameraRig.transform.position = new Vector3(0f, 1.7f, -6f);\n` +
`        cameraRig.AddComponent<Camera>();\n` +
`        cameraRig.AddComponent<HDAdditionalCameraData>();\n` +
`        cameraRig.AddComponent<AudioListener>();\n` +
`        cameraRig.tag = "MainCamera";\n` +
`        systems.AddComponent<${c}ShotTour>();\n\n` +
`        // Add architecture, lighting, interaction and authored content above this line.\n` +
`        GmSceneBuildUtility.SaveScene(scene, ScenePath);\n` +
`        Debug.Log("[${c}] BUILD PASS: " + ScenePath);\n` +
`    }\n}\n`;
}

function compositionTemplate(spec) {
  const { className: c, displayName, id } = spec;
  return `// This is the visual design contract, not a prop-scatter recipe. Name every zone, cluster,\n` +
`// relationship, reserved route, negative-space volume, motivated light and screenshot claim.\n` +
`using UnityEngine;\n\n` +
`public static class ${c}CompositionPlan\n{\n` +
`    public static void Author(GameObject owner)\n    {\n` +
`        GmCompositionAuthoring.Begin(owner, "${csharp(id)}",\n` +
`            "replace-me: describe the visual hierarchy and emotional job of ${csharp(displayName)}",\n` +
`            minZones: 1, minClusters: 1, minElements: 4);\n\n` +
`        // RED BY DESIGN. Add authored GmCompositionZone and GmCompositionCluster marker roots.\n` +
`        // Mark visible objects with GmCompositionElement, reserve traversal and negative space,\n` +
`        // motivate every local light, and add one GmReviewCompositionClaim per tour shot.\n` +
`    }\n}\n`;
}

function auditTemplate(spec) {
  const { className: c, displayName } = spec;
  return `// Structural gate for ${csharp(displayName)}. Add room-specific, objective checks here; visual\n` +
`// intent and spatial relationships use the shared composition audit; taste remains visual/human.\n` +
`using System.Collections.Generic;\nusing UnityEditor;\nusing UnityEngine;\n\n` +
`public static class ${c}QualityAudit\n{\n` +
`    static readonly string[] RequiredRoots =\n` +
`    {\n        "SceneSystems", "Environment", "Gameplay", "Lighting", "Composition", "ReviewCamera"\n    };\n\n` +
`    [MenuItem("GamesMaster/Scenes/Audit ${csharp(displayName)}")]\n` +
`    public static void Run()\n    {\n` +
`        ${c}Builder.Build();\n` +
`        List<string> issues = ValidateOpenScene();\n` +
`        if (issues.Count > 0)\n        {\n` +
`            foreach (string issue in issues) Debug.LogError("[${c}Audit] FAILED: " + issue);\n` +
`            return;\n        }\n` +
`        Debug.Log("[${c}Audit] PASS: scene contract and required roots verified");\n` +
`    }\n\n` +
`    public static List<string> ValidateOpenScene()\n    {\n` +
`        var issues = GmSceneContractAudit.ValidateOpenScene(\n` +
`            ${c}Builder.SceneId, ${c}Builder.DisplayName, ${c}Builder.ScenePath, RequiredRoots);\n` +
`        issues.AddRange(GmSceneCompositionAudit.ValidateOpenScene(\n` +
`            ${c}Builder.SceneId, Object.FindAnyObjectByType<${c}ShotTour>(), Camera.main));\n` +
`        return issues;\n` +
`    }\n}\n`;
}

function tourTemplate(spec) {
  const { className: c, displayName, sceneName } = spec;
  const shots = [];
  for (let i = 1; i <= spec.shots; i++) {
    const n = String(i).padStart(2, '0');
    shots.push(`        new GmReviewShot("${n}-replace-me", new Vector3(0f, 1.7f, ${-6 + i * 2}f), 0f, 0f)`);
  }
  return `// Replace every generated waypoint with a deliberate composition before visual review.\n` +
`using System.Collections.Generic;\nusing UnityEngine;\n#if UNITY_EDITOR\nusing UnityEditor;\n#endif\n\n` +
`public sealed class ${c}ShotTour : GmSceneReviewTour\n{\n` +
`    static readonly GmReviewShot[] Shots =\n    {\n${shots.join(',\n')}\n    };\n\n` +
`    protected override IReadOnlyList<GmReviewShot> ReviewShots => Shots;\n` +
`}\n\n#if UNITY_EDITOR\n` +
`public static class ${c}ShotTourMenu\n{\n` +
`    [MenuItem("GamesMaster/Scenes/Review Tour ${csharp(displayName)}")]\n` +
`    public static void ArmAndPlay()\n    {\n` +
`        GmSceneReviewTourMenu.ArmAndPlay<${c}ShotTour>("Assets/Scenes/${csharp(sceneName)}.unity");\n` +
`    }\n}\n#endif\n`;
}

function testsTemplate(spec) {
  const { className: c, displayName } = spec;
  return `// Generated minimum gates for ${csharp(displayName)}. Add tests for every regression found.\n` +
`using NUnit.Framework;\nusing UnityEngine;\n\n` +
`public class ${c}BuildTests\n{\n` +
`    [OneTimeSetUp]\n    public void BuildOnce() => ${c}Builder.Build();\n\n` +
`    [Test]\n    public void SceneContractPasses()\n    {\n` +
`        var issues = ${c}QualityAudit.ValidateOpenScene();\n` +
`        Assert.IsEmpty(issues, "${csharp(displayName)} scene contract failed:\\n- " + string.Join("\\n- ", issues));\n` +
`    }\n\n` +
`    [Test]\n    public void ReviewTourHasNoPlaceholderShotNames()\n    {\n` +
`        var tour = Object.FindAnyObjectByType<${c}ShotTour>();\n` +
`        Assert.IsNotNull(tour, "review tour component is missing");\n` +
`        Assert.IsFalse(tour.HasPlaceholderShots,\n` +
`            "replace every generated '*-replace-me' waypoint before this scene can pass");\n` +
`    }\n\n` +
`    [Test]\n    public void AuthoredCompositionContractPasses()\n    {\n` +
`        var issues = GmSceneCompositionAudit.ValidateOpenScene(\n` +
`            ${c}Builder.SceneId, Object.FindAnyObjectByType<${c}ShotTour>(), Camera.main);\n` +
`        Assert.IsEmpty(issues, "${csharp(displayName)} composition failed:\\n- " + string.Join("\\n- ", issues));\n` +
`    }\n}\n`;
}

function readmeTemplate(spec) {
  return `# ${spec.displayName} Unity scene\n\n` +
`Generated scaffold for Phase ${spec.phase}. The C# files in this directory are the source of truth.\n\n` +
`1. Run \`npm run unity:scene:sync\` to copy sources into \`./unity-project\`.\n` +
`2. Implement deterministic construction in \`Editor/${spec.className}Builder.cs\`.\n` +
`3. Author scene grammar in \`Editor/${spec.className}CompositionPlan.cs\`; the placeholder keeps tests red.\n` +
`4. Replace all \`*-replace-me\` review shots in \`Runtime/${spec.className}ShotTour.cs\`.\n` +
`5. Add objective room checks to \`Editor/${spec.className}QualityAudit.cs\`.\n` +
`6. Run \`node scripts/unity-cli.mjs rebuild ${spec.id}\`, then \`audit ${spec.id}\`.\n` +
`7. Run Unity EditMode tests, PlayMode route tests where relevant, and \`tour ${spec.id}\`.\n` +
`8. Inspect every screenshot at full size and complete the human walk before approval.\n\n` +
`The scaffold is intentionally incomplete. A successful compile is not a visual or gameplay pass.\n`;
}

export function renderSceneFiles(spec) {
  const c = spec.className;
  return new Map([
    [`Editor/${c}Builder.cs`, builderTemplate(spec)],
    [`Editor/${c}CompositionPlan.cs`, compositionTemplate(spec)],
    [`Editor/${c}QualityAudit.cs`, auditTemplate(spec)],
    [`Runtime/${c}ShotTour.cs`, tourTemplate(spec)],
    [`Tests/${c}BuildTests.cs`, testsTemplate(spec)],
    ['README.md', readmeTemplate(spec)],
  ]);
}

export function registryEntry(spec) {
  const c = spec.className;
  return {
    id: spec.id,
    displayName: spec.displayName,
    phase: spec.phase,
    status: 'scaffold',
    sceneName: spec.sceneName,
    scenePath: `Assets/Scenes/${spec.sceneName}.unity`,
    sourceDir: `unity/scenes/${spec.id}`,
    build: { method: `${c}Builder.Build`, success: `[${c}] BUILD PASS` },
    audit: { method: `${c}QualityAudit.Run`, success: `[${c}Audit] PASS` },
    tour: {
      method: `${c}ShotTourMenu.ArmAndPlay`,
      success: '[GmSceneReviewTour] TOUR COMPLETE',
      logTag: 'GmSceneReviewTour',
      shots: spec.shots,
    },
  };
}

export function scaffoldScene(spec, {
  write = false,
  repoRoot = REPO_ROOT,
  registryPath = REGISTRY_PATH,
} = {}) {
  const registry = loadSceneRegistry(registryPath);
  const entry = registryEntry(spec);
  if (registry.scenes.some((scene) => scene.id === spec.id)) throw new Error(`scene '${spec.id}' is already registered`);
  if (registry.scenes.some((scene) => scene.sceneName === spec.sceneName)) {
    throw new Error(`Unity scene name '${spec.sceneName}' is already registered`);
  }
  const sourceRoot = path.join(repoRoot, entry.sourceDir);
  const files = renderSceneFiles(spec);
  for (const relative of files.keys()) {
    if (existsSync(path.join(sourceRoot, relative))) throw new Error(`refusing to overwrite ${path.join(sourceRoot, relative)}`);
  }

  const nextRegistry = { ...registry, scenes: [...registry.scenes, entry] };
  validateSceneRegistry(nextRegistry);
  if (write) {
    for (const [relative, content] of files) {
      const target = path.join(sourceRoot, relative);
      mkdirSync(path.dirname(target), { recursive: true });
      writeFileSync(target, content, 'utf8');
    }
    writeFileSync(registryPath, `${JSON.stringify(nextRegistry, null, 2)}\n`, 'utf8');
  }
  return { entry, files, sourceRoot, wrote: write };
}

function parseArgs(argv) {
  const options = { write: false };
  const valueFlags = new Map([
    ['--id', 'id'], ['--name', 'displayName'], ['--phase', 'phase'], ['--shots', 'shots'],
    ['--class', 'className'], ['--scene-name', 'sceneName'],
  ]);
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--write') { options.write = true; continue; }
    const key = valueFlags.get(arg);
    if (!key) throw new Error(`unknown argument '${arg}'`);
    if (i + 1 >= argv.length) throw new Error(`${arg} requires a value`);
    options[key] = argv[++i];
  }
  return options;
}

function usage() {
  return 'usage: node scripts/scaffold-unity-scene.mjs --id <kebab-id> --phase <0-10> ' +
    '[--name "Display Name"] [--scene-name Name] [--class GmName] [--shots 5] [--write]';
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const options = parseArgs(process.argv.slice(2));
    const spec = createSceneSpec(options);
    const result = scaffoldScene(spec, { write: options.write });
    console.log(`${result.wrote ? '✓ wrote' : 'DRY RUN:'} ${result.entry.id} -> ${result.entry.scenePath}`);
    for (const relative of result.files.keys()) console.log(`  ${path.join(result.entry.sourceDir, relative)}`);
    if (!result.wrote) console.log('No files changed. Re-run with --write after reviewing this plan.');
  } catch (error) {
    console.error(`✗ ${error.message}\n${usage()}`);
    process.exit(2);
  }
}
