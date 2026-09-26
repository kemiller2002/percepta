/// Leakage check for future experiment bundles (PCT-038, PCT-041, PCT-043 in
/// requirements/PERCEPTA-EXPERIMENT-PROVENANCE.md). Lane-visible and
/// reviewer-visible material must not reveal provenance or actor identity;
/// evaluator-only material under `sealed/` and `evaluator/` may.
module ExperimentBlinding

open System
open System.IO
open System.Text.RegularExpressions

/// Existing experiments are frozen (PCT-041). Bundles whose directory name
/// starts with one of these ids are excluded before any file is opened.
let frozenExperiments =
    [ "EX-PERCEPTA-2026-0001"
      "EX-PERCEPTA-2026-0002"
      "EX-PERCEPTA-2026-0003"
      "EX-PERCEPTA-2026-0004" ]

/// Repository-relative directories that hold experiment bundles.
let bundleRoots = [ "experiments"; "research/experiments" ]

type Audience =
    | EvaluatorOnly
    | Visible

type Leak = { Path: string; Signal: string }

let private normalize (path: string) = path.Replace('\\', '/')

let isFrozen (bundleName: string) =
    frozenExperiments
    |> List.exists (fun id -> bundleName.StartsWith(id, StringComparison.OrdinalIgnoreCase))

/// Audience of a path relative to the bundle directory.
let audience (bundleRelativePath: string) =
    match (normalize bundleRelativePath).Split('/', StringSplitOptions.RemoveEmptyEntries) |> Array.tryHead with
    | Some first when
        String.Equals(first, "sealed", StringComparison.OrdinalIgnoreCase)
        || String.Equals(first, "evaluator", StringComparison.OrdinalIgnoreCase)
        ->
        EvaluatorOnly
    | _ -> Visible

let private regex ignoreCase pattern =
    let options =
        RegexOptions.Multiline ||| RegexOptions.CultureInvariant
        ||| (if ignoreCase then RegexOptions.IgnoreCase else RegexOptions.None)

    Regex(pattern, options)

let private identityNames =
    regex true @"\b(anthropic|openai|claude|chatgpt|codex|gemini|deepmind|copilot|gpt-\d[\w.-]*)\b"

let private contentSignals =
    [ "provenance block", regex true @"^\s*provenance\s*:|""provenance""\s*:|praxis\.provenance/"
      "legacy author field", regex true @"\b(author_agent|created_by_agent|owner_agent|source_author)\b"
      "execution key", regex false @"\b(EXE-\d{8}T\d{9}Z-[0-9a-f]{8}|EXT-[a-z0-9-]+\.[A-Za-z0-9._:-]+|CTB-[A-Za-z0-9][A-Za-z0-9._-]*)"
      "actor variable", regex false @"\bROS_(ACTOR_KIND|ACTOR|TELEMETRY_PROVIDER|TELEMETRY_MODEL|TELEMETRY_RUNTIME|EXECUTION_ID)\b"
      "provider/model/runtime name", identityNames ]

let private textExtensions =
    set [ ".md"; ".txt"; ".json"; ".jsonl"; ".yaml"; ".yml"; ".html"; ".htm"; ".css"; ".js"; ".mjs"; ".ts"; ".tsx"; ".jsx"; ".fs"; ".fsx"; ".sh"; ".log"; ".tsv"; ".csv"; ".xml"; ".svg" ]

let isText (path: string) =
    textExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())

/// Identity signals in a file's content.
let leaksInText (path: string) (content: string) =
    contentSignals
    |> List.choose (fun (name, pattern) ->
        let found = pattern.Match content

        if found.Success then
            Some { Path = path; Signal = $"{name}: {found.Value.Trim()}" }
        else
            None)

/// Identity signals in a path name (for example a lane named after a provider).
let leaksInPath (path: string) =
    let found = identityNames.Match(normalize path)

    if found.Success then
        [ { Path = path; Signal = $"provider/model/runtime name in path: {found.Value}" } ]
    else
        []

/// Leaks in one bundle. Each file is (bundle-relative path, content reader);
/// the reader is invoked only for visible text files, so evaluator-only
/// material is never read.
let leaksInBundle (bundleName: string) (files: (string * (unit -> string)) list) =
    if isFrozen bundleName then
        []
    else
        files
        |> List.filter (fun (path, _) -> audience path = Visible)
        |> List.collect (fun (path, read) ->
            let shown = $"{bundleName}/{normalize path}"
            leaksInPath (normalize path)
            |> List.map (fun leak -> { leak with Path = shown })
            |> List.append (if isText path then leaksInText shown (read ()) else []))

/// Future (non-frozen) bundle directories under the repository root. Frozen
/// bundles are dropped by name before their contents are enumerated.
let futureBundles (repositoryRoot: string) =
    bundleRoots
    |> List.map (fun root -> Path.Combine(repositoryRoot, root))
    |> List.filter Directory.Exists
    |> List.collect (fun root -> Directory.GetDirectories root |> Array.toList)
    |> List.map (fun directory -> Path.GetFileName directory, directory)
    |> List.filter (fun (name, _) -> name.StartsWith("EX-", StringComparison.Ordinal) && not (isFrozen name))
    |> List.sort

/// Scan every future bundle on disk.
let scanRepository (repositoryRoot: string) =
    futureBundles repositoryRoot
    |> List.collect (fun (name, directory) ->
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
        |> Seq.map (fun file -> Path.GetRelativePath(directory, file), (fun () -> File.ReadAllText file))
        |> Seq.toList
        |> leaksInBundle name)

/// Nearest ancestor of `start` that is the Percepta repository root.
let rec findRepositoryRoot (start: string) =
    let marker = Path.Combine(start, "requirements", "PERCEPTA-EXPERIMENT-PROVENANCE.md")

    if File.Exists(Path.Combine(start, "ros.json")) && File.Exists marker then
        Some start
    else
        match Directory.GetParent start with
        | null -> None
        | parent -> findRepositoryRoot parent.FullName
