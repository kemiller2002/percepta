// Browser tests for bound-screen verification (docs/SCREEN-BINDING.md) against
// tests/fixtures/bound-app: screen identity, asynchronous state injection, keyboard reachability,
// component boundaries, and the negative controls that must never pass.
open System
open System.IO
open Percepta.Core
open Percepta.Adapter.Playwright

let repositoryRoot =
    let rec up (directory: DirectoryInfo) =
        if File.Exists(Path.Combine(directory.FullName, "package.json")) && Directory.Exists(Path.Combine(directory.FullName, "tests", "fixtures")) then
            directory.FullName
        elif isNull directory.Parent then
            failwith "Repository root not found."
        else
            up directory.Parent

    up (DirectoryInfo(AppContext.BaseDirectory))

let fixture path = Path.Combine(repositoryRoot, "tests", "fixtures", path)
let workArea = Path.Combine(repositoryRoot, "artifacts", "percepta", "binding-tests")

let contract =
    match ContractSerialization.deserialize (File.ReadAllText(fixture "bound-app/contracts/indy-init-investigation-workspace.json")) with
    | Ok value -> value
    | Error errors -> failwithf "Fixture contract is invalid: %A" errors

let pageUrl (relative: string) =
    Uri(Uri(Path.GetFullPath(fixture "bound-app/") |> Uri |> _.AbsoluteUri), relative).AbsoluteUri

let options name relative fixtures : BrowserVerification.Options =
    {
        Url = pageUrl relative
        StateFixturePaths = fixtures |> List.map fixture
        ScreenshotDirectory = Path.Combine(workArea, name, "screenshots")
        BaselineDirectory = Path.Combine(workArea, name, "baselines")
        UpdateBaselines = false
    }

let stateFixtures = [ "indy-init/blocked.json"; "indy-init/legal.json" ]

let target appScreen stateInjection : BrowserVerification.ScreenTarget =
    {
        AppScreen = appScreen
        StateInjection = stateInjection
        ReadyTimeoutMs = 5000.0
    }

let run name appScreen stateInjection relative fixtures =
    (BrowserVerification.verifyBound (target appScreen stateInjection) (options name relative fixtures) contract)
        .GetAwaiter()
        .GetResult()

let statusOf kind (results: BrowserVerification.AdapterEvidence list) =
    results |> List.find (fun result -> result.Kind = kind) |> _.Status

let failedWith (marker: string) =
    function
    | Failed reason -> reason.Contains(marker, StringComparison.Ordinal)
    | _ -> false

let verdictFor results =
    let records: Evidence.EvidenceRecord list =
        contract.EvidenceRequirements
        |> List.map (fun requirement ->
            {
                Requirement = requirement.Id
                Kind = requirement.Kind
                Required = requirement.Required
                Status =
                    match requirement.Kind with
                    | ContractValidation -> Passed
                    | kind ->
                        results
                        |> List.tryFind (fun (result: BrowserVerification.AdapterEvidence) -> result.Kind = kind)
                        |> Option.map _.Status
                        |> Option.defaultValue (Unavailable "not produced")
                Summary = ""
                EvidenceReferences = []
                Source = "test"
            })

    let bound: Binding.BoundScreen =
        { Contract = contract.Id; AppScreen = "investigation-workspace"; Path = ""; Fixtures = [] }

    Binding.verdict (Binding.ResolvedBound bound) records

let describe (results: BrowserVerification.AdapterEvidence list) =
    results |> List.map (fun result -> $"{result.Kind}={result.Status}") |> String.concat "; "

let positive = run "positive" "investigation-workspace" true "index.html#/workspace" stateFixtures
let wrongScreen = run "wrong-screen" "investigation-workspace" true "index.html#/settings" stateFixtures
let noHook = run "no-hook" "investigation-workspace" true "index.html?nohook#/workspace" stateFixtures
let declaredNoInjection = run "declared-no-injection" "investigation-workspace" false "index.html#/workspace" stateFixtures
let untabbable = run "untabbable" "investigation-workspace" true "index.html?untabbable#/workspace" stateFixtures
let duplicateRegion = run "duplicate-region" "investigation-workspace" true "index.html?dupregion#/workspace" stateFixtures

let checks =
    [
        "bound screen with async state hook passes every required kind",
        (verdictFor positive = Binding.Pass),
        describe positive
        "async state injection is awaited (state projection passes)",
        (statusOf StateProjection positive = Passed),
        describe positive
        "keyboard reachability passes on the conforming screen",
        (statusOf Accessibility positive = Passed),
        describe positive
        "NEGATIVE CONTROL: wrong screen fails identity for every kind",
        (wrongScreen |> List.forall (fun result -> failedWith "SCREEN-IDENTITY" result.Status)),
        describe wrongScreen
        "NEGATIVE CONTROL: wrong screen verdict is FAIL",
        (match verdictFor wrongScreen with Binding.Fail _ -> true | _ -> false),
        describe wrongScreen
        "NEGATIVE CONTROL: declared hook missing fails state projection",
        (failedWith "STATE-INJECTION-MISSING" (statusOf StateProjection noHook)),
        describe noHook
        "NEGATIVE CONTROL: declared hook missing fails interaction",
        (failedWith "STATE-INJECTION-MISSING" (statusOf Interaction noHook)),
        describe noHook
        "no declared state injection leaves state projection unavailable, not passed",
        (match statusOf StateProjection declaredNoInjection with Unavailable reason -> reason.Contains("STATE-INJECTION-UNSUPPORTED") | _ -> false),
        describe declaredNoInjection
        "no declared state injection never yields PASS",
        (verdictFor declaredNoInjection <> Binding.Pass),
        describe declaredNoInjection
        "NEGATIVE CONTROL: capability removed from tab order fails accessibility",
        (failedWith "not reachable with the keyboard" (statusOf Accessibility untabbable)),
        describe untabbable
        "NEGATIVE CONTROL: duplicated region boundary fails structural",
        (failedWith "boundaries" (statusOf Structural duplicateRegion)),
        describe duplicateRegion
    ]

let failures = checks |> List.filter (fun (_, passed, _) -> not passed)

for name, passed, detail in checks do
    printfn "%s %s" (if passed then "PASS" else "FAIL") name

    if not passed then
        printfn "     %s" detail

if List.isEmpty failures then
    printfn "Percepta.Binding.Tests: all %d checks passed." checks.Length
else
    eprintfn "Percepta.Binding.Tests: %d failure(s)." failures.Length
    Environment.ExitCode <- 1
