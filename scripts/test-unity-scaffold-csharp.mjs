#!/usr/bin/env node
// Compiles a freshly generated scene pack against the pinned Unity references without opening the
// editor. This catches template/assembly/API failures even when the full Unity launch is unavailable.
import {
  existsSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  rmSync,
  statSync,
  writeFileSync,
} from 'node:fs';
import { homedir, tmpdir } from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { createSceneSpec, renderSceneFiles, REPO_ROOT } from './scaffold-unity-scene.mjs';

const unityRoot = process.env.GM_UNITY_PROJECT || path.join(REPO_ROOT, 'unity-project');
const versionFile = path.join(unityRoot, 'ProjectSettings', 'ProjectVersion.txt');
if (!existsSync(versionFile)) throw new Error(`Unity project is missing: ${versionFile}`);
const version = readFileSync(versionFile, 'utf8').match(/m_EditorVersion:\s*(\S+)/)?.[1];
if (!version) throw new Error('could not parse pinned Unity editor version');

const editorRoot = `/Applications/Unity/Hub/Editor/${version}/Unity.app`;
const dotnet = path.join(editorRoot, 'Contents', 'Resources', 'Scripting', 'DotNetSdk', 'dotnet');
const sdkRoot = path.join(editorRoot, 'Contents', 'Resources', 'Scripting', 'DotNetSdk', 'sdk');
const sdkVersion = readdirSync(sdkRoot).sort().at(-1);
const csc = path.join(sdkRoot, sdkVersion, 'Roslyn', 'bincore', 'csc.dll');
if (!existsSync(dotnet) || !existsSync(csc)) throw new Error(`pinned Unity Roslyn compiler is missing for ${version}`);

const artifacts = path.join(unityRoot, 'Library', 'Bee', 'artifacts');
const responseCandidates = readdirSync(artifacts, { withFileTypes: true })
  .filter((entry) => entry.isDirectory() && entry.name.endsWith('.dag'))
  .map((entry) => path.join(artifacts, entry.name, 'Assembly-CSharp.rsp'))
  .filter(existsSync)
  // A standalone player build writes a newer response file that intentionally omits UnityEditor
  // references. Generated scene packs contain Editor sources, so selecting that file makes a good
  // scaffold look broken immediately after every player build. Use the newest editor-capable
  // response instead.
  .filter((file) => /UnityEditor(?:\.CoreModule)?\.dll/.test(readFileSync(file, 'utf8')))
  .sort((a, b) => statSync(b).mtimeMs - statSync(a).mtimeMs);
if (!responseCandidates.length) {
  throw new Error('no editor-capable Assembly-CSharp.rsp exists; open/compile the Unity project once before this static smoke test');
}
const response = responseCandidates[0];
const responseText = readFileSync(response, 'utf8');

