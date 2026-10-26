namespace Percepta.Cli

open System
open System.Diagnostics
open System.IO
open System.Reflection
open System.Text.Json
open Percepta.Core
open Percepta.Core.Evidence
open Percepta.Core.EvidenceEnvelope

module EvidenceRuntime =

    let private normalizeSlashes (value: string) = value.Replace('\\', '/')

    let private normalizeRepository (value: string) =
        let trimmed = value.Trim().TrimEnd('/')
        let withoutGit =
            if trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase) then
                trimmed.Substring(0, trimmed.Length - 4)
            else
                trimmed

        if withoutGit.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase) then
            withoutGit.Substring("git@github.com:".Length)
        elif withoutGit.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) then
            withoutGit.Substring("https://github.com/".Length)
        elif withoutGit.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase) then
            withoutGit.Substring("http://github.com/".Length)
        else
            withoutGit

    let private runGit root arguments =
        try
            let start = ProcessStartInfo("git")
            start.WorkingDirectory <- root
            start.RedirectStandardOutput <- true
            start.RedirectStandardError <- true
            start.UseShellExecute <- false

            for argument in arguments do
                start.ArgumentList.Add(argument)

            use child = Process.Start(start)
            let output = child.StandardOutput.ReadToEnd()
            let error = child.StandardError.ReadToEnd()
            child.WaitForExit()

            if child.ExitCode = 0 then
                Ok(output.Trim())
            else
                Error(error.Trim())
        with ex ->
            Error ex.Message

    let private runGitBytes root arguments =
        try
            let start = ProcessStartInfo("git")
            start.WorkingDirectory <- root
            start.RedirectStandardOutput <- true
            start.RedirectStandardError <- true
            start.UseShellExecute <- false

            for argument in arguments do
                start.ArgumentList.Add(argument)

            use child = Process.Start(start)
            use output = new MemoryStream()
            child.StandardOutput.BaseStream.CopyTo(output)
            let error = child.StandardError.ReadToEnd()
            child.WaitForExit()

            if child.ExitCode = 0 then
                Ok(output.ToArray())
            else
                Error(error.Trim())
        with ex ->
            Error ex.Message

    let sourceIdentity root =
        match runGit root [ "rev-parse"; "--show-toplevel" ] with
        | Error error -> Error($"Cannot establish the source repository: {error}")
        | Ok gitRoot ->
            match runGit gitRoot [ "rev-parse"; "HEAD" ], runGit gitRoot [ "config"; "--get"; "remote.origin.url" ] with
            | Ok revision, Ok repository when not (String.IsNullOrWhiteSpace repository) ->
                Ok(gitRoot, normalizeRepository repository, revision)
            | Error error, _ -> Error($"Cannot establish the source revision: {error}")
            | _, Error error -> Error($"Cannot establish the source repository identity: {error}")
            | _ -> Error "Cannot establish the source repository identity."

    let relativePath root path =
        let root = Path.GetFullPath(root)
        let full = Path.GetFullPath(path)
        let relative = Path.GetRelativePath(root, full) |> normalizeSlashes

        if relative = ".." || relative.StartsWith("../", StringComparison.Ordinal) then
            Error($"Path is outside the source repository: {path}")
        else
            Ok relative

    let private informationalVersion (assembly: Assembly) =
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        |> Option.ofObj
        |> Option.map _.InformationalVersion
        |> Option.defaultValue ""

    let private artifactVersionAndRevision () =
        let information = Assembly.GetExecutingAssembly() |> informationalVersion
        let separator = information.IndexOf('+')

        if separator <= 0 || separator = information.Length - 1 then
            Error(
                "The running Percepta artifact lacks source-revision build metadata; build or release it from a Git checkout."
            )
        else
            Ok(information.Substring(0, separator), information.Substring(separator + 1))

    let private dependencyFiles =
        [
            "FSharp.Core", "FSharp.Core.dll"
            "Microsoft.Playwright", "Microsoft.Playwright.dll"
            "EchelonFoundry.Aegis.Core", "Aegis.Core.dll"
        ]

    let private dependencies (assemblyPath: string) =
        try
            let baseDirectory = Path.GetDirectoryName(assemblyPath)
            let depsPath = Path.ChangeExtension(assemblyPath, ".deps.json")

            if not (File.Exists depsPath) then
                Error($"Resolved dependency manifest is unavailable: {depsPath}")
            else
                use document = JsonDocument.Parse(File.ReadAllText(depsPath))
                let libraries = document.RootElement.GetProperty("libraries")

                let resolveVersion name =
                    libraries.EnumerateObject()
                    |> Seq.tryPick (fun property ->
                        let separator = property.Name.LastIndexOf('/')

                        if separator > 0
                           && property.Name.Substring(0, separator).Equals(name, StringComparison.OrdinalIgnoreCase) then
                            Some(property.Name.Substring(separator + 1))
                        else
                            None)

                dependencyFiles
                |> List.map (fun (name, fileName) ->
                    let file = Path.Combine(baseDirectory, fileName)

                    match resolveVersion name with
                    | None -> Error($"Resolved dependency version is unavailable for {name}.")
                    | Some _ when not (File.Exists file) -> Error($"Resolved dependency artifact is unavailable: {file}")
                    | Some version ->
                        Ok
                            {
                                Name = name
                                Version = version
                                Digest = Some(sha256File file)
                                DigestAlgorithm = Some Sha256Algorithm
                            })
                |> List.fold (fun state item ->
                    match state, item with
                    | Error error, _ -> Error error
                    | _, Error error -> Error error
                    | Ok values, Ok value -> Ok(value :: values)) (Ok [])
                |> Result.map (List.sortBy _.Name)
        with ex ->
            Error($"Cannot resolve dependency identities: {ex.Message}")

    let artifactMetadata () =
        try
            let assembly = Assembly.GetExecutingAssembly()
            let assemblyPath = assembly.Location

            if String.IsNullOrWhiteSpace assemblyPath || not (File.Exists assemblyPath) then
                Error "The running Percepta assembly cannot be located for hashing."
            else
                match artifactVersionAndRevision (), dependencies assemblyPath with
                | Ok(version, revision), Ok resolved ->
                    Ok
                        {
                            SchemaVersion = SchemaVersion
                            Artifact =
                                {
                                    Name = "percepta-semantic"
                                    Version = version
                                    SourceRevision = revision
                                    Digest = sha256File assemblyPath
                                    DigestAlgorithm = Sha256Algorithm
                                }
                            Dependencies = resolved
                        }
                | Error error, _
                | _, Error error -> Error error
        with ex ->
            Error($"Cannot identify the running Percepta artifact: {ex.Message}")

    let private input role sourceRoot path =
        if not (File.Exists path) then
            Error($"Required experiment input is unavailable: {path}")
        else
            relativePath sourceRoot path
            |> Result.map (fun relative ->
                {
                    Role = role
                    Path = relative
                    Digest = sha256File path
                    DigestAlgorithm = Sha256Algorithm
                })

    let inputRevisionMismatches sourceRoot sourceRevision (inputs: InputIdentity list) =
        inputs
        // Visual baselines may be created by a prior run in the same controlled
        // workspace. They are still content-bound inputs, but are not source
        // files and therefore cannot be required to exist in the Git tree.
        |> List.filter (fun input -> input.Role <> "visual-baseline")
        |> List.choose (fun input ->
            match runGitBytes sourceRoot [ "show"; $"{sourceRevision}:{input.Path}" ] with
            | Error _ -> Some($"{input.Path} is not available at source revision {sourceRevision}.")
            | Ok bytes when sha256Bytes bytes <> input.Digest ->
                Some($"{input.Path} differs from source revision {sourceRevision}.")
            | Ok _ -> None)

    let collectInputs sourceRoot sourceRevision contractPath target fixturePaths semanticReview baselineDirectory =
        let targetPath =
            match Uri.TryCreate(target, UriKind.Absolute) with
            | true, uri when uri.Scheme = Uri.UriSchemeFile -> Ok uri.LocalPath
            | true, uri when uri.Scheme = Uri.UriSchemeHttp || uri.Scheme = Uri.UriSchemeHttps ->
                Error "Evidence contract v1.0.0 requires a local target file whose actual content can be hashed."
            | _ -> Ok target

        let baselinePaths =
            if Directory.Exists baselineDirectory then
                Directory.GetFiles(baselineDirectory, "*", SearchOption.AllDirectories)
                |> Array.sort
                |> Array.toList
            else
                []

        match targetPath with
        | Error error -> Error error
        | Ok targetFile ->
            let candidates =
                [ "contract", contractPath; "target", targetFile ]
                @ (fixturePaths |> List.map (fun path -> "fixture", path))
                @ (semanticReview |> Option.toList |> List.map (fun path -> "semantic-review", path))
                @ (baselinePaths |> List.map (fun path -> "visual-baseline", path))

            candidates
            |> List.map (fun (role, path) -> input role sourceRoot path)
            |> List.fold (fun state item ->
                match state, item with
                | Error error, _ -> Error error
                | _, Error error -> Error error
                | Ok values, Ok value -> Ok(value :: values)) (Ok [])
            |> Result.map (List.sortBy (fun value -> value.Role, value.Path))
            |> Result.bind (fun inputs ->
                match inputRevisionMismatches sourceRoot sourceRevision inputs with
                | [] -> Ok inputs
                | mismatches ->
                    Error(
                        "Experiment inputs are not bound to the reported source revision: "
                        + String.concat " | " mismatches
                    ))

    let evidenceReferences
        (sourceRoot: string)
        (sourceRevision: string)
        (inputs: InputIdentity list)
        (records: EvidenceRecord list)
        =
        let inputPaths = inputs |> List.map _.Path |> Set.ofList

        records
        |> List.collect _.EvidenceReferences
        |> List.choose (fun path ->
            let resolved =
                if Path.IsPathRooted path then path else Path.Combine(sourceRoot, path)

            if File.Exists resolved then
                match relativePath sourceRoot resolved with
                | Ok relative when not (Set.contains relative inputPaths) ->
                    Some
                        {
                            Kind = "verification-artifact"
                            Path = relative
                            SourceRevision = sourceRevision
                            Digest = sha256File resolved
                            DigestAlgorithm = Sha256Algorithm
                        }
                | _ -> None
            else
                None)
        |> List.distinctBy (fun value -> value.Path)
        |> List.sortBy _.Path

    let private normalizeEvaluationReferences (sourceRoot: string) (records: EvidenceRecord list) =
        let rec normalizeReferences (normalized: string list) (remaining: string list) =
            match remaining with
            | [] -> Ok(List.rev normalized)
            | reference :: remaining ->
                let normalizedReference =
                    if Path.IsPathRooted reference then
                        relativePath sourceRoot reference
                    else
                        Ok(normalizeSlashes reference)

                match normalizedReference with
                | Error error -> Error error
                | Ok value -> normalizeReferences (value :: normalized) remaining

        let rec normalizeRecords (normalized: EvidenceRecord list) (remaining: EvidenceRecord list) =
            match remaining with
            | [] -> Ok(List.rev normalized)
            | record :: remaining ->
                match normalizeReferences [] record.EvidenceReferences with
                | Error error -> Error error
                | Ok references ->
                    normalizeRecords
                        ({ record with EvidenceReferences = references } :: normalized)
                        remaining

        normalizeRecords [] records

    let createReport
        sourceRoot
        repository
        sourceRevision
        experimentId
        createdAt
        screen
        inputs
        configuration
        artifact
        dependencies
        (results: EvidenceRecord list)
        (references: EvidenceReference list)
        =
        match normalizeEvaluationReferences sourceRoot results with
        | Error error ->
            Error($"Evaluation evidence references must resolve within the source repository: {error}")
        | Ok normalizedResults when List.isEmpty references ->
            Error "No durable evidence artifacts were produced; evidence contract v1.0.0 cannot be emitted."
        | Ok normalizedResults ->
            let outcome =
                if normalizedResults
                   |> List.filter _.Required
                   |> List.forall (fun record -> Evidence.isAcceptable record.Status) then
                    Satisfied
                else
                    NotSatisfied

            let report =
                {
                    SchemaVersion = SchemaVersion
                    ExperimentId = experimentId
                    RunId = Guid.NewGuid().ToString("D")
                    CreatedAt = createdAt
                    Repository = repository
                    SourceRevision = sourceRevision
                    Inputs = inputs
                    InputDigest = computeInputDigest inputs
                    InputDigestAlgorithm = Sha256Algorithm
                    Configuration = configuration
                    ConfigurationDigest = computeConfigurationDigest configuration
                    ConfigurationDigestAlgorithm = Sha256Algorithm
                    Artifact = artifact
                    Dependencies = dependencies
                    Outcome = outcome
                    EvaluationResults = normalizedResults
                    EvidenceReferences = references
                    EvidenceDigest = String.replicate 64 "0"
                    EvidenceDigestAlgorithm = Sha256Algorithm
                    Screen = screen
                }

            Ok(withEvidenceDigest report)

    let validationStatusName =
        function
        | Valid -> "VALID"
        | Invalid -> "INVALID"
        | Unverifiable -> "UNVERIFIABLE"

    let writeValidationResult (path: string option) (evidencePath: string) (result: ValidationResult) =
        let write (stream: Stream) =
            use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
            writer.WriteStartObject()
            writer.WriteString("schemaVersion", SchemaVersion)
            writer.WriteString("status", validationStatusName result.Status)
            writer.WriteString("evidence", evidencePath)
            writer.WritePropertyName("findings")
            writer.WriteStartArray()

            for finding in result.Findings do
                writer.WriteStartObject()
                writer.WriteString("code", finding.Code)
                writer.WriteString("message", finding.Message)
                writer.WriteEndObject()

            writer.WriteEndArray()
            writer.WriteEndObject()
            writer.Flush()

        match path with
        | Some output ->
            let directory = Path.GetDirectoryName(output)

            if not (String.IsNullOrWhiteSpace directory) then
                Directory.CreateDirectory(directory) |> ignore

            use stream = File.Create(output)
            write stream
        | None -> write (Console.OpenStandardOutput())
