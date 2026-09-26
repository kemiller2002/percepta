open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open Percepta.Core
open Percepta.Core.Compilation
open Percepta.Core.Evidence
open Percepta.Adapter.Playwright
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

    let private writeEvidenceReport path (report: EvidenceReport) =
        ensureDirectoryForFile path
        use stream = File.Create(path)
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", report.SchemaVersion)
        writer.WriteString("screen", screenId report.Screen)
        writer.WriteString("contractSha256", report.ContractSha256)
        writer.WriteString("generatedAtUtc", report.GeneratedAtUtc)
        writer.WriteBoolean("complete", report.Complete)
        writer.WritePropertyName("results")
        writer.WriteStartArray()

        for result in report.Results do
            let status, reason = statusName result.Status
            writer.WriteStartObject()
            writer.WriteString("requirement", verificationId result.Requirement)
            writer.WriteString("kind", Compilation.evidenceKindName result.Kind)
            writer.WriteBoolean("required", result.Required)
            writer.WriteString("status", status)

            match reason with
            | Some value -> writer.WriteString("reason", value)
            | None -> ()

            writer.WriteString("summary", result.Summary)
            writer.WriteString("source", result.Source)
            writer.WritePropertyName("evidenceReferences")
            writer.WriteStartArray()

            for reference in result.EvidenceReferences do
                writer.WriteStringValue(reference)

            writer.WriteEndArray()
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()

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

                            let options: BrowserVerification.Options =
                                {
                                    Url = url
                                    StateFixturePaths = fixtures
                                    ScreenshotDirectory = screenshots
                                    BaselineDirectory = baselines
                                    UpdateBaselines = hasFlag "--update-baselines" args
                                }

                            let! adapterResults = BrowserVerification.verify options contract

                            let semanticReview =
                                tryOption "--semantic-review" args
                                |> Option.map (fun path ->
                                    let resolved =
                                        if Path.IsPathRooted path then path else Path.Combine(root, path)

                                    readExternalReview resolved)

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

                            let report =
                                Evidence.createReport
                                    (contractHash contract)
                                    (DateTimeOffset.UtcNow.ToString("O"))
                                    contract
                                    records

                            let output =
                                tryOption "--out" args
                                |> Option.map (fun path -> if Path.IsPathRooted path then path else Path.Combine(root, path))
                                |> Option.defaultValue (Path.Combine(root, ".percepta", "evidence", $"{screen}.evidence.json"))

                            writeEvidenceReport output report

                            for result in report.Results do
                                let status, _ = statusName result.Status
                                let marker = if result.Required then "*" else " "
                                printfn "%s %-24s %-18s %s" marker (verificationId result.Requirement) status result.Summary

                            printfn "Evidence: %s" output
                            printfn "Complete: %b" report.Complete

                            return if report.Complete then 0 else 3
        }

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
        printfn "  percepta status [--evidence path]"
        printfn "  percepta doctor [--contract path]"
        printfn "  percepta install-browser"

let private execute argv =
    let root = Cli.findRepositoryRoot (Directory.GetCurrentDirectory())
    let args = argv |> Array.toList

    match args with
    | "compile" :: rest -> Cli.compileCommand root rest
    | "verify" :: rest -> Cli.verifyCommand root rest |> fun task -> task.GetAwaiter().GetResult()
    | "status" :: rest -> Cli.statusCommand root rest
    | "doctor" :: rest -> Cli.doctorCommand root rest |> fun task -> task.GetAwaiter().GetResult()
    | [ "install-browser" ] -> BrowserVerification.installChromium ()
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
