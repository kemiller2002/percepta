open System
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

match Compilation.compile validContract with
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

if List.isEmpty failures then
    printfn "Percepta.Core.Tests: all checks passed."
    0
else
    eprintfn "Percepta.Core.Tests: %d failure(s)." failures.Length

    for failure in List.rev failures do
        eprintfn "- %s" failure

    1
