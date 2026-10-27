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

// Screen bindings (docs/SCREEN-BINDING.md): applicability is declared by the application and is never
// inferred; an unbound contract must never pass.
let bindingJson screens =
    $$"""{"schema":"percepta.screen-bindings/1","application":"fixture","stateInjection":true,"screens":[{{screens}}]}"""

let boundWorkspace =
    """{"contract":"investigation-workspace","appScreen":"workspace","path":"index.html#/w","fixtures":["a.json"]}"""

let excludedSandbox =
    """{"contract":"S16-counterfactual-sandbox","notApplicable":"No sandbox in this release."}"""

let recordsWith status =
    validContract.EvidenceRequirements
    |> List.map (fun requirement -> evidenceFor requirement (if requirement.Required then status else Unavailable "supporting"))

let bindingChecks =
    match Binding.parse (bindingJson (boundWorkspace + "," + excludedSandbox)) with
    | Error errors -> [ $"valid bindings rejected: {errors}", false ]
    | Ok bindings ->
        let workspace = Binding.resolve bindings validContract.Id
        let sandbox = Binding.resolve bindings (ScreenId "S16-counterfactual-sandbox")
        let unbound = Binding.resolve bindings (ScreenId "S03-hypothesis-battlefield")

        [
            "bound screen resolves with its app screen id",
            (match workspace with
             | Binding.ResolvedBound screen -> screen.AppScreen = "workspace" && screen.Fixtures = [ "a.json" ]
             | _ -> false)
            "declared exclusion resolves as not applicable", (match sandbox with Binding.ResolvedNotApplicable _ -> true | _ -> false)
            "NEGATIVE CONTROL: unmentioned contract resolves NotBound", (unbound = Binding.NotBound)
            "NEGATIVE CONTROL: NotBound verdict is NOT-BOUND even with passing records",
            (Binding.verdict unbound (recordsWith Passed) = Binding.NotBoundVerdict)
            "NEGATIVE CONTROL: NotBound never completes, even when exclusions are allowed",
            (not (Binding.complete true 0 [ Binding.Pass; Binding.NotBoundVerdict ]))
            "not-applicable verdict is never PASS", (Binding.verdictName (Binding.verdict sandbox (recordsWith Passed)) = "NOT-APPLICABLE")
            "not-applicable blocks completion by default", (not (Binding.complete false 0 [ Binding.Pass; Binding.verdict sandbox [] ]))
            "not-applicable completes only when explicitly allowed", (Binding.complete true 0 [ Binding.Pass; Binding.verdict sandbox [] ])
            "bound screen with acceptable evidence passes", (Binding.verdict workspace (recordsWith Passed) = Binding.Pass)
            "bound screen with failed required evidence fails",
            (match Binding.verdict workspace (recordsWith (Failed "x")) with Binding.Fail _ -> true | _ -> false)
            "bound screen with unavailable required evidence fails",
            (match Binding.verdict workspace (recordsWith (Unavailable "no state hook")) with Binding.Fail _ -> true | _ -> false)
            "bound screen without evidence fails", (match Binding.verdict workspace [] with Binding.Fail _ -> true | _ -> false)
            "empty run never completes", (not (Binding.complete true 0 []))
            "orphan binding blocks completion", (not (Binding.complete true 1 [ Binding.Pass ]))
            "orphan bindings are reported",
            (Binding.orphans bindings [ validContract.Id ] = [ ScreenId "S16-counterfactual-sandbox" ])
        ]

let rejects name json =
    name, (match Binding.parse json with Error _ -> true | Ok _ -> false)

let bindingParseChecks =
    [
        rejects "wrong schema is rejected" (bindingJson boundWorkspace |> fun json -> json.Replace("screen-bindings/1", "screen-bindings/9"))
        rejects "bound screen without appScreen is rejected" (bindingJson """{"contract":"x","path":"p"}""")
        rejects "exclusion without reason is rejected" (bindingJson """{"contract":"x","notApplicable":" "}""")
        rejects "exclusion that also binds is rejected" (bindingJson """{"contract":"x","notApplicable":"r","appScreen":"a"}""")
        rejects "duplicate declarations are rejected" (bindingJson (boundWorkspace + "," + boundWorkspace))
        rejects "missing stateInjection is rejected" """{"schema":"percepta.screen-bindings/1","application":"a","screens":[]}"""
        rejects "malformed JSON is rejected" "{"
    ]

for name, passed in bindingChecks @ bindingParseChecks do
    check name passed

if List.isEmpty failures then
    printfn "Percepta.Core.Tests: all checks passed."
else
    eprintfn "Percepta.Core.Tests: %d failure(s)." failures.Length

    for failure in List.rev failures do
        eprintfn "- %s" failure

    Environment.ExitCode <- 1
