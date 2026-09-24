open System
open System.IO
open System.Text
open System.Text.Json
open Percepta.Core
open Percepta.Adapter.Playwright

type MutationClass =
    | MachineAddressable
    | SemanticSubstitution

type Mutation =
    {
        Name: string
        Class: MutationClass
        ExpectedKind: EvidenceKind option
        Transform: string -> string
    }

type ExperimentResult =
    {
        Name: string
        Class: string
        ExpectedKind: string option
        ExpectedStatus: string
        ObservedStatus: string
        Complete: bool
        DetectedAsExpected: bool
        EvidenceSummary: string
    }

let kindName =
    function
    | ContractValidation -> "contract-validation"
    | Structural -> "structural"
    | StateProjection -> "state-projection"
    | Interaction -> "interaction"
    | Accessibility -> "accessibility"
    | Responsive -> "responsive"
    | VisualRegression -> "visual-regression"
    | SemanticVisualReview -> "semantic-visual-review"
    | AcceptedDeviationEvidence -> "accepted-deviation"

let statusName =
    function
    | Passed -> "Passed"
    | Failed _ -> "Failed"
    | Unavailable _ -> "Unavailable"
    | NotApplicable _ -> "NotApplicable"
    | AcceptedDeviation _ -> "AcceptedDeviation"

let acceptable =
    function
    | Passed
    | NotApplicable _
    | AcceptedDeviation _ -> true
    | Failed _
    | Unavailable _ -> false

let className =
    function
    | MachineAddressable -> "machine-addressable"
    | SemanticSubstitution -> "semantic-substitution"

let replaceRequired oldValue newValue (text: string) =
    if not (text.Contains(oldValue, StringComparison.Ordinal)) then
        failwith $"Mutation source was not found: {oldValue}"

    text.Replace(oldValue, newValue, StringComparison.Ordinal)

let appendScript script (text: string) =
    replaceRequired "</body>" $"<script>{script}</script>\n</body>" text

let wrapStateScript body =
    $"""
(() => {{
  const original = window.__perceptaSetState;
  window.__perceptaSetState = state => {{
    original(state);
    {body}
  }};
}})();
"""

let mutations =
    [
        {
            Name = "missing-observation-region"
            Class = MachineAddressable
            ExpectedKind = Some Structural
            Transform =
                replaceRequired
                    "data-percepta-region=\"observation\""
                    "data-mutant-region=\"observation\""
        }
        {
            Name = "deleted-falsified-hypothesis"
            Class = MachineAddressable
            ExpectedKind = Some StateProjection
            Transform =
                replaceRequired
                    "data-percepta-observation=\"falsified-hypothesis-retained\""
                    "data-mutant-observation=\"falsified-hypothesis-retained\""
        }
        {
            Name = "hidden-blocking-unknown"
            Class = MachineAddressable
            ExpectedKind = Some StateProjection
            Transform =
                replaceRequired
                    "data-percepta-observation=\"blocker-visible\""
                    "data-mutant-observation=\"blocker-visible\""
        }
        {
            Name = "illegal-action-enabled"
            Class = MachineAddressable
            ExpectedKind = Some Interaction
            Transform =
                appendScript (
                    wrapStateScript
                        """
const action = document.querySelector('[data-percepta-capability="confirm-root-cause"]');
action.disabled = false;
action.setAttribute('aria-disabled', 'false');
"""
                )
        }
        {
            Name = "unavailable-reason-missing"
            Class = MachineAddressable
            ExpectedKind = Some Interaction
            Transform =
                appendScript (
                    wrapStateScript
                        """
const reason = document.querySelector('[data-percepta-unavailable-reason-for="confirm-root-cause"]');
reason.textContent = '';
reason.hidden = true;
"""
                )
        }
        {
            Name = "blocker-navigation-missing"
            Class = MachineAddressable
            ExpectedKind = Some Interaction
            Transform =
                appendScript (
                    wrapStateScript
                        """
document.querySelector('[data-percepta-blocker-link-for="confirm-root-cause"]')?.remove();
"""
                )
        }
        {
            Name = "color-only-state"
            Class = MachineAddressable
            ExpectedKind = Some Structural
            Transform =
                appendScript (
                    wrapStateScript
                        """
for (const el of document.querySelectorAll('[data-percepta-state]')) {
  el.textContent = '';
  el.removeAttribute('aria-label');
}
"""
                )
        }
        {
            Name = "inaccessible-action-name"
            Class = MachineAddressable
            ExpectedKind = Some Accessibility
            Transform =
                appendScript (
                    wrapStateScript
                        """
const action = document.querySelector('[data-percepta-capability="confirm-root-cause"]');
action.textContent = '';
action.removeAttribute('aria-label');
action.removeAttribute('title');
"""
                )
        }
        {
            Name = "horizontal-overflow"
            Class = MachineAddressable
            ExpectedKind = Some Responsive
            Transform =
                replaceRequired
                    "<main>"
                    "<main style=\"width: 2000px; max-width: none;\">"
        }
        {
            Name = "optimistic-persistence-success"
            Class = MachineAddressable
            ExpectedKind = Some StateProjection
            Transform =
                appendScript (
                    wrapStateScript
                        """
document.getElementById('persistence-copy').textContent = 'Synced';
for (const id of ['persistence-pending-visible', 'no-false-remote-success']) {
  const el = document.querySelector('[data-percepta-observation="' + id + '"]');
  if (el) el.hidden = true;
}
"""
                )
        }
        {
            Name = "chat-only-primary"
            Class = MachineAddressable
            ExpectedKind = Some Structural
            Transform =
                replaceRequired
                    "<main>"
                    "<div data-percepta-chat-primary=\"true\">Ask the agent anything.</div>\n<main>"
        }
        {
            Name = "unsupported-confidence"
            Class = MachineAddressable
            ExpectedKind = Some Structural
            Transform =
                replaceRequired
                    "<main>"
                    "<div data-percepta-confidence-kind=\"unsupported\">Root cause confidence: 92%</div>\n<main>"
        }
        {
            Name = "linearized-competing-hypotheses"
            Class = MachineAddressable
            ExpectedKind = Some Structural
            Transform =
                replaceRequired
                    "<main>"
                    "<div data-percepta-linearized-competing=\"true\">Step 1: H1, then H2.</div>\n<main>"
        }
        {
            Name = "wrong-primary-question"
            Class = SemanticSubstitution
            ExpectedKind = None
            Transform =
                replaceRequired
                    "What currently explains the observation, and what should be tested next?"
                    "Which explanation should we accept immediately?"
        }
        {
            Name = "misleading-unknowns-content"
            Class = SemanticSubstitution
            ExpectedKind = None
            Transform =
                appendScript (
                    wrapStateScript
                        """
document.getElementById('unknown-copy').textContent = 'There are no important unknowns remaining.';
"""
                )
        }
    ]

