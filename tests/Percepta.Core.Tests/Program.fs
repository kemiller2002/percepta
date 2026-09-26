open System
open System.IO
open Percepta.Core
open Percepta.Core.Compilation
open Percepta.Examples

let mutable failures = []

let check name condition =
    if not condition then
        failures <- name :: failures

let contains (needle: string) (haystack: string) =
    haystack.Contains(needle, StringComparison.Ordinal)

let validContract = IndyInit.investigationWorkspace

let firstCompilation = Compilation.compile validContract
let secondCompilation = Compilation.compile validContract

check "compiler output is deterministic" (firstCompilation = secondCompilation)

match firstCompilation with
| Error issues ->
    failures <- $"valid contract failed compilation: {issues}" :: failures
| Ok compiled ->
    check "guidance includes purpose" (contains validContract.Purpose compiled.AgentGuidance)
    check "guidance includes primary question" (contains validContract.PrimaryQuestion compiled.AgentGuidance)
    check "guidance includes hypotheses region" (contains "hypotheses" compiled.AgentGuidance)
    check "guidance includes confirm-root-cause" (contains "confirm-root-cause" compiled.AgentGuidance)
    check "guidance includes falsified projection" (contains "hypothesis-is-falsified" compiled.AgentGuidance)
    check "guidance includes forbidden section" (contains "Forbidden outcomes" compiled.AgentGuidance)
    check "guidance includes responsive section" (contains "Responsive semantic obligations" compiled.AgentGuidance)
    check "guidance includes evidence section" (contains "Required verification evidence" compiled.AgentGuidance)

    let requiredCount =
        compiled.VerificationPlan
        |> List.filter (fun obligation -> obligation.Required)
        |> List.length

    let supportingCount =
        compiled.VerificationPlan
        |> List.filter (fun obligation -> not obligation.Required)
        |> List.length

    check "verification plan has six required obligations" (requiredCount = 6)
    check "verification plan has two supporting obligations" (supportingCount = 2)



let serialized = ContractSerialization.serialize validContract

match ContractSerialization.deserialize serialized with
| Ok roundTrip ->
    check "contract serialization round-trips" (roundTrip = validContract)
| Error errors ->
    failures <- $"serialized contract failed to deserialize: {errors}" :: failures

match ContractSerialization.deserialize """{"schemaVersion":1}""" with
| Ok _ ->
    failures <- "malformed serialized contract was accepted" :: failures
| Error _ ->
    ()

let evidenceFor (requirement: EvidenceRequirement) (status: VerificationStatus) : Evidence.EvidenceRecord =
    {
        Requirement = requirement.Id
        Kind = requirement.Kind
        Required = requirement.Required
        Status = status
        Summary = "test"
        EvidenceReferences = []
        Source = "test"
    }

let allAcceptableEvidence =
    validContract.EvidenceRequirements
    |> List.map (fun requirement ->
        if requirement.Required then
            evidenceFor requirement Passed
        else
            evidenceFor requirement (Unavailable "supporting evidence omitted"))

check
    "supporting unavailable evidence does not block completion"
    (Evidence.evaluateCompletion validContract allAcceptableEvidence)

let requiredUnavailableEvidence =
    validContract.EvidenceRequirements
    |> List.map (fun requirement ->
        if requirement.Required && requirement.Kind = Structural then
            evidenceFor requirement (Unavailable "adapter missing")
        else
            evidenceFor requirement Passed)

check
    "required unavailable evidence blocks completion"
    (not (Evidence.evaluateCompletion validContract requiredUnavailableEvidence))

let invalidContract =
    {
        validContract with
            Purpose = ""
            Regions = []
            EvidenceRequirements = []
    }

match Compilation.compile invalidContract with
| Ok _ ->
    failures <- "invalid contract compiled successfully" :: failures
| Error issues ->
    check "invalid contract reports empty purpose" (List.contains Validation.EmptyScreenPurpose issues)
    check "invalid contract reports no regions" (List.contains Validation.NoRegions issues)
    check "invalid contract reports missing evidence" (List.contains Validation.MissingRequiredEvidence issues)

let duplicateRegion =
    validContract.Regions |> List.head

let duplicateContract =
    {
        validContract with
            Regions = duplicateRegion :: validContract.Regions
    }

match Compilation.compile duplicateContract with
| Ok _ ->
    failures <- "duplicate-region contract compiled successfully" :: failures
| Error issues ->
    check
        "duplicate region is detected"
        (issues
         |> List.exists (function
             | Validation.DuplicateRegion _ -> true
             | _ -> false))

// Experiment provenance and blinding (PCT-037, PCT-038, PCT-041, PCT-043).

let reader (content: string) = fun () -> content

let neverRead (path: string) =
    fun () ->
        failures <- $"evaluator-only or frozen file was read: {path}" :: failures
        ""

let cleanLane =
    [ "lanes/lane-a/AGENTS.md", reader "# Lane A\n\nImplement the frozen brief. Do not read sibling lanes.\n"
      "lanes/lane-a/index.html", reader "<main data-percepta-region=\"hypotheses\" style=\"cursor: pointer\"></main>"
      "lanes/lane-a/screenshot.png", neverRead "lanes/lane-a/screenshot.png"
      "sealed/provenance-ledger.json", neverRead "sealed/provenance-ledger.json"
      "evaluator/candidate-mapping.json", neverRead "evaluator/candidate-mapping.json" ]