const temp = mkdtempSync(path.join(tmpdir(), 'gm-unity-scaffold-csharp-'));
try {
  const spec = createSceneSpec({ id: 'compile-smoke-room', phase: 1, shots: 5 });
  const generated = renderSceneFiles(spec);
  const sources = [];
  for (const [relative, content] of generated) {
    if (!relative.endsWith('.cs')) continue;
    const target = path.join(temp, relative.replaceAll('/', '-'));
    writeFileSync(target, content, 'utf8');
    sources.push(target);
  }
  // Only the .cs files above were compiled; the pack's README.md rides along in `generated` and
  // proves nothing, so it does not get counted in the verdict.
  const scaffoldSources = sources.length;

  const runtimeShared = [
    // Feel constants are referenced by the HUD, the pause menu, the audio manager and the room
    // controllers, so it has to be available even in the window between a source sync and the
    // editor's next import, when the response file does not name it yet.
    ['Assets/Scripts/GmFeelConfig.cs', 'unity/scene-system/Runtime/GmFeelConfig.cs'],
    ['Assets/Scripts/GmSceneIdentity.cs', 'unity/scene-system/Runtime/GmSceneIdentity.cs'],
    ['Assets/Scripts/GmSceneReviewTour.cs', 'unity/scene-system/Runtime/GmSceneReviewTour.cs'],
    ['Assets/Scripts/GmSceneComposition.cs', 'unity/scene-system/Runtime/GmSceneComposition.cs'],
    ['Assets/Scripts/GmSceneAdaptiveIntent.cs', 'unity/scene-system/Runtime/GmSceneAdaptiveIntent.cs'],
    ['Assets/Scripts/GmPerceptualIntents.cs', 'unity/scene-system/Runtime/GmPerceptualIntents.cs'],
    ['Assets/Scripts/GmRepetitionIntent.cs', 'unity/scene-system/Runtime/GmRepetitionIntent.cs'],
    ['Assets/Scripts/GmEnvironmentalStoryIntent.cs', 'unity/scene-system/Runtime/GmEnvironmentalStoryIntent.cs'],
    ['Assets/Scripts/GmStyleIntent.cs', 'unity/scene-system/Runtime/GmStyleIntent.cs'],
    ['Assets/Scripts/GmSurfacePaletteIntent.cs', 'unity/scene-system/Runtime/GmSurfacePaletteIntent.cs'],
    ['Assets/Scripts/GmLandscapeDepthIntent.cs', 'unity/scene-system/Runtime/GmLandscapeDepthIntent.cs'],
    ['Assets/Scripts/GmSoundscapeIntent.cs', 'unity/scene-system/Runtime/GmSoundscapeIntent.cs'],
    ['Assets/Scripts/GmPacingIntent.cs', 'unity/scene-system/Runtime/GmPacingIntent.cs'],
    ['Assets/Scripts/GmExperienceTelemetry.cs', 'unity/scene-system/Runtime/GmExperienceTelemetry.cs'],
    ['Assets/Scripts/GmAdaptiveSlot.cs', 'unity/scene-system/Runtime/GmAdaptiveSlot.cs'],
    ['Assets/Scripts/GmAudioIntent.cs', 'unity/scene-system/Runtime/GmAudioIntent.cs'],
    ['Assets/Scripts/GmLightIntent.cs', 'unity/scene-system/Runtime/GmLightIntent.cs'],
    ['Assets/Scripts/GmCompositionZone.cs', 'unity/scene-system/Runtime/GmCompositionZone.cs'],
    ['Assets/Scripts/GmCompositionCluster.cs', 'unity/scene-system/Runtime/GmCompositionCluster.cs'],
    ['Assets/Scripts/GmCompositionElement.cs', 'unity/scene-system/Runtime/GmCompositionElement.cs'],
    ['Assets/Scripts/GmRouteReservation.cs', 'unity/scene-system/Runtime/GmRouteReservation.cs'],
    ['Assets/Scripts/GmNegativeSpace.cs', 'unity/scene-system/Runtime/GmNegativeSpace.cs'],
    ['Assets/Scripts/GmMotivatedLight.cs', 'unity/scene-system/Runtime/GmMotivatedLight.cs'],
    ['Assets/Scripts/GmReviewCompositionClaim.cs', 'unity/scene-system/Runtime/GmReviewCompositionClaim.cs'],
    ['Assets/Scripts/GmRunStore.cs', 'unity/scene-system/Runtime/GmRunStore.cs'],
    ['Assets/Scripts/GmSaveSystem.cs', 'unity/scene-system/Runtime/GmSaveSystem.cs'],
    ['Assets/Scripts/GmEndingManager.cs', 'unity/scene-system/Runtime/GmEndingManager.cs'],
    ['Assets/Scripts/GmGameHud.cs', 'unity/scene-system/Runtime/GmGameHud.cs'],
    ['Assets/Scripts/GmPauseMenu.cs', 'unity/scene-system/Runtime/GmPauseMenu.cs'],
    ['Assets/Scripts/GmCreditsUI.cs', 'unity/scene-system/Runtime/GmCreditsUI.cs'],
    ['Assets/Scripts/GmSceneDirector.cs', 'unity/scene-system/Runtime/GmSceneDirector.cs'],
    ['Assets/Scripts/GmSceneTransitionTrigger.cs', 'unity/scene-system/Runtime/GmSceneTransitionTrigger.cs'],
    ['Assets/Scripts/GmAudioManager.cs', 'unity/scene-system/Runtime/GmAudioManager.cs'],
  ];
  for (const [unityRelative, repoRelative] of runtimeShared) {
    if (!responseText.includes(`"${unityRelative}"`)) sources.push(path.join(REPO_ROOT, repoRelative));
  }
  sources.push(
    // GmSceneBuildUtility applies the same textured Victorian surface adapter used by every room
    // builder. Unity's full editor assembly already sees this scene-specific source, but the
    // isolated scaffold compiler must name that dependency explicitly or its portability proof is
    // compiling a dependency graph that no generated room actually uses.
    path.join(REPO_ROOT, 'unity/scenes/wend-hill-prologue/Editor/GmVictorianInteriorKit.cs'),
    path.join(REPO_ROOT, 'unity/scene-system/Editor/GmSceneBuildUtility.cs'),
    path.join(REPO_ROOT, 'unity/scene-system/Editor/GmSceneContractAudit.cs'),
    path.join(REPO_ROOT, 'unity/scene-system/Editor/GmSceneCompositionAudit.cs'),
    path.join(REPO_ROOT, 'unity/scene-system/Editor/GmAudioAnalysis.cs'),
    path.join(REPO_ROOT, 'unity/scene-system/Editor/GmCraftQualityAudit.cs'),
    path.join(REPO_ROOT, 'unity/scene-system/Editor/GmPerceptualAudit.cs'),
    path.join(REPO_ROOT, 'unity/scene-system/Editor/GmViewportOccupancyAudit.cs'),
    path.join(REPO_ROOT, 'unity/scene-system/Editor/GmPacingSimulator.cs'),
    path.join(REPO_ROOT, 'unity/scene-system/Editor/GmSceneReportWriter.cs'),
    path.join(REPO_ROOT, 'unity/scene-system/Editor/GmAssetIntelligence.cs'),
    path.join(REPO_ROOT, 'unity/scene-system/Editor/GmSceneIntelligenceEditor.cs'),
    path.join(REPO_ROOT, 'unity/scene-system/Editor/GmVisualBaseline.cs'),
    path.join(REPO_ROOT, 'unity/scene-system/Editor/GmStandaloneBuild.cs'),
  );

  const result = spawnSync(dotnet, [
    'exec', csc, `@${response}`,
    `/out:${path.join(temp, 'GmScaffoldSmoke.dll')}`,
    `/refout:${path.join(temp, 'GmScaffoldSmoke.ref.dll')}`,
    ...sources,
  ], {
    cwd: unityRoot,
    encoding: 'utf8',
    maxBuffer: 20 * 1024 * 1024,
    timeout: 5 * 60 * 1000,
  });
  const output = `${result.stdout ?? ''}${result.stderr ?? ''}`;
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`generated C# scaffold failed static compilation:\n${output}`);
  const errors = output.split('\n').filter((line) => /error CS\d+/.test(line));
  if (errors.length) throw new Error(`generated C# scaffold reported compiler errors:\n${errors.join('\n')}`);
  console.log(`✓ generated Unity scene scaffold compiles against ${version} (${scaffoldSources} generated .cs files, ${spec.shots} shots)`);
} finally {
  rmSync(temp, { recursive: true, force: true });
}