let findRepositoryRoot startDirectory =
    let rec loop (directory: DirectoryInfo) =
        if File.Exists(Path.Combine(directory.FullName, "ros.json")) then
            directory.FullName
        elif isNull directory.Parent then
            failwith "Could not locate repository root."
        else
            loop directory.Parent

    loop (DirectoryInfo(Path.GetFullPath(startDirectory)))

let writeResults path canonicalComplete results detectionRate h1Supported h2Supported =
    Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore

    use stream = File.Create(path)
    use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

    writer.WriteStartObject()
    writer.WriteNumber("schemaVersion", 1)
    writer.WriteString("experiment", "EX-PERCEPTA-2026-0001")
    writer.WriteString("generatedAtUtc", DateTimeOffset.UtcNow.ToString("O"))
    writer.WriteBoolean("canonicalControlComplete", canonicalComplete)
    writer.WriteNumber("machineAddressableDetectionRate", detectionRate)
    writer.WriteBoolean("hypothesis1Supported", h1Supported)
    writer.WriteBoolean("hypothesis2Supported", h2Supported)
    writer.WritePropertyName("results")
    writer.WriteStartArray()

    for result in results do
        writer.WriteStartObject()
        writer.WriteString("mutation", result.Name)
        writer.WriteString("class", result.Class)

        match result.ExpectedKind with
        | Some kind -> writer.WriteString("expectedKind", kind)
        | None -> writer.WriteNull("expectedKind")

        writer.WriteString("expectedStatus", result.ExpectedStatus)
        writer.WriteString("observedStatus", result.ObservedStatus)
        writer.WriteBoolean("complete", result.Complete)
        writer.WriteBoolean("detectedAsExpected", result.DetectedAsExpected)
        writer.WriteString("evidenceSummary", result.EvidenceSummary)
        writer.WriteEndObject()

    writer.WriteEndArray()
    writer.WriteEndObject()
    writer.Flush()

let verifyRequiredCompletion contract adapterEvidence =
    contract.EvidenceRequirements
    |> List.filter _.Required
    |> List.forall (fun requirement ->
        match requirement.Kind with
        | ContractValidation -> true
        | kind ->
            adapterEvidence
            |> List.tryFind (fun evidence -> evidence.Kind = kind)
            |> Option.map (fun evidence -> acceptable evidence.Status)
            |> Option.defaultValue false)

let runVerification root contract htmlPath runName =
    task {
        let outputRoot = Path.Combine(root, "artifacts", "percepta", "adversarial", runName)

        let options: BrowserVerification.Options =
            {
                Url = htmlPath
                StateFixturePaths =
                    [
                        Path.Combine(root, "tests", "fixtures", "indy-init", "blocked.json")
                        Path.Combine(root, "tests", "fixtures", "indy-init", "legal.json")
                    ]
                ScreenshotDirectory = Path.Combine(outputRoot, "screenshots")
                BaselineDirectory = Path.Combine(outputRoot, "baselines")
                UpdateBaselines = false
            }

        let! evidence = BrowserVerification.verify options contract
        return evidence
    }

