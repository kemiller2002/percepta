open System
open System.IO
open System.Text.Json
open Aegis
open Percepta.RepositoryLifecycle

module Cli =
    let private tryOption name (args: string list) =
        let rec loop remaining =
            match remaining with
            | key :: value :: _ when key = name -> Some value
            | _ :: tail -> loop tail
            | [] -> None

        loop args

    let private hasFlag name (args: string list) =
        args |> List.exists ((=) name)

    let private target args =
        tryOption "--target" args
        |> Option.defaultValue (Directory.GetCurrentDirectory())
        |> Path.GetFullPath

    let private stateName =
        function
        | Uninstalled -> "uninstalled"
        | Current -> "current"
        | UpgradeRequired _ -> "upgrade-required"
        | NewerThanCli _ -> "newer-than-cli"
        | Invalid _ -> "invalid"

    let private installedVersion =
        function
        | UpgradeRequired version
        | NewerThanCli version -> Some version
        | _ -> None

    let private writeInspectionJson (inspection: Inspection) =
        use stream = Console.OpenStandardOutput()
        let mutable options = JsonWriterOptions()
        options.Indented <- true
        use writer = new Utf8JsonWriter(stream, options)
        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", 1)
        writer.WriteString("component", "percepta")
        writer.WriteString("lifecycleVersion", Lifecycle.ToolVersion)
        writer.WriteString("state", stateName inspection.State)

        match installedVersion inspection.State with
        | Some version -> writer.WriteString("installedVersion", version)
        | None ->
            match inspection.Manifest with
            | Some manifest -> writer.WriteString("installedVersion", manifest.InstalledVersion)
            | None -> writer.WriteNull("installedVersion")

        writer.WriteStartArray("errors")

        for error in inspection.Errors do
            writer.WriteStringValue error

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()

    let private writePlanJson (operation: string) (changes: PlannedChange list) =
        use stream = Console.OpenStandardOutput()
        let mutable options = JsonWriterOptions()
        options.Indented <- true
        use writer = new Utf8JsonWriter(stream, options)
        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", 1)
        writer.WriteString("operation", operation)
        writer.WriteString("component", "percepta")
        writer.WriteString("lifecycleVersion", Lifecycle.ToolVersion)
        writer.WriteStartArray("changes")

        for change in changes do
            writer.WriteStringValue(Lifecycle.describeChange change)

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()

    let private printErrors (errors: string list) =
        for error in errors do
            eprintfn "%s" error

    let private applyPlan (operation: string) (args: string list) (plan: Result<PlannedChange list, string list>) =
        let dryRun = hasFlag "--dry-run" args
        let json = hasFlag "--json" args

        match plan with
        | Error errors ->
            if json then
                use stream = Console.OpenStandardOutput()
                let mutable options = JsonWriterOptions()
                options.Indented <- true
                use writer = new Utf8JsonWriter(stream, options)
                writer.WriteStartObject()
                writer.WriteNumber("schemaVersion", 1)
                writer.WriteString("operation", operation)
                writer.WriteString("component", "percepta")
                writer.WriteString("status", "blocked")
                writer.WriteStartArray("errors")

                for error in errors do
                    writer.WriteStringValue error

                writer.WriteEndArray()
                writer.WriteEndObject()
                writer.Flush()
            else
                printErrors errors

            4
        | Ok changes when dryRun ->
            if json then
                writePlanJson operation changes
            else
                printfn "Percepta repository lifecycle %s plan:" operation

                if List.isEmpty changes then
                    printfn "  no changes"
                else
                    for change in changes do
                        printfn "  %s" (Lifecycle.describeChange change)

            0
        | Ok changes ->
            match Lifecycle.apply (target args) changes with
            | Error errors ->
                printErrors errors
                4
            | Ok() ->
                match Lifecycle.verify (target args) with
                | Error errors ->
                    printErrors errors
                    3
                | Ok() ->
                    if json then
                        writeInspectionJson (Lifecycle.inspect (target args))
                    else
                        printfn "Percepta repository lifecycle %s complete. version=%s changes=%d" operation Lifecycle.ToolVersion changes.Length

                    0

    let init args =
        let root = target args
        Lifecycle.planInit root |> applyPlan "init" args

    let upgrade args =
        let root = target args
        Lifecycle.planUpgrade root |> applyPlan "upgrade" args

    let status args =
        let inspection = Lifecycle.inspect (target args)

        if hasFlag "--json" args then
            writeInspectionJson inspection
        else
            printfn "Percepta repository lifecycle"
            printfn "  version: %s" Lifecycle.ToolVersion
            printfn "  state: %s" (stateName inspection.State)

            match inspection.Manifest with
            | Some manifest -> printfn "  installed: %s" manifest.InstalledVersion
            | None -> printfn "  installed: none"

            for error in inspection.Errors do
                printfn "  error: %s" error

        match inspection.State with
        | Current -> 0
        | Uninstalled
        | UpgradeRequired _
        | NewerThanCli _ -> 3
        | Invalid _ -> 4

    let verify args =
        match Lifecycle.verify (target args) with
        | Ok() ->
            if hasFlag "--json" args then
                writeInspectionJson (Lifecycle.inspect (target args))
            else
                printfn "Percepta repository lifecycle verify passed."

            0
        | Error errors ->
            if hasFlag "--json" args then
                writeInspectionJson (Lifecycle.inspect (target args))
            else
                printErrors errors

            3

    let doctor args =
        let inspection = Lifecycle.inspect (target args)

        let findings =
            match inspection.State with
            | Current -> [ "PERCEPTA-REPO-OK", "Installation is current.", None ]
            | Uninstalled ->
                [ "PERCEPTA-REPO-UNINSTALLED",
                  "Percepta repository lifecycle is not installed.",
                  Some "Run 'percepta-repo init'." ]
            | UpgradeRequired version ->
                [ "PERCEPTA-REPO-UPGRADE",
                  $"Percepta repository lifecycle {version} requires upgrade to {Lifecycle.ToolVersion}.",
                  Some "Run 'percepta-repo upgrade'." ]
            | NewerThanCli version ->
                [ "PERCEPTA-REPO-NEWER",
                  $"Repository was installed by newer lifecycle {version}.",
                  Some "Install a Percepta repository lifecycle CLI at least as new as the installed version." ]
            | Invalid errors ->
                errors
                |> List.map (fun error ->
                    "PERCEPTA-REPO-INVALID",
                    error,
                    Some "Resolve the named managed-state conflict; use init only when it can prove safe ownership.")

        if hasFlag "--json" args then
            use stream = Console.OpenStandardOutput()
            let mutable options = JsonWriterOptions()
            options.Indented <- true
            use writer = new Utf8JsonWriter(stream, options)
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", 1)
            writer.WriteString("component", "percepta")
            writer.WriteBoolean("healthy", inspection.State = Current)
            writer.WriteStartArray("findings")

            for code, detail, remediation in findings do
                writer.WriteStartObject()
                writer.WriteString("code", code)
                writer.WriteString("detail", detail)

                match remediation with
                | Some value -> writer.WriteString("remediation", value)
                | None -> ()

                writer.WriteEndObject()

            writer.WriteEndArray()
            writer.WriteEndObject()
            writer.Flush()
        else
            printfn "Percepta Repository Doctor"

            for code, detail, remediation in findings do
                printfn "  %s: %s" code detail

                match remediation with
                | Some value -> printfn "    remediation: %s" value
                | None -> ()

        if inspection.State = Current then 0 else 3

    let usage () =
        printfn "Percepta repository lifecycle"
        printfn "  percepta-repo --version"
        printfn "  percepta-repo init [--target PATH] [--dry-run] [--json]"
        printfn "  percepta-repo status [--target PATH] [--json]"
        printfn "  percepta-repo verify [--target PATH] [--strict] [--json]"
        printfn "  percepta-repo doctor [--target PATH] [--json]"
        printfn "  percepta-repo upgrade [--target PATH] [--dry-run] [--json]"

