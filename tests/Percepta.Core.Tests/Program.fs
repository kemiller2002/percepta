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

// ---------------------------------------------------------------- specification preflight (PER-SPEC-PREFLIGHT)

do
    let t id state event guard next effects : Specification.Transition =
        { Id = id; State = state; Event = event; Guard = guard; Next = next; Effects = effects }

    let machine transitions : Specification.Machine = { Id = "SM-PUMP"; Initial = "Idle"; Transitions = transitions }

    let spec machines invariants types operations catalog : Specification.Specification =
        { Machines = machines; Invariants = invariants; Types = types; Operations = operations
          Catalog = catalog; UnsupportedObligations = [] }

    let noCatalog : Specification.Catalog = { Carrier = None; Commands = [] }

    let bound : Specification.Invariant =
        { Id = "INV-PUMP"; Machines = [ "SM-PUMP" ]
          Statement = "Each start is logged; under a stuck valve at most three pump strokes in 30 seconds." }

    let path =
        [ t "P1" "Idle" "Start" "true" "Pumping" [ "one pump stroke" ]
          t "P2" "Pumping" "Stall" "true" "Stalled" [ "log the stall" ] ]

    let findings transitions = Specification.analyze (spec [ machine (path @ transitions) ] [ bound ] [] [] noCatalog)
    let rules transitions = findings transitions |> List.map _.Rule

    let types fsharp = Specification.parseTypes fsharp

    let carrierSpec fsharp =
        spec [] [] (types fsharp)
            [ { Interface = "IF-KERNEL"; Name = "Decide"; Signature = "Policy -> Envelope -> Decision" } ]
            { Carrier = Some "Envelope"; Commands = [ "OpenValve"; "CloseValve"; "Flush" ] }

    check "spec: a rate bound is extracted from prose" (
        Specification.rateBounds bound = [ { Invariant = "INV-PUMP"; Effect = "pump stroke"; Max = 3; WindowSeconds = 30; Condition = "under a stuck valve" } ])

    let contradiction = findings [ t "P3" "Stalled" "Retry" "operator retried" "Stalled" [ "one pump stroke" ] ]

    check "spec: an unguarded self-loop emitting a rate-bounded effect is a likely contradiction with a counterexample" (
        match contradiction with
        | [ f ] ->
            f.Kind = Specification.LikelyContradiction && f.Subjects = [ "INV-PUMP"; "SM-PUMP"; "P3" ] && f.Blocking && f.OwnerDecision
            && (f.Counterexample |> List.filter (fun line -> line.Contains "pump stroke #") |> List.length) = 4
        | _ -> false)

    check "spec: a contradiction is never reported as proven when the bound came from prose" (
        contradiction |> List.forall (fun f -> f.Kind <> Specification.ProvenContradiction))

    check "spec: a counter or a clock in the loop's guard bounds it" (
        not (List.contains "rate-bound-vs-unbounded-loop" (rules [ t "P3" "Stalled" "Retry" "fewer than three retries made" "Stalled" [ "one pump stroke" ] ]))
        && rules [ t "P3" "Stalled" "Retry" "at least 20 seconds after the last stroke" "Stalled" [ "one pump stroke" ] ] = [])

    check "spec: an effect stated as none, a negated effect or a substring is not an emission" (
        rules [ t "P3" "Stalled" "Retry" "true" "Stalled" [ "none: confirms the PumpStrokeAnswer" ] ] = []
        && rules [ t "P3" "Stalled" "Retry" "true" "Stalled" [ "send no pump stroke" ] ] = [])

    check "spec: a counter guard without its complement is a missing specification" (
        match findings [ t "P3" "Stalled" "ReadFailed" "fewer than three reads made" "Stalled" [ "schedule a read" ] ] with
        | [ f ] -> f.Kind = Specification.MissingSpecification && f.Rule = "counter-guard-without-complement" && not f.Blocking
        | _ -> false)

    check "spec: both sides of the counter, or a state reference, need no complement" (
        rules [ t "P3" "Stalled" "ReadFailed" "fewer than three reads made" "Stalled" [ "schedule a read" ]
                t "P4" "Stalled" "ReadFailed" "three reads already made" "Stalled" [ "offer check again" ] ] = []
        && rules [ t "P3" "Stalled" "Resolved" "SM-VALVE reached Open" "Idle" [ "resume" ] ] = []
        // "already sent" is the at-limit side as much as "already made" (found on Indy Init v4.1.2's T-S32/T-S63).
        && rules [ t "P3" "Stalled" "Resume" "fewer than four reads sent in the last 60 s" "Stalled" [ "one read" ]
                   t "P4" "Stalled" "Resume" "four reads already sent in the last 60 s" "Stalled" [ "send nothing" ] ] = [])

    check "spec: F# records, multi-case and single-case unions parse" (
        types "type Id = Id of Guid\ntype Envelope = { CommandId: Guid; Target: Id; Version: int64 option }\ntype Decision =\n    | Allowed of string list\n    | Blocked" =
            [ Specification.Union("Id", [ "Id", Some "Guid" ])
              Specification.Record("Envelope", [ "CommandId", "Guid"; "Target", "Id"; "Version", "int64 option" ])
              Specification.Union("Decision", [ "Allowed", Some "string list"; "Blocked", None ]) ])

    check "spec: a carrier of identities and versions cannot discriminate the catalog's commands" (
        match Specification.analyze (carrierSpec "type Id = Id of Guid\ntype Envelope = { CommandId: Guid; Target: Id; Version: int64 option }\ntype Box = { Pending: Envelope list }") with
        | [ f ] ->
            f.Kind = Specification.MissingInterface && f.Subjects = [ "Envelope"; "IF-KERNEL.Decide" ]
            && f.Counterexample |> List.exists (fun l -> l.Contains "Box carries Envelope (Pending)")
        | _ -> false)

    check "spec: a carrier with a command union, a string or an opaque type is sufficient" (
        Specification.analyze (carrierSpec "type Command =\n    | OpenValve\n    | CloseValve\ntype Envelope = { CommandId: Guid; Command: Command }") = []
        && Specification.analyze (carrierSpec "type Envelope = { CommandId: Guid; Kind: string }") = []
        && Specification.analyze (carrierSpec "type Envelope = { CommandId: Guid; Body: CommandBody }") = [])

    check "spec: the catalog's command tables and carrier are read from markdown" (
        Specification.readCatalog "Every command is carried inside Envelope.\n\n| Command | Primary event(s) |\n| --- | --- |\n| OpenValve | ValveOpened |\n| `Flush` | Flushed |\n\n| Event | x |\n| --- | --- |\n| ValveOpened | y |"
            = { Carrier = Some "Envelope"; Commands = [ "OpenValve"; "Flush" ] })

if List.isEmpty failures then
    printfn "Percepta.Core.Tests: all checks passed."
else
    eprintfn "Percepta.Core.Tests: %d failure(s)." failures.Length

    for failure in List.rev failures do
        eprintfn "- %s" failure

    Environment.ExitCode <- 1