[<EntryPoint>]
let main _ =
    try
        let root = findRepositoryRoot (Directory.GetCurrentDirectory())
        let contractPath = Path.Combine(root, ".percepta", "contracts", "indy-init-investigation-workspace.json")
        let canonicalPath = Path.Combine(root, "tests", "fixtures", "indy-init", "index.html")
        let canonicalHtml = File.ReadAllText(canonicalPath)

        let contract =
            match ContractSerialization.deserialize (File.ReadAllText(contractPath)) with
            | Ok value -> value
            | Error errors -> failwith $"Could not load contract: {String.concat " | " errors}"

        let canonicalEvidence =
            runVerification root contract canonicalPath "control"
            |> fun task -> task.GetAwaiter().GetResult()

        let canonicalComplete = verifyRequiredCompletion contract canonicalEvidence

        printfn "Canonical control complete: %b" canonicalComplete

        if not canonicalComplete then
            eprintfn "The canonical positive control failed. Experiment results would be invalid."
            2
        else
            let tempRoot = Path.Combine(root, "artifacts", "percepta", "adversarial", "mutants")
            Directory.CreateDirectory(tempRoot) |> ignore

            let results =
                mutations
                |> List.map (fun mutation ->
                    let mutantPath = Path.Combine(tempRoot, mutation.Name + ".html")
                    let mutated = mutation.Transform canonicalHtml
                    File.WriteAllText(mutantPath, mutated, Encoding.UTF8)

                    let evidence =
                        runVerification root contract mutantPath mutation.Name
                        |> fun task -> task.GetAwaiter().GetResult()

                    let complete = verifyRequiredCompletion contract evidence

                    let expectedEvidence =
                        mutation.ExpectedKind
                        |> Option.bind (fun kind -> evidence |> List.tryFind (fun item -> item.Kind = kind))

                    let observedStatus, summary =
                        match expectedEvidence with
                        | Some item -> statusName item.Status, item.Summary
                        | None when mutation.Class = SemanticSubstitution ->
                            if complete then "Escaped" else "DetectedElsewhere",
                            "Semantic substitution has no pre-registered deterministic detector."
                        | None -> "MissingEvidence", "Expected evidence category was not produced."

                    let detectedAsExpected =
                        match mutation.Class, expectedEvidence with
                        | MachineAddressable, Some item ->
                            match item.Status with
                            | Failed _ -> not complete
                            | _ -> false
                        | SemanticSubstitution, _ -> complete

                    printfn
                        "%-34s %-22s expected=%-18s observed=%-18s complete=%b"
                        mutation.Name
                        (className mutation.Class)
                        (mutation.ExpectedKind |> Option.map kindName |> Option.defaultValue "escape-candidate")
                        observedStatus
                        complete

                    {
                        Name = mutation.Name
                        Class = className mutation.Class
                        ExpectedKind = mutation.ExpectedKind |> Option.map kindName
                        ExpectedStatus =
                            if mutation.Class = MachineAddressable then "Failed" else "MayEscape"
                        ObservedStatus = observedStatus
                        Complete = complete
                        DetectedAsExpected = detectedAsExpected
                        EvidenceSummary = summary
                    })

            let machineResults =
                results |> List.filter (fun result -> result.Class = "machine-addressable")

            let detected =
                machineResults |> List.filter _.DetectedAsExpected |> List.length

            let detectionRate =
                if machineResults.IsEmpty then 0.0 else float detected / float machineResults.Length

            let machineFalseCompletions =
                machineResults |> List.filter _.Complete

            let semanticEscapes =
                results
                |> List.filter (fun result -> result.Class = "semantic-substitution" && result.Complete)

            let h1Supported =
                detectionRate >= 0.90 && List.isEmpty machineFalseCompletions

            let h2Supported = not semanticEscapes.IsEmpty

            let outputPath =
                Path.Combine(root, "artifacts", "percepta", "adversarial", "experiment-results.json")

            writeResults outputPath canonicalComplete results detectionRate h1Supported h2Supported

            printfn ""
            printfn "Machine-addressable detection: %d/%d (%.1f%%)" detected machineResults.Length (detectionRate * 100.0)
            printfn "Machine-addressable false completions: %d" machineFalseCompletions.Length
            printfn "Semantic substitutions escaping deterministic checks: %d/%d" semanticEscapes.Length 2
            printfn "HY-PERCEPTA-2026-0001 supported: %b" h1Supported
            printfn "HY-PERCEPTA-2026-0002 supported: %b" h2Supported
            printfn "Results: %s" outputPath

            0
    with ex ->
        eprintfn "Adversarial experiment failed to execute: %s" ex.Message
        10
