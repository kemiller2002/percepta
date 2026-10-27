open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open Percepta.Core
open Percepta.Core.Compilation
open Percepta.Core.Evidence
open Percepta.Core.EvidenceEnvelope
open Percepta.Adapter.Playwright
open Percepta.Cli.EvidenceRuntime
open Aegis

module Cli =

    let screenId (ScreenId value) = value
    let verificationId (VerificationId value) = value

    let rec tryOption name args =
        match args with
        | key :: value :: _ when key = name -> Some value
        | _ :: tail -> tryOption name tail
        | [] -> None

    let allOptions name args =
        let rec loop remaining values =
            match remaining with
            | key :: value :: tail when key = name -> loop tail (value :: values)
            | _ :: tail -> loop tail values
            | [] -> List.rev values

        loop args []

    let hasFlag name args =
        args |> List.exists ((=) name)

    let findRepositoryRoot startDirectory =
        let rec loop (directory: DirectoryInfo) =
            let contracts = Path.Combine(directory.FullName, ".percepta", "contracts")

            if Directory.Exists contracts || Directory.Exists(Path.Combine(directory.FullName, ".git")) then
                directory.FullName
            elif isNull directory.Parent then
                startDirectory
            else
                loop directory.Parent

        loop (DirectoryInfo(Path.GetFullPath(startDirectory)))

    let discoverContracts root =
        let directory = Path.Combine(root, ".percepta", "contracts")

        if Directory.Exists directory then
            Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            |> Array.sort
            |> Array.toList
        else
            []

    let loadContract path =
        let text = File.ReadAllText(path)

        match ContractSerialization.deserialize text with
        | Error errors -> Error errors
        | Ok contract ->
            match Validation.validate contract with
            | [] -> Ok contract
            | issues ->
                issues
                |> List.map (fun issue -> $"Contract validation: {issue}")
                |> Error

    let resolveSingleContract root args =
        match tryOption "--contract" args with
        | Some path ->
            let full =
                if Path.IsPathRooted path then path else Path.GetFullPath(Path.Combine(root, path))

            Ok full
        | None ->
            match discoverContracts root with
            | [ path ] -> Ok path
            | [] -> Error "No Percepta contracts were found under .percepta/contracts."
            | many ->
                Error($"Multiple Percepta contracts were found ({many.Length}); pass --contract explicitly.")

    let private ensureDirectoryForFile (path: string) =
        let directory = Path.GetDirectoryName(path)

        if not (String.IsNullOrWhiteSpace directory) then
            Directory.CreateDirectory(directory) |> ignore

    let private writeVerificationPlan path (compiled: CompiledScreenContract) =
        ensureDirectoryForFile path
        use stream = File.Create(path)
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", 1)
        writer.WriteString("screen", screenId compiled.Contract.Id)
        writer.WritePropertyName("obligations")
        writer.WriteStartArray()

        for obligation in compiled.VerificationPlan do
            writer.WriteStartObject()
            writer.WriteString("id", verificationId obligation.Id)
            writer.WriteString("kind", Compilation.evidenceKindName obligation.Kind)
            writer.WriteString("description", obligation.Description)
            writer.WriteBoolean("required", obligation.Required)
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()

    let compileContract outputRoot contract =
        match Compilation.compile contract with
        | Error issues ->
            Error(issues |> List.map string)
        | Ok compiled ->
            let screen = screenId contract.Id
            let directory = Path.Combine(outputRoot, screen)
            Directory.CreateDirectory(directory) |> ignore
            File.WriteAllText(Path.Combine(directory, "agent-guidance.md"), compiled.AgentGuidance + Environment.NewLine)
            File.WriteAllText(Path.Combine(directory, "normalized-contract.json"), ContractSerialization.serialize contract)
            writeVerificationPlan (Path.Combine(directory, "verification-plan.json")) compiled
            Ok directory

    let private statusName =
        function
        | Passed -> "Passed", None
        | Failed reason -> "Failed", Some reason
        | Unavailable reason -> "Unavailable", Some reason
        | NotApplicable reason -> "NotApplicable", Some reason
        | AcceptedDeviation reason -> "AcceptedDeviation", Some reason

    let private contractHash contract =
        let bytes = Encoding.UTF8.GetBytes(ContractSerialization.serialize contract)
        SHA256.HashData(bytes)
        |> Convert.ToHexString
        |> fun value -> value.ToLowerInvariant()

    type private ExternalReview =
        {
            Status: VerificationStatus
            Summary: string
            References: string list
        }

    let private readExternalReview path =
        try
            use document = JsonDocument.Parse(File.ReadAllText(path))
            let root = document.RootElement

            let mutable statusElement = Unchecked.defaultof<JsonElement>
            let status =
                if root.TryGetProperty("status", &statusElement) && statusElement.ValueKind = JsonValueKind.String then
                    match statusElement.GetString().Trim().ToLowerInvariant() with
                    | "passed" -> Passed
                    | "failed" ->
                        let mutable reason = Unchecked.defaultof<JsonElement>

                        if root.TryGetProperty("reason", &reason) && reason.ValueKind = JsonValueKind.String then
                            Failed(reason.GetString())
                        else
                            Failed "External semantic review failed without a reason."
                    | "unavailable" ->
                        let mutable reason = Unchecked.defaultof<JsonElement>

                        if root.TryGetProperty("reason", &reason) && reason.ValueKind = JsonValueKind.String then
                            Unavailable(reason.GetString())
                        else
                            Unavailable "External semantic review unavailable."
                    | "notapplicable"
                    | "not-applicable" ->
                        NotApplicable "External semantic review marked not applicable."
                    | "accepteddeviation"
                    | "accepted-deviation" ->
                        let mutable reason = Unchecked.defaultof<JsonElement>

                        if root.TryGetProperty("reason", &reason) && reason.ValueKind = JsonValueKind.String then
                            AcceptedDeviation(reason.GetString())
                        else
                            AcceptedDeviation "External semantic review deviation accepted."
                    | other -> Unavailable($"Unknown external semantic review status '{other}'.")
                else
                    Unavailable "External semantic review is missing a status."

            let summary =
                let mutable element = Unchecked.defaultof<JsonElement>

                if root.TryGetProperty("summary", &element) && element.ValueKind = JsonValueKind.String then
                    element.GetString()
                else
                    "External semantic visual review."

            let references =
                let mutable element = Unchecked.defaultof<JsonElement>

                if root.TryGetProperty("evidenceReferences", &element) && element.ValueKind = JsonValueKind.Array then
                    element.EnumerateArray()
                    |> Seq.choose (fun item ->
                        if item.ValueKind = JsonValueKind.String then Some(item.GetString()) else None)
                    |> Seq.toList
                else
                    [ path ]

            {
                Status = status
                Summary = summary
                References = references
            }
        with ex ->
            {
                Status = Unavailable($"Could not read external semantic review: {ex.Message}")
                Summary = "External semantic visual review could not be loaded."
                References = [ path ]
            }

    let private toEvidenceRecord (requirement: EvidenceRequirement) (source: string) (status: VerificationStatus) (summary: string) (references: string list) : EvidenceRecord =
        {
            Requirement = requirement.Id
            Kind = requirement.Kind
            Required = requirement.Required
            Status = status
            Summary = summary
            EvidenceReferences = references
            Source = source
        }

    let compileCommand root args =
        let outputRoot =
            tryOption "--out" args
            |> Option.map (fun value -> if Path.IsPathRooted value then value else Path.Combine(root, value))
            |> Option.defaultValue (Path.Combine(root, "artifacts", "percepta"))

        let paths =
            match tryOption "--contract" args with
            | Some path ->
                [ if Path.IsPathRooted path then path else Path.Combine(root, path) ]
            | None -> discoverContracts root

        if List.isEmpty paths then
            eprintfn "No Percepta contracts found."
            2
        else
            let mutable failed = false

            for path in paths do
                match loadContract path with
                | Error errors ->
                    failed <- true
                    eprintfn "Contract %s is invalid:" path

                    for error in errors do
                        eprintfn "  %s" error
                | Ok contract ->
                    match compileContract outputRoot contract with
                    | Error errors ->
                        failed <- true

                        for error in errors do
                            eprintfn "  %s" error
                    | Ok directory ->
                        printfn "Compiled %s -> %s" (screenId contract.Id) directory

            if failed then 2 else 0

    let verifyCommand root args =
        task {
            match resolveSingleContract root args with
            | Error error ->
                eprintfn "%s" error
                return 2
            | Ok contractPath ->
                match loadContract contractPath with
                | Error errors ->
                    for error in errors do
                        eprintfn "%s" error

                    return 2
                | Ok contract ->
                    match Compilation.compile contract with
                    | Error issues ->
                        for issue in issues do
                            eprintfn "%A" issue

                        return 2
                    | Ok _ ->
                        let screen = screenId contract.Id

                        match tryOption "--url" args with
                        | None ->
                            eprintfn "verify requires --url <rendered-page-url-or-file>."
                            return 2
                        | Some target ->
                            let fixtures =
                                allOptions "--fixture" args
                                |> List.map (fun path ->
                                    if Path.IsPathRooted path then path else Path.Combine(root, path))

                            let evidenceRoot = Path.Combine(root, ".percepta", "evidence", screen)

                            let screenshots =
                                tryOption "--screenshots" args
                                |> Option.map (fun path -> if Path.IsPathRooted path then path else Path.Combine(root, path))
                                |> Option.defaultValue (Path.Combine(evidenceRoot, "screenshots"))

                            let baselines =
                                tryOption "--baselines" args
                                |> Option.map (fun path -> if Path.IsPathRooted path then path else Path.Combine(root, path))
                                |> Option.defaultValue (Path.Combine(root, ".percepta", "baselines", screen))

                            let url =
                                if Uri.IsWellFormedUriString(target, UriKind.Absolute) then
                                    target
                                else
                                    Path.Combine(root, target)

                            let semanticReviewPath =
                                tryOption "--semantic-review" args
                                |> Option.map (fun path ->
                                    if Path.IsPathRooted path then path else Path.Combine(root, path))

                            let semanticReview = semanticReviewPath |> Option.map readExternalReview

                            let options: BrowserVerification.Options =
                                {
                                    Url = url
                                    StateFixturePaths = fixtures
                                    ScreenshotDirectory = screenshots
                                    BaselineDirectory = baselines
                                    UpdateBaselines = hasFlag "--update-baselines" args
                                }

                            match sourceIdentity root, artifactMetadata () with
                            | Error error, _
                            | _, Error error ->
                                eprintfn "%s" error
                                return 2
                            | Ok(sourceRoot, repository, sourceRevision), Ok artifactMetadata ->
                                match
                                    collectInputs
                                        sourceRoot
                                        sourceRevision
                                        contractPath
                                        url
                                        fixtures
                                        semanticReviewPath
                                        baselines
                                with
                                | Error error ->
                                    eprintfn "%s" error
                                    return 2
                                | Ok inputs ->
                                    let! adapterResults = BrowserVerification.verify options contract

                                    let resultForRequirement (requirement: EvidenceRequirement) =
                                        match requirement.Kind with
                                        | ContractValidation ->
                                            toEvidenceRecord
                                                requirement
                                                "percepta-core"
                                                Passed
                                                "Typed contract parsed and validated successfully."
                                                [ contractPath ]
                                        | SemanticVisualReview ->
                                            match semanticReview with
                                            | Some review ->
                                                toEvidenceRecord
                                                    requirement
                                                    "external-semantic-review"
                                                    review.Status
                                                    review.Summary
                                                    review.References
                                            | None ->
                                                toEvidenceRecord
                                                    requirement
                                                    "external-semantic-review"
                                                    (Unavailable "No semantic visual review evidence was supplied.")
                                                    "Semantic visual review is supporting evidence and was not supplied."
                                                    []
                                        | kind ->
                                            match adapterResults |> List.tryFind (fun result -> result.Kind = kind) with
                                            | Some result ->
                                                toEvidenceRecord
                                                    requirement
                                                    "playwright"
                                                    result.Status
                                                    result.Summary
                                                    result.EvidenceReferences
                                            | None ->
                                                toEvidenceRecord
                                                    requirement
                                                    "percepta"
                                                    (Unavailable $"No adapter produced evidence for {Compilation.evidenceKindName kind}.")
                                                    "Required verification adapter evidence is unavailable."
                                                    []

                                    let records =
                                        contract.EvidenceRequirements |> List.map resultForRequirement

                                    let references =
                                        evidenceReferences sourceRoot sourceRevision inputs records

                                    match relativePath sourceRoot contractPath with
                                    | Error error ->
                                        eprintfn "%s" error
                                        return 2
                                    | Ok contractReference ->
                                        let configuration =
                                            {
                                                ContractReference = contractReference
                                                ContractSha256 = contractHash contract
                                                UpdateBaselines = options.UpdateBaselines
                                            }

                                        match
                                            Percepta.Cli.EvidenceRuntime.createReport
                                                sourceRoot
                                                repository
                                                sourceRevision
                                                screen
                                                (DateTimeOffset.UtcNow.ToString("O"))
                                                contract.Id
                                                inputs
                                                configuration
                                                artifactMetadata.Artifact
                                                artifactMetadata.Dependencies
                                                records
                                                references
                                        with
                                        | Error error ->
                                            eprintfn "%s" error
                                            return 2
                                        | Ok report ->
                                            let output =
                                                tryOption "--out" args
                                                |> Option.map (fun path ->
                                                    if Path.IsPathRooted path then path else Path.Combine(root, path))
                                                |> Option.defaultValue (
                                                    Path.Combine(root, ".percepta", "evidence", $"{screen}.evidence.json")
                                                )

                                            ensureDirectoryForFile output
                                            File.WriteAllText(output, EvidenceEnvelope.serialize report)

                                            for result in report.EvaluationResults do
                                                let status, _ = statusName result.Status
                                                let marker = if result.Required then "*" else " "
                                                printfn
                                                    "%s %-24s %-18s %s"
                                                    marker
                                                    (verificationId result.Requirement)
                                                    status
                                                    result.Summary

                                            printfn "Evidence: %s" output
                                            printfn "Evidence digest: %s" report.EvidenceDigest
                                            printfn "Complete: %b" (report.Outcome = Satisfied)

                                            return if report.Outcome = Satisfied then 0 else 3
        }

    /// Maps adapter evidence onto a contract's declared evidence requirements (no semantic review is
    /// supplied in an application run, so that evidence is Unavailable, never assumed).
    let private recordsFor (contract: ScreenContract) contractPath (adapterResults: BrowserVerification.AdapterEvidence list) =
        contract.EvidenceRequirements
        |> List.map (fun requirement ->
            match requirement.Kind with
            | ContractValidation ->
                toEvidenceRecord requirement "percepta-core" Passed "Typed contract parsed and validated successfully." [ contractPath ]
            | SemanticVisualReview ->
                toEvidenceRecord
                    requirement
                    "external-semantic-review"
                    (Unavailable "No semantic visual review evidence was supplied.")
                    "Semantic visual review is not part of an application run."
                    []
            | kind ->
                match adapterResults |> List.tryFind (fun result -> result.Kind = kind) with
                | Some result -> toEvidenceRecord requirement "playwright" result.Status result.Summary result.EvidenceReferences
                | None ->
                    toEvidenceRecord
                        requirement
                        "percepta"
                        (Unavailable $"No adapter produced evidence for {Compilation.evidenceKindName kind}.")
                        "Required verification adapter evidence is unavailable."
                        [])

    type private ScreenRun =
        {
            ContractScreen: string
            ContractPath: string
            Verdict: Binding.ScreenVerdict
            AppScreen: string option
            Url: string option
            Records: EvidenceRecord list
        }

    let private writeApplicationReport (path: string) (application: string) (baseUrl: string) allowNotApplicable (orphans: string list) complete (runs: ScreenRun list) =
        ensureDirectoryForFile path
        use stream = File.Create(path)
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        writer.WriteStartObject()
        writer.WriteString("schema", "percepta.application-verification/1")
        writer.WriteString("application", application)
        writer.WriteString("baseUrl", baseUrl)
        // A served application is not a revision-bound input; this report is not a v1 evidence envelope.
        writer.WriteBoolean("revisionBoundEvidence", false)
        writer.WriteBoolean("allowNotApplicable", allowNotApplicable)
        writer.WriteBoolean("complete", complete)
        writer.WriteStartObject("counts")

        for name in [ "PASS"; "FAIL"; "NOT-BOUND"; "NOT-APPLICABLE" ] do
            writer.WriteNumber(name, runs |> List.filter (fun run -> Binding.verdictName run.Verdict = name) |> List.length)

        writer.WriteEndObject()
        writer.WriteStartArray("orphanBindings")
        orphans |> List.iter writer.WriteStringValue
        writer.WriteEndArray()
        writer.WriteStartArray("screens")

        for run in runs do
            writer.WriteStartObject()
            writer.WriteString("contract", run.ContractScreen)
            writer.WriteString("contractPath", run.ContractPath)
            writer.WriteString("verdict", Binding.verdictName run.Verdict)

            match run.Verdict with
            | Binding.Fail reason
            | Binding.NotApplicableVerdict reason -> writer.WriteString("reason", reason)
            | Binding.NotBoundVerdict -> writer.WriteString("reason", "The application's bindings do not mention this contract.")
            | Binding.Pass -> ()

            run.AppScreen |> Option.iter (fun value -> writer.WriteString("appScreen", value))
            run.Url |> Option.iter (fun value -> writer.WriteString("url", value))
            writer.WriteStartArray("evidence")

            for record in run.Records do
                let status, reason = statusName record.Status
                writer.WriteStartObject()
                writer.WriteString("requirement", verificationId record.Requirement)
                writer.WriteString("kind", Compilation.evidenceKindName record.Kind)
                writer.WriteBoolean("required", record.Required)
                writer.WriteString("status", status)
                reason |> Option.iter (fun value -> writer.WriteString("reason", value))
                writer.WriteString("summary", record.Summary)
                writer.WriteStartArray("references")
                record.EvidenceReferences |> List.iter writer.WriteStringValue
                writer.WriteEndArray()
                writer.WriteEndObject()

            writer.WriteEndArray()
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()

    /// Verifies every contract under the contracts directory against the application screens named in
    /// its bindings (docs/SCREEN-BINDING.md). Exit 0 only when the run is complete.
    let verifyAppCommand root args =
        task {
            let resolvePath (path: string) = if Path.IsPathRooted path then path else Path.GetFullPath(Path.Combine(root, path))

            let bindingsPath =
                tryOption "--bindings" args |> Option.map resolvePath |> Option.defaultValue (Path.Combine(root, ".percepta", "bindings.json"))

            let contractsDirectory =
                tryOption "--contracts" args |> Option.map resolvePath |> Option.defaultValue (Path.Combine(root, ".percepta", "contracts"))

            let allowNotApplicable = hasFlag "--allow-not-applicable" args

            let readyTimeout =
                tryOption "--ready-timeout-ms" args
                |> Option.bind (fun value ->
                    match Double.TryParse(value, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                    | true, parsed when parsed > 0.0 -> Some parsed
                    | _ -> None)
                |> Option.defaultValue 30000.0

            match tryOption "--base-url" args, File.Exists bindingsPath, Directory.Exists contractsDirectory with
            | None, _, _ ->
                eprintfn "verify-app requires --base-url <url> (the served application, e.g. http://127.0.0.1:4321/)."
                return 2
            | _, false, _ ->
                eprintfn "Screen bindings not found: %s (see docs/SCREEN-BINDING.md)." bindingsPath
                return 2
            | _, _, false ->
                eprintfn "Contracts directory not found: %s" contractsDirectory
                return 2
            | Some baseUrlText, true, true ->
                match Binding.parse (File.ReadAllText bindingsPath) with
                | Error errors ->
                    eprintfn "Screen bindings are invalid (%s):" bindingsPath
                    errors |> List.iter (eprintfn "  %s")
                    return 2
                | Ok bindings ->
                    let baseUrl =
                        let normalized =
                            match Uri.TryCreate(baseUrlText, UriKind.Absolute) with
                            | true, uri -> uri.AbsoluteUri
                            | _ -> (Path.GetFullPath(baseUrlText) |> Uri).AbsoluteUri

                        if normalized.EndsWith("/", StringComparison.Ordinal) then normalized else normalized + "/"

                    let bindingsDirectory = Path.GetDirectoryName(bindingsPath)
                    let contractPaths = Directory.GetFiles(contractsDirectory, "*.json") |> Array.sort |> Array.toList
                    let loaded = contractPaths |> List.map (fun path -> path, loadContract path)

                    let runOne (path: string, result: Result<ScreenContract, string list>) =
                        task {
                            match result with
                            | Error errors ->
                                return
                                    {
                                        ContractScreen = Path.GetFileNameWithoutExtension path
                                        ContractPath = path
                                        Verdict = Binding.Fail("INVALID-CONTRACT: " + String.concat " | " errors)
                                        AppScreen = None
                                        Url = None
                                        Records = []
                                    }
                            | Ok contract ->
                                let screen = screenId contract.Id
                                let resolution = Binding.resolve bindings contract.Id

                                match resolution with
                                | Binding.ResolvedBound bound ->
                                    let url = Uri(Uri(baseUrl), bound.Path).AbsoluteUri
                                    let evidenceRoot = Path.Combine(root, ".percepta", "evidence", "application", screen)

                                    let options: BrowserVerification.Options =
                                        {
                                            Url = url
                                            StateFixturePaths =
                                                bound.Fixtures
                                                |> List.map (fun fixture ->
                                                    if Path.IsPathRooted fixture then fixture else Path.GetFullPath(Path.Combine(bindingsDirectory, fixture)))
                                            ScreenshotDirectory = Path.Combine(evidenceRoot, "screenshots")
                                            BaselineDirectory = Path.Combine(root, ".percepta", "baselines", screen)
                                            UpdateBaselines = false
                                        }

                                    let missingFixtures = options.StateFixturePaths |> List.filter (File.Exists >> not)

                                    if not (List.isEmpty missingFixtures) then
                                        return
                                            {
                                                ContractScreen = screen
                                                ContractPath = path
                                                Verdict = Binding.Fail("Bound state fixtures are missing: " + String.concat ", " missingFixtures)
                                                AppScreen = Some bound.AppScreen
                                                Url = Some url
                                                Records = []
                                            }
                                    else
                                        let target: BrowserVerification.ScreenTarget =
                                            {
                                                AppScreen = bound.AppScreen
                                                StateInjection = bindings.StateInjection
                                                ReadyTimeoutMs = readyTimeout
                                            }

                                        let! adapterResults = BrowserVerification.verifyBound target options contract
                                        let records = recordsFor contract path adapterResults

                                        return
                                            {
                                                ContractScreen = screen
                                                ContractPath = path
                                                Verdict = Binding.verdict resolution records
                                                AppScreen = Some bound.AppScreen
                                                Url = Some url
                                                Records = records
                                            }
                                | _ ->
                                    return
                                        {
                                            ContractScreen = screen
                                            ContractPath = path
                                            Verdict = Binding.verdict resolution []
                                            AppScreen = None
                                            Url = None
                                            Records = []
                                        }
                        }

                    let runs = ResizeArray<ScreenRun>()

                    for item in loaded do
                        let! run = runOne item
                        runs.Add run

                    let runs = List.ofSeq runs

                    let orphans =
                        loaded
                        |> List.choose (fun (_, result) -> result |> Result.toOption |> Option.map _.Id)
                        |> Binding.orphans bindings
                        |> List.map screenId

                    let complete = Binding.complete allowNotApplicable orphans.Length (runs |> List.map _.Verdict)

                    let output =
                        tryOption "--out" args
                        |> Option.map resolvePath
                        |> Option.defaultValue (Path.Combine(root, ".percepta", "evidence", "application-verification.json"))

                    writeApplicationReport output bindings.Application baseUrl allowNotApplicable orphans complete runs

                    for run in runs do
                        let detail =
                            match run.Verdict with
                            | Binding.Fail reason
                            | Binding.NotApplicableVerdict reason -> reason
                            | Binding.NotBoundVerdict -> "not mentioned in the application's bindings"
                            | Binding.Pass -> defaultArg run.AppScreen ""

                        printfn "%-15s %-40s %s" (Binding.verdictName run.Verdict) run.ContractScreen detail

                    for orphan in orphans do
                        printfn "%-15s %-40s %s" "ORPHAN-BINDING" orphan "binding names a contract that does not exist"

                    printfn "Report: %s" output
                    printfn "Complete: %b" complete
                    return if complete then 0 else 3
        }

    let artifactMetadataCommand args =
        try
            match tryOption "--out" args, Percepta.Cli.EvidenceRuntime.artifactMetadata () with
            | _, Error error ->
                eprintfn "%s" error
                2
            | output, Ok metadata ->
                let json = EvidenceEnvelope.serializeArtifactMetadata metadata

                match output with
                | None -> printfn "%s" json
                | Some path ->
                    let full = Path.GetFullPath(path)
                    ensureDirectoryForFile full
                    File.WriteAllText(full, json)
                    printfn "Artifact metadata: %s" full

                0
        with ex ->
            eprintfn "Cannot emit Percepta artifact metadata: %s" ex.Message
            2

    let validateEvidenceCommand root args =
        let evidencePath =
            tryOption "--evidence" args
            |> Option.map (fun path -> if Path.IsPathRooted path then path else Path.Combine(root, path))

        let outputPath =
            tryOption "--out" args
            |> Option.map (fun path -> if Path.IsPathRooted path then path else Path.Combine(root, path))

        let sourceRoot =
            tryOption "--source-root" args
            |> Option.map Path.GetFullPath
            |> Option.defaultValue root

        match evidencePath with
        | None ->
            eprintfn "validate-evidence requires --evidence <path>."
            2
        | Some path when not (File.Exists path) ->
            let result =
                {
                    Status = Invalid
                    Findings =
                        [
                            {
                                Code = "PERCEPTA.EVIDENCE.PACKAGE_MISSING"
                                Message = $"Evidence package is unavailable: {path}"
                            }
                        ]
                }

            writeValidationResult outputPath path result
            3
        | Some path ->
            match EvidenceEnvelope.deserialize (File.ReadAllText(path)) with
            | Error errors ->
                let result =
                    {
                        Status = Invalid
                        Findings =
                            errors
                            |> List.map (fun message ->
                                {
                                    Code = "PERCEPTA.EVIDENCE.SCHEMA_INVALID"
                                    Message = message
                                })
                    }

                writeValidationResult outputPath path result
                3
            | Ok report ->
                match sourceIdentity sourceRoot with
                | Error error ->
                    let result =
                        {
                            Status = Unverifiable
                            Findings =
                                [
                                    {
                                        Code = "PERCEPTA.EVIDENCE.SOURCE_TRUST_UNAVAILABLE"
                                        Message = error
                                    }
                                ]
                        }

                    writeValidationResult outputPath path result
                    4
                | Ok(actualRoot, repository, revision) ->
                    let trustedArtifact =
                        tryOption "--trusted-artifact-metadata" args
                        |> Option.bind (fun metadataPath ->
                            let resolved =
                                if Path.IsPathRooted metadataPath then
                                    metadataPath
                                else
                                    Path.Combine(root, metadataPath)

                            if File.Exists resolved then
                                match EvidenceEnvelope.deserializeArtifactMetadata (File.ReadAllText(resolved)) with
                                | Ok metadata -> Some metadata
                                | Error _ -> None
                            else
                                None)

                    let baseResult =
                        EvidenceEnvelope.validate
                            {
                                SourceRoot = actualRoot
                                Repository = repository
                                SourceRevision = revision
                                TrustedArtifact = trustedArtifact
                            }
                            report

                    let revisionFindings =
                        inputRevisionMismatches actualRoot revision report.Inputs
                        |> List.map (fun message ->
                            {
                                Code = "PERCEPTA.EVIDENCE.INPUT_SOURCE_MISMATCH"
                                Message = message
                            })

                    let result =
                        if List.isEmpty revisionFindings then
                            baseResult
                        else
                            {
                                Status = Invalid
                                Findings = baseResult.Findings @ revisionFindings
                            }

                    writeValidationResult outputPath path result

                    match result.Status with
                    | Valid -> 0
                    | Invalid -> 3
                    | Unverifiable -> 4

    let doctorCommand root args =
        task {
            let contracts =
                match tryOption "--contract" args with
                | Some path -> [ if Path.IsPathRooted path then path else Path.Combine(root, path) ]
                | None -> discoverContracts root

            let mutable healthy = true

            if List.isEmpty contracts then
                healthy <- false
                eprintfn "No Percepta contracts found."

            for path in contracts do
                match loadContract path with
                | Ok contract -> printfn "Contract OK: %s (%s)" path (screenId contract.Id)
                | Error errors ->
                    healthy <- false
                    eprintfn "Contract FAILED: %s" path

                    for error in errors do
                        eprintfn "  %s" error

            let! browser = BrowserVerification.doctor ()

            match browser with
            | Ok message -> printfn "Browser OK: %s" message
            | Error error ->
                healthy <- false
                eprintfn "Browser FAILED: %s" error

            return if healthy then 0 else 4
        }

    let statusCommand root args =
        let explicitEvidence =
            tryOption "--evidence" args
            |> Option.map (fun path -> if Path.IsPathRooted path then path else Path.Combine(root, path))

        let files =
            match explicitEvidence with
            | Some path -> [ path ]
            | None ->
                let directory = Path.Combine(root, ".percepta", "evidence")

                if Directory.Exists directory then
                    Directory.GetFiles(directory, "*.evidence.json", SearchOption.AllDirectories)
                    |> Array.sort
                    |> Array.toList
                else
                    []

        if List.isEmpty files then
            printfn "No Percepta evidence reports found."
            1
        else
            for file in files do
                try
                    use document = JsonDocument.Parse(File.ReadAllText(file))
                    let rootElement = document.RootElement
                    let screen = rootElement.GetProperty("screen").GetString()
                    let complete = rootElement.GetProperty("complete").GetBoolean()
                    printfn "%s  complete=%b  %s" screen complete file
                with ex ->
                    eprintfn "Could not read %s: %s" file ex.Message

            0

    let usage () =
        printfn "Percepta CLI"
        printfn "  percepta compile [--contract path] [--out directory]"
        printfn "  percepta verify [--contract path] --url url-or-file [--fixture path ...] [--semantic-review path] [--update-baselines]"
        printfn "  percepta verify-app --base-url url [--bindings path] [--contracts directory] [--out path] [--allow-not-applicable] [--ready-timeout-ms n]"
        printfn "  percepta validate-evidence --evidence path --source-root path --trusted-artifact-metadata path [--out path]"
        printfn "  percepta artifact-metadata [--out path]"
        printfn "  percepta status [--evidence path]"
        printfn "  percepta doctor [--contract path]"
        printfn "  percepta install-browser"
        printfn "  percepta version"

    /// The released version line (PERCEPTA-SEMCLI-001); a source build reports the project default.
    let versionCommand () =
        let assembly = System.Reflection.Assembly.GetExecutingAssembly()

        let version =
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(
                assembly
            )
            |> Option.ofObj
            |> Option.map _.InformationalVersion
            |> Option.defaultValue "unknown"

        printfn "percepta %s" version
        0

let private execute argv =
    let root = Cli.findRepositoryRoot (Directory.GetCurrentDirectory())
    let args = argv |> Array.toList

    match args with
    | "compile" :: rest -> Cli.compileCommand root rest
    | "verify" :: rest -> Cli.verifyCommand root rest |> fun task -> task.GetAwaiter().GetResult()
    | "verify-app" :: rest -> Cli.verifyAppCommand root rest |> fun task -> task.GetAwaiter().GetResult()
    | "validate-evidence" :: rest -> Cli.validateEvidenceCommand root rest
    | "artifact-metadata" :: rest -> Cli.artifactMetadataCommand rest
    | "status" :: rest -> Cli.statusCommand root rest
    | "doctor" :: rest -> Cli.doctorCommand root rest |> fun task -> task.GetAwaiter().GetResult()
    | [ "install-browser" ] -> BrowserVerification.installChromium ()
    | [ "version" ]
    | [ "--version" ] -> Cli.versionCommand ()
    | _ ->
        Cli.usage ()
        1

[<EntryPoint>]
let main argv =
    let version =
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
        |> Option.ofObj
        |> Option.map string

    let config = Aegis.configure "Percepta.Cli" version [ Sinks.console ]

    match Bootstrap.validate None config with
    | Result.Error problems ->
        for problem in problems do
            let _, message = Bootstrap.describe problem
            eprintfn "Aegis configuration error: %s" message

        10
    | Ok validated ->
        let scope = Aegis.scope validated "Percepta.Cli.Main" Map.empty

        let classify scope ex =
            Aegis.faultOf
                validated
                scope
                (FaultCode "PERCEPTA.CLI.UNHANDLED")
                UnknownFailure
                FaultSeverity.Error
                DegradedApplication
                RequiresIntervention
                ManualIntervention
                "Percepta encountered an unexpected operational failure."
                ex

        match Aegis.capture validated scope classify (fun () -> execute argv) with
        | Ok exitCode -> exitCode
        | Result.Error fault ->
            eprintfn "%s Reference %s" fault.UserMessage fault.Id.Value
            10