check
    "clean future bundle has no leaks and evaluator-only files are not read"
    (ExperimentBlinding.leaksInBundle "EX-PERCEPTA-2026-0099" cleanLane |> List.isEmpty)

check "sealed ledger is evaluator-only" (ExperimentBlinding.audience "sealed/provenance-ledger.json" = ExperimentBlinding.EvaluatorOnly)
check "lane AGENTS.md is visible" (ExperimentBlinding.audience "lanes/lane-a/AGENTS.md" = ExperimentBlinding.Visible)
check "review packet is visible" (ExperimentBlinding.audience "review/packet-1.json" = ExperimentBlinding.Visible)

let leakCases =
    [ "front-matter provenance in lane AGENTS.md", "lanes/lane-a/AGENTS.md", "---\nid: lane\nprovenance:\n  contributions: {}\n---\n"
      "interchange block in review packet", "review/packet-1.json", """{"schema":"praxis.provenance/1"}"""
      "JSON provenance key in candidate output", "lanes/lane-b/out.json", """{ "provenance": {} }"""
      "legacy author field in brief", "briefs/brief.md", "author_agent: someone\n"
      "execution id in prompt", "prompts/lane-a.md", "Run as EXE-20260926T081249230Z-a66fed41."
      "foreign execution key in packet", "review/packet-2.md", "from EXT-dokimos.run-42"
      "actor variable in runtime protocol", "frozen-runtime-protocol.md", "export ROS_TELEMETRY_PROVIDER=x"
      "provider name in candidate comment", "lanes/lane-a/index.html", "<!-- generated by Claude -->"
      "model name in review packet", "review/packet-3.md", "Candidate produced with gpt-5.1"
      "runtime name in brief", "briefs/brief.md", "Use the Codex CLI." ]

for (name, path, content) in leakCases do
    check
        $"leak detected: {name}"
        (ExperimentBlinding.leaksInBundle "EX-PERCEPTA-2026-0099" [ path, reader content ]
         |> List.isEmpty
         |> not)

check
    "provider name in lane directory is a leak"
    (ExperimentBlinding.leaksInBundle "EX-PERCEPTA-2026-0099" [ "lanes/openai-control/AGENTS.md", reader "# Lane\n" ]
     |> List.isEmpty
     |> not)

check
    "provenance in sealed ledger is allowed"
    (ExperimentBlinding.leaksInBundle "EX-PERCEPTA-2026-0099" [ "sealed/provenance-ledger.json", reader """{"schema":"praxis.provenance/1"}""" ]
     |> List.isEmpty)

for frozen in ExperimentBlinding.frozenExperiments do
    check
        $"frozen bundle {frozen} is excluded without reading"
        (ExperimentBlinding.leaksInBundle frozen [ "lanes/treatment-b/AGENTS.md", neverRead $"{frozen}/lanes/treatment-b/AGENTS.md" ]
         |> List.isEmpty)

let temporaryRoot =
    Path.Combine(Path.GetTempPath(), $"percepta-blinding-{Guid.NewGuid():N}")

try
    let write (relative: string) (content: string) =
        let file = Path.Combine(temporaryRoot, relative)
        Directory.CreateDirectory(Path.GetDirectoryName file) |> ignore
        File.WriteAllText(file, content)

    write "experiments/EX-PERCEPTA-2026-0004/lanes/claude/AGENTS.md" "provenance: frozen"
    write "experiments/EX-PERCEPTA-2026-0100/lanes/lane-a/AGENTS.md" "# Lane A\n"
    write "experiments/EX-PERCEPTA-2026-0100/sealed/provenance-ledger.json" """{"schema":"praxis.provenance/1"}"""
    write "research/experiments/EX-PERCEPTA-2026-0101/review/packet.md" "Reviewed output from Anthropic."

    let found = ExperimentBlinding.scanRepository temporaryRoot

    check
        "on-disk scan skips frozen bundle directories"
        (ExperimentBlinding.futureBundles temporaryRoot |> List.map fst = [ "EX-PERCEPTA-2026-0100"; "EX-PERCEPTA-2026-0101" ])

    check
        "on-disk scan reports only the leaking future bundle"
        (found |> List.map (fun leak -> leak.Path) |> List.distinct = [ "EX-PERCEPTA-2026-0101/review/packet.md" ])
finally
    if Directory.Exists temporaryRoot then
        Directory.Delete(temporaryRoot, true)

match
    [ Directory.GetCurrentDirectory(); AppContext.BaseDirectory ]
    |> List.tryPick ExperimentBlinding.findRepositoryRoot
with
| None -> failures <- "could not locate the Percepta repository root for the experiment leakage scan" :: failures
| Some root ->
    for leak in ExperimentBlinding.scanRepository root do
        failures <- $"experiment identity leak in {leak.Path} ({leak.Signal})" :: failures

if List.isEmpty failures then
    printfn "Percepta.Core.Tests: all checks passed."
else
    eprintfn "Percepta.Core.Tests: %d failure(s)." failures.Length

    for failure in List.rev failures do
        eprintfn "- %s" failure

    Environment.ExitCode <- 1