let private execute argv =
    let args = argv |> Array.toList

    match args with
    | [ "--version" ] ->
        printfn "%s" Lifecycle.ToolVersion
        0
    | "init" :: rest -> Cli.init rest
    | "status" :: rest -> Cli.status rest
    | "verify" :: rest -> Cli.verify rest
    | "doctor" :: rest -> Cli.doctor rest
    | "upgrade" :: rest -> Cli.upgrade rest
    | _ ->
        Cli.usage ()
        1

[<EntryPoint>]
let main argv =
    let version =
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
        |> Option.ofObj
        |> Option.map string

    let config = Aegis.configure "Percepta.RepositoryLifecycle" version [ Sinks.console ]

    match Bootstrap.validate None config with
    | Result.Error problems ->
        for problem in problems do
            let _, message = Bootstrap.describe problem
            eprintfn "Aegis configuration error: %s" message

        10
    | Ok validated ->
        let scope = Aegis.scope validated "Percepta.RepositoryLifecycle.Main" Map.empty

        let classify scope ex =
            Aegis.faultOf
                validated
                scope
                (FaultCode "PERCEPTA.REPOSITORY_LIFECYCLE.UNHANDLED")
                UnknownFailure
                FaultSeverity.Error
                DegradedApplication
                RequiresIntervention
                ManualIntervention
                "Percepta repository lifecycle encountered an unexpected operational failure."
                ex

        match Aegis.capture validated scope classify (fun () -> execute argv) with
        | Ok exitCode -> exitCode
        | Result.Error fault ->
            eprintfn "%s Reference %s" fault.UserMessage fault.Id.Value
            10
