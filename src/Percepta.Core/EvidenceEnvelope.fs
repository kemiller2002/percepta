namespace Percepta.Core

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open Percepta.Core.Evidence

module EvidenceEnvelope =

    [<Literal>]
    let SchemaVersion = "1.0.0"

    [<Literal>]
    let Sha256Algorithm = "sha-256"

    type ExperimentOutcome =
        | Satisfied
        | NotSatisfied

    type InputIdentity =
        {
            Role: string
            Path: string
            Digest: string
            DigestAlgorithm: string
        }

    type EffectiveConfiguration =
        {
            ContractReference: string
            ContractSha256: string
            UpdateBaselines: bool
        }

    type ArtifactIdentity =
        {
            Name: string
            Version: string
            SourceRevision: string
            Digest: string
            DigestAlgorithm: string
        }

    type DependencyIdentity =
        {
            Name: string
            Version: string
            Digest: string option
            DigestAlgorithm: string option
        }

    type EvidenceReference =
        {
            Kind: string
            Path: string
            SourceRevision: string
            Digest: string
            DigestAlgorithm: string
        }

    type EvidenceReport =
        {
            SchemaVersion: string
            ExperimentId: string
            RunId: string
            CreatedAt: string
            Repository: string
            SourceRevision: string
            Inputs: InputIdentity list
            InputDigest: string
            InputDigestAlgorithm: string
            Configuration: EffectiveConfiguration
            ConfigurationDigest: string
            ConfigurationDigestAlgorithm: string
            Artifact: ArtifactIdentity
            Dependencies: DependencyIdentity list
            Outcome: ExperimentOutcome
            EvaluationResults: EvidenceRecord list
            EvidenceReferences: EvidenceReference list
            EvidenceDigest: string
            EvidenceDigestAlgorithm: string
            Screen: ScreenId
        }

    type ArtifactMetadata =
        {
            SchemaVersion: string
            Artifact: ArtifactIdentity
            Dependencies: DependencyIdentity list
        }

    type ValidationStatus =
        | Valid
        | Invalid
        | Unverifiable

    type ValidationFinding =
        {
            Code: string
            Message: string
        }

    type ValidationContext =
        {
            SourceRoot: string
            Repository: string
            SourceRevision: string
            TrustedArtifact: ArtifactMetadata option
        }

    type ValidationResult =
        {
            Status: ValidationStatus
            Findings: ValidationFinding list
        }

    let private screenValue (ScreenId value) = value
    let private verificationValue (VerificationId value) = value

    let private outcomeName =
        function
        | Satisfied -> "satisfied"
        | NotSatisfied -> "not-satisfied"

    let private statusName =
        function
        | Passed -> "Passed", None
        | Failed reason -> "Failed", Some reason
        | Unavailable reason -> "Unavailable", Some reason
        | NotApplicable reason -> "NotApplicable", Some reason
        | AcceptedDeviation reason -> "AcceptedDeviation", Some reason

    let private parseOutcome =
        function
        | "satisfied" -> Satisfied
        | "not-satisfied" -> NotSatisfied
        | value -> raise (FormatException($"Unsupported experiment outcome '{value}'."))

    let private parseEvidenceKind =
        function
        | "contract-validation" -> ContractValidation
        | "structural" -> Structural
        | "state-projection" -> StateProjection
        | "interaction" -> Interaction
        | "accessibility" -> Accessibility
        | "responsive" -> Responsive
        | "visual-regression" -> VisualRegression
        | "semantic-visual-review" -> SemanticVisualReview
        | "accepted-deviation" -> AcceptedDeviationEvidence
        | value -> raise (FormatException($"Unsupported evidence kind '{value}'."))

    let private parseStatus value reason =
        match value with
        | "Passed" -> Passed
        | "Failed" -> Failed(defaultArg reason "Evidence evaluation failed without a reason.")
        | "Unavailable" -> Unavailable(defaultArg reason "Evidence evaluation is unavailable.")
        | "NotApplicable" -> NotApplicable(defaultArg reason "Evidence evaluation is not applicable.")
        | "AcceptedDeviation" -> AcceptedDeviation(defaultArg reason "Evidence deviation was accepted.")
        | other -> raise (FormatException($"Unsupported verification status '{other}'."))

    let sha256Bytes (bytes: byte array) =
        SHA256.HashData(bytes)
        |> Convert.ToHexString
        |> fun value -> value.ToLowerInvariant()

    let sha256Text (value: string) =
        value |> Encoding.UTF8.GetBytes |> sha256Bytes

    let sha256File path =
        use stream = File.OpenRead(path)

        SHA256.HashData(stream)
        |> Convert.ToHexString
        |> fun value -> value.ToLowerInvariant()

    let isSha256 (value: string) =
        value.Length = 64
        && value
           |> Seq.forall (fun character ->
               (character >= '0' && character <= '9')
               || (character >= 'a' && character <= 'f'))

    let private writeEvidenceRecord (writer: Utf8JsonWriter) (record: EvidenceRecord) =
        let status, reason = statusName record.Status
        writer.WriteStartObject()
        writer.WriteString("requirement", verificationValue record.Requirement)
        writer.WriteString("kind", Compilation.evidenceKindName record.Kind)
        writer.WriteBoolean("required", record.Required)
        writer.WriteString("status", status)

        match reason with
        | Some value -> writer.WriteString("reason", value)
        | None -> ()

        writer.WriteString("summary", record.Summary)
        writer.WriteString("source", record.Source)
        writer.WritePropertyName("evidenceReferences")
        writer.WriteStartArray()

        for reference in record.EvidenceReferences do
            writer.WriteStringValue(reference)

        writer.WriteEndArray()
        writer.WriteEndObject()

    let private writeInput (writer: Utf8JsonWriter) (input: InputIdentity) =
        writer.WriteStartObject()
        writer.WriteString("role", input.Role)
        writer.WriteString("path", input.Path)
        writer.WriteString("digest", input.Digest)
        writer.WriteString("digestAlgorithm", input.DigestAlgorithm)
        writer.WriteEndObject()

    let private writeConfiguration (writer: Utf8JsonWriter) (configuration: EffectiveConfiguration) =
        writer.WriteStartObject()
        writer.WriteString("contractReference", configuration.ContractReference)
        writer.WriteString("contractSha256", configuration.ContractSha256)
        writer.WriteBoolean("updateBaselines", configuration.UpdateBaselines)
        writer.WriteEndObject()

    let private writeArtifact (writer: Utf8JsonWriter) (artifact: ArtifactIdentity) =
        writer.WriteStartObject()
        writer.WriteString("artifactName", artifact.Name)
        writer.WriteString("artifactVersion", artifact.Version)
        writer.WriteString("artifactSourceRevision", artifact.SourceRevision)
        writer.WriteString("artifactDigest", artifact.Digest)
        writer.WriteString("artifactDigestAlgorithm", artifact.DigestAlgorithm)
        writer.WriteEndObject()

    let private writeDependency (writer: Utf8JsonWriter) (dependency: DependencyIdentity) =
        writer.WriteStartObject()
        writer.WriteString("dependencyName", dependency.Name)
        writer.WriteString("dependencyVersion", dependency.Version)

        match dependency.Digest, dependency.DigestAlgorithm with
        | Some digest, Some algorithm ->
            writer.WriteString("dependencyDigest", digest)
            writer.WriteString("dependencyDigestAlgorithm", algorithm)
        | None, None -> ()
        | _ -> invalidArg "dependency" "A dependency digest and algorithm must be present together."

        writer.WriteEndObject()

    let private writeEvidenceReference (writer: Utf8JsonWriter) (reference: EvidenceReference) =
        writer.WriteStartObject()
        writer.WriteString("kind", reference.Kind)
        writer.WriteString("path", reference.Path)
        writer.WriteString("sourceRevision", reference.SourceRevision)
        writer.WriteString("digest", reference.Digest)
        writer.WriteString("digestAlgorithm", reference.DigestAlgorithm)
        writer.WriteEndObject()

    let private sortedInputs inputs =
        inputs |> List.sortBy (fun value -> value.Role, value.Path)

    let private sortedDependencies dependencies =
        dependencies |> List.sortBy (fun value -> value.Name.ToLowerInvariant(), value.Version)

    let private sortedReferences references =
        references |> List.sortBy (fun value -> value.Kind, value.Path)

    let private render indented write =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = indented))
        write writer
        writer.Flush()
        stream.ToArray()

    let computeInputDigest inputs =
        render false (fun writer ->
            writer.WriteStartArray()

            for input in sortedInputs inputs do
                writeInput writer input

            writer.WriteEndArray())
        |> sha256Bytes

    let serializeConfiguration configuration =
        render false (fun writer -> writeConfiguration writer configuration)
        |> Encoding.UTF8.GetString

    let computeConfigurationDigest configuration =
        serializeConfiguration configuration |> sha256Text

    let private stableBytes (report: EvidenceReport) =
        render false (fun writer ->
            writer.WriteStartObject()
            writer.WriteString("schemaVersion", report.SchemaVersion)
            writer.WriteString("experimentId", report.ExperimentId)
            writer.WriteString("repository", report.Repository)
            writer.WriteString("sourceRevision", report.SourceRevision)
            writer.WriteString("inputDigest", report.InputDigest)
            writer.WriteString("inputDigestAlgorithm", report.InputDigestAlgorithm)
            writer.WritePropertyName("inputs")
            writer.WriteStartArray()

            for input in sortedInputs report.Inputs do
                writeInput writer input

            writer.WriteEndArray()
            writer.WriteString("configurationDigest", report.ConfigurationDigest)
            writer.WriteString("configurationDigestAlgorithm", report.ConfigurationDigestAlgorithm)
            writer.WritePropertyName("configuration")
            writeConfiguration writer report.Configuration
            writer.WritePropertyName("artifact")
            writeArtifact writer report.Artifact
            writer.WritePropertyName("dependencies")
            writer.WriteStartArray()

            for dependency in sortedDependencies report.Dependencies do
                writeDependency writer dependency

            writer.WriteEndArray()
            writer.WriteString("outcome", outcomeName report.Outcome)
            writer.WritePropertyName("evaluationResults")
            writer.WriteStartArray()

            for result in report.EvaluationResults do
                writeEvidenceRecord writer result

            writer.WriteEndArray()
            writer.WritePropertyName("evidenceReferences")
            writer.WriteStartArray()

            for reference in sortedReferences report.EvidenceReferences do
                writeEvidenceReference writer reference

            writer.WriteEndArray()
            writer.WriteString("screen", screenValue report.Screen)
            writer.WriteEndObject())

    let computeEvidenceDigest report =
        stableBytes report |> sha256Bytes

    let withEvidenceDigest report =
        { report with EvidenceDigest = computeEvidenceDigest report }

    let serialize report =
        let report = withEvidenceDigest report

        render true (fun writer ->
            writer.WriteStartObject()
            writer.WriteString("schemaVersion", report.SchemaVersion)
            writer.WriteString("experimentId", report.ExperimentId)
            writer.WriteString("runId", report.RunId)
            writer.WriteString("createdAt", report.CreatedAt)
            writer.WriteString("repository", report.Repository)
            writer.WriteString("sourceRevision", report.SourceRevision)
            writer.WriteString("inputDigest", report.InputDigest)
            writer.WriteString("inputDigestAlgorithm", report.InputDigestAlgorithm)
            writer.WritePropertyName("inputs")
            writer.WriteStartArray()

            for input in sortedInputs report.Inputs do
                writeInput writer input

            writer.WriteEndArray()
            writer.WriteString("configurationDigest", report.ConfigurationDigest)
            writer.WriteString("configurationDigestAlgorithm", report.ConfigurationDigestAlgorithm)
            writer.WritePropertyName("configuration")
            writeConfiguration writer report.Configuration
            writer.WritePropertyName("artifact")
            writeArtifact writer report.Artifact
            writer.WritePropertyName("dependencies")
            writer.WriteStartArray()

            for dependency in sortedDependencies report.Dependencies do
                writeDependency writer dependency

            writer.WriteEndArray()
            writer.WriteString("outcome", outcomeName report.Outcome)
            writer.WritePropertyName("evaluationResults")
            writer.WriteStartArray()

            for result in report.EvaluationResults do
                writeEvidenceRecord writer result

            writer.WriteEndArray()
            writer.WritePropertyName("evidenceReferences")
            writer.WriteStartArray()

            for reference in sortedReferences report.EvidenceReferences do
                writeEvidenceReference writer reference

            writer.WriteEndArray()
            writer.WriteString("evidenceDigest", report.EvidenceDigest)
            writer.WriteString("evidenceDigestAlgorithm", report.EvidenceDigestAlgorithm)

            // Compatibility projection for existing 0.1.x consumers. The validator
            // checks that these aliases agree with the authoritative v1.0.0 fields.
            writer.WriteString("screen", screenValue report.Screen)
            writer.WriteString("contractSha256", report.Configuration.ContractSha256)
            writer.WriteString("generatedAtUtc", report.CreatedAt)
            writer.WriteBoolean("complete", report.Outcome = Satisfied)
            writer.WritePropertyName("results")
            writer.WriteStartArray()

            for result in report.EvaluationResults do
                writeEvidenceRecord writer result

            writer.WriteEndArray()
            writer.WriteEndObject())
        |> Encoding.UTF8.GetString

    let serializeArtifactMetadata metadata =
        render true (fun writer ->
            writer.WriteStartObject()
            writer.WriteString("schemaVersion", metadata.SchemaVersion)
            writer.WritePropertyName("artifact")
            writeArtifact writer metadata.Artifact
            writer.WritePropertyName("dependencies")
            writer.WriteStartArray()

            for dependency in sortedDependencies metadata.Dependencies do
                writeDependency writer dependency

            writer.WriteEndArray()
            writer.WriteEndObject())
        |> Encoding.UTF8.GetString

    let private requireProperty (name: string) (element: JsonElement) =
        let mutable value = Unchecked.defaultof<JsonElement>

        if element.TryGetProperty(name, &value) then
            value
        else
            raise (FormatException($"Missing required property '{name}'."))

    let private stringProperty (name: string) (element: JsonElement) =
        let value = requireProperty name element

        if value.ValueKind <> JsonValueKind.String || String.IsNullOrWhiteSpace(value.GetString()) then
            raise (FormatException($"Property '{name}' must be a non-empty string."))

        value.GetString()

    let private boolProperty (name: string) (element: JsonElement) =
        let value = requireProperty name element

        match value.ValueKind with
        | JsonValueKind.True -> true
        | JsonValueKind.False -> false
        | _ -> raise (FormatException($"Property '{name}' must be a boolean."))

    let private arrayProperty (name: string) (element: JsonElement) =
        let value = requireProperty name element

        if value.ValueKind <> JsonValueKind.Array then
            raise (FormatException($"Property '{name}' must be an array."))

        value.EnumerateArray() |> Seq.toList

    let private optionalString (name: string) (element: JsonElement) =
        let mutable value = Unchecked.defaultof<JsonElement>

        if element.TryGetProperty(name, &value) && value.ValueKind = JsonValueKind.String then
            Some(value.GetString())
        else
            None

    let private parseInput element =
        {
            Role = stringProperty "role" element
            Path = stringProperty "path" element
            Digest = stringProperty "digest" element
            DigestAlgorithm = stringProperty "digestAlgorithm" element
        }

    let private parseConfiguration element =
        {
            ContractReference = stringProperty "contractReference" element
            ContractSha256 = stringProperty "contractSha256" element
            UpdateBaselines = boolProperty "updateBaselines" element
        }

    let private parseArtifact element =
        {
            Name = stringProperty "artifactName" element
            Version = stringProperty "artifactVersion" element
            SourceRevision = stringProperty "artifactSourceRevision" element
            Digest = stringProperty "artifactDigest" element
            DigestAlgorithm = stringProperty "artifactDigestAlgorithm" element
        }

    let private parseDependency element =
        let digest = optionalString "dependencyDigest" element
        let algorithm = optionalString "dependencyDigestAlgorithm" element

        if Option.isSome digest <> Option.isSome algorithm then
            raise (FormatException("Dependency digest and digest algorithm must be present together."))

        {
            Name = stringProperty "dependencyName" element
            Version = stringProperty "dependencyVersion" element
            Digest = digest
            DigestAlgorithm = algorithm
        }

    let private parseEvidenceReference element =
        {
            Kind = stringProperty "kind" element
            Path = stringProperty "path" element
            SourceRevision = stringProperty "sourceRevision" element
            Digest = stringProperty "digest" element
            DigestAlgorithm = stringProperty "digestAlgorithm" element
        }

    let private parseEvidenceRecord element =
        let reason = optionalString "reason" element

        {
            Requirement = VerificationId(stringProperty "requirement" element)
            Kind = parseEvidenceKind (stringProperty "kind" element)
            Required = boolProperty "required" element
            Status = parseStatus (stringProperty "status" element) reason
            Summary = stringProperty "summary" element
            Source = stringProperty "source" element
            EvidenceReferences =
                arrayProperty "evidenceReferences" element
                |> List.map (fun value ->
                    if value.ValueKind <> JsonValueKind.String then
                        raise (FormatException("Evaluation evidence references must be strings."))

                    value.GetString())
        }

    let private checkDigest name algorithm digest =
        if algorithm <> Sha256Algorithm then
            raise (FormatException($"Property '{name}Algorithm' uses unsupported algorithm '{algorithm}'."))

        if not (isSha256 digest) then
            raise (FormatException($"Property '{name}' must be a lowercase SHA-256 digest."))

    let private checkDependencies dependencies =
        let duplicates =
            dependencies
            |> List.countBy (fun value -> value.Name.ToLowerInvariant())
            |> List.filter (fun (_, count) -> count > 1)

        if not (List.isEmpty duplicates) then
            let names = duplicates |> List.map fst |> String.concat ", "
            raise (FormatException($"Duplicate dependency identities are not allowed: {names}."))

        for dependency in dependencies do
            match dependency.Digest, dependency.DigestAlgorithm with
            | Some digest, Some algorithm -> checkDigest "dependencyDigest" algorithm digest
            | None, None -> ()
            | _ -> raise (FormatException("Dependency digest and algorithm must be present together."))

    let deserialize (text: string) =
        try
            use document = JsonDocument.Parse(text)
            let root = document.RootElement
            let schemaVersion = stringProperty "schemaVersion" root

            if schemaVersion <> SchemaVersion then
                raise (FormatException($"Unsupported evidence schemaVersion '{schemaVersion}'."))

            let createdAt = stringProperty "createdAt" root
            let generatedAt = stringProperty "generatedAtUtc" root

            if createdAt <> generatedAt then
                raise (FormatException("Compatibility property 'generatedAtUtc' conflicts with 'createdAt'."))

            match DateTimeOffset.TryParse createdAt with
            | false, _ -> raise (FormatException("Property 'createdAt' must be an ISO-8601 timestamp."))
            | _ -> ()

            let configuration = parseConfiguration (requireProperty "configuration" root)
            let configurationDigest = stringProperty "configurationDigest" root
            let configurationAlgorithm = stringProperty "configurationDigestAlgorithm" root
            let inputDigest = stringProperty "inputDigest" root
            let inputAlgorithm = stringProperty "inputDigestAlgorithm" root
            let evidenceDigest = stringProperty "evidenceDigest" root
            let evidenceAlgorithm = stringProperty "evidenceDigestAlgorithm" root
            checkDigest "inputDigest" inputAlgorithm inputDigest
            checkDigest "configurationDigest" configurationAlgorithm configurationDigest
            checkDigest "evidenceDigest" evidenceAlgorithm evidenceDigest
            checkDigest "contractSha256" Sha256Algorithm configuration.ContractSha256

            if stringProperty "contractSha256" root <> configuration.ContractSha256 then
                raise (FormatException("Compatibility property 'contractSha256' conflicts with configuration.contractSha256."))

            let artifact = parseArtifact (requireProperty "artifact" root)
            checkDigest "artifactDigest" artifact.DigestAlgorithm artifact.Digest

            let dependencies = arrayProperty "dependencies" root |> List.map parseDependency
            checkDependencies dependencies

            let inputs = arrayProperty "inputs" root |> List.map parseInput

            if List.isEmpty inputs then
                raise (FormatException("At least one experiment input is required."))

            for input in inputs do
                checkDigest "input.digest" input.DigestAlgorithm input.Digest

            let evaluationResults =
                arrayProperty "evaluationResults" root |> List.map parseEvidenceRecord

            if List.isEmpty evaluationResults then
                raise (FormatException("At least one evaluation result is required."))

            let compatibilityResults = arrayProperty "results" root |> List.map parseEvidenceRecord

            if compatibilityResults <> evaluationResults then
                raise (FormatException("Compatibility property 'results' conflicts with 'evaluationResults'."))

            let references =
                arrayProperty "evidenceReferences" root |> List.map parseEvidenceReference

            if List.isEmpty references then
                raise (FormatException("At least one durable evidence reference is required."))

            for reference in references do
                checkDigest "evidenceReference.digest" reference.DigestAlgorithm reference.Digest

            let outcome = parseOutcome (stringProperty "outcome" root)
            let complete = boolProperty "complete" root

            if complete <> (outcome = Satisfied) then
                raise (FormatException("Compatibility property 'complete' conflicts with 'outcome'."))

            let report: EvidenceReport =
                {
                    SchemaVersion = schemaVersion
                    ExperimentId = stringProperty "experimentId" root
                    RunId = stringProperty "runId" root
                    CreatedAt = createdAt
                    Repository = stringProperty "repository" root
                    SourceRevision = stringProperty "sourceRevision" root
                    Inputs = inputs
                    InputDigest = inputDigest
                    InputDigestAlgorithm = inputAlgorithm
                    Configuration = configuration
                    ConfigurationDigest = configurationDigest
                    ConfigurationDigestAlgorithm = configurationAlgorithm
                    Artifact = artifact
                    Dependencies = dependencies
                    Outcome = outcome
                    EvaluationResults = evaluationResults
                    EvidenceReferences = references
                    EvidenceDigest = evidenceDigest
                    EvidenceDigestAlgorithm = evidenceAlgorithm
                    Screen = ScreenId(stringProperty "screen" root)
                }

            Ok report
        with
        | :? JsonException as ex -> Error [ $"Invalid JSON: {ex.Message}" ]
        | :? FormatException as ex -> Error [ ex.Message ]

    let deserializeArtifactMetadata (text: string) =
        try
            use document = JsonDocument.Parse(text)
            let root = document.RootElement
            let schemaVersion = stringProperty "schemaVersion" root

            if schemaVersion <> SchemaVersion then
                raise (FormatException($"Unsupported artifact metadata schemaVersion '{schemaVersion}'."))

            let artifact = parseArtifact (requireProperty "artifact" root)
            checkDigest "artifactDigest" artifact.DigestAlgorithm artifact.Digest
            let dependencies = arrayProperty "dependencies" root |> List.map parseDependency
            checkDependencies dependencies

            Ok
                ({
                    SchemaVersion = schemaVersion
                    Artifact = artifact
                    Dependencies = dependencies
                }: ArtifactMetadata)
        with
        | :? JsonException as ex -> Error [ $"Invalid artifact metadata JSON: {ex.Message}" ]
        | :? FormatException as ex -> Error [ ex.Message ]

    let private safePath root relative =
        let root = Path.GetFullPath(root)
        let candidate = Path.GetFullPath(Path.Combine(root, relative))
        let boundary = root.TrimEnd(Path.DirectorySeparatorChar) + string Path.DirectorySeparatorChar

        if candidate <> root && not (candidate.StartsWith(boundary, StringComparison.Ordinal)) then
            Error($"Path escapes the trusted source root: {relative}")
        else
            Ok candidate

    let private dependencyKey (dependency: DependencyIdentity) =
        dependency.Name.ToLowerInvariant(), dependency.Version, dependency.Digest, dependency.DigestAlgorithm

    let validate (context: ValidationContext) (report: EvidenceReport) =
        let invalid = ResizeArray<ValidationFinding>()
        let unverifiable = ResizeArray<ValidationFinding>()
        let addInvalid code message = invalid.Add({ Code = code; Message = message })
        let addUnverifiable code message = unverifiable.Add({ Code = code; Message = message })

        if report.SchemaVersion <> SchemaVersion then
            addInvalid "PERCEPTA.EVIDENCE.SCHEMA_UNSUPPORTED" $"Unsupported evidence schemaVersion '{report.SchemaVersion}'."

        if report.Repository <> context.Repository then
            addInvalid "PERCEPTA.EVIDENCE.REPOSITORY_MISMATCH" "Evidence repository does not match the trusted source repository."

        if report.SourceRevision <> context.SourceRevision then
            addInvalid "PERCEPTA.EVIDENCE.SOURCE_MISMATCH" "Evidence source revision does not match the trusted source revision."

        if computeEvidenceDigest report <> report.EvidenceDigest then
            addInvalid "PERCEPTA.EVIDENCE.DIGEST_MISMATCH" "The stable evidence digest does not match the canonical evidence content."

        let recomputedInputs = ResizeArray<InputIdentity>()

        for (input: InputIdentity) in report.Inputs do
            match safePath context.SourceRoot input.Path with
            | Error message -> addInvalid "PERCEPTA.EVIDENCE.INPUT_PATH_INVALID" message
            | Ok path when not (File.Exists path) ->
                addInvalid "PERCEPTA.EVIDENCE.INPUT_MISSING" $"Experiment input is unavailable: {input.Path}"
            | Ok path ->
                let digest = sha256File path

                if digest <> input.Digest then
                    addInvalid "PERCEPTA.EVIDENCE.INPUT_MISMATCH" $"Experiment input digest differs: {input.Path}"

                recomputedInputs.Add({ input with Digest = digest })

        if recomputedInputs.Count = report.Inputs.Length
           && computeInputDigest (List.ofSeq recomputedInputs) <> report.InputDigest then
            addInvalid "PERCEPTA.EVIDENCE.INPUT_SET_MISMATCH" "The aggregate input digest does not match the referenced inputs."

        match safePath context.SourceRoot report.Configuration.ContractReference with
        | Error message -> addInvalid "PERCEPTA.EVIDENCE.CONFIGURATION_PATH_INVALID" message
        | Ok path when not (File.Exists path) ->
            addInvalid "PERCEPTA.EVIDENCE.CONFIGURATION_MISSING" "The effective experiment configuration is unavailable."
        | Ok path ->
            match ContractSerialization.deserialize (File.ReadAllText(path)) with
            | Error errors ->
                addInvalid "PERCEPTA.EVIDENCE.CONFIGURATION_INVALID" (String.concat " | " errors)
            | Ok contract ->
                let canonicalContractDigest = ContractSerialization.serialize contract |> sha256Text

                if canonicalContractDigest <> report.Configuration.ContractSha256 then
                    addInvalid "PERCEPTA.EVIDENCE.CONTRACT_MISMATCH" "The canonical contract digest does not match the evidence."

                if computeConfigurationDigest report.Configuration <> report.ConfigurationDigest then
                    addInvalid "PERCEPTA.EVIDENCE.CONFIGURATION_MISMATCH" "The effective configuration digest does not match the evidence."

        for (reference: EvidenceReference) in report.EvidenceReferences do
            if reference.SourceRevision <> report.SourceRevision then
                addInvalid "PERCEPTA.EVIDENCE.REFERENCE_SOURCE_MISMATCH" $"Evidence reference has a different source revision: {reference.Path}"

            match safePath context.SourceRoot reference.Path with
            | Error message -> addInvalid "PERCEPTA.EVIDENCE.REFERENCE_PATH_INVALID" message
            | Ok path when not (File.Exists path) ->
                addInvalid "PERCEPTA.EVIDENCE.REFERENCE_MISSING" $"Evidence artifact is unavailable: {reference.Path}"
            | Ok path when sha256File path <> reference.Digest ->
                addInvalid "PERCEPTA.EVIDENCE.REFERENCE_MISMATCH" $"Evidence artifact digest differs: {reference.Path}"
            | Ok _ -> ()

        match context.TrustedArtifact with
        | None ->
            addUnverifiable
                "PERCEPTA.EVIDENCE.ARTIFACT_TRUST_UNAVAILABLE"
                "Trusted Percepta artifact metadata is unavailable."
        | Some trusted ->
            if trusted.SchemaVersion <> SchemaVersion then
                addUnverifiable
                    "PERCEPTA.EVIDENCE.ARTIFACT_TRUST_UNSUPPORTED"
                    "Trusted artifact metadata uses an unsupported schema version."

            if trusted.Artifact <> report.Artifact then
                addInvalid "PERCEPTA.EVIDENCE.ARTIFACT_MISMATCH" "Percepta artifact identity conflicts with trusted release metadata."

            let trustedDependencies = trusted.Dependencies |> List.map dependencyKey |> Set.ofList
            let reportDependencies = report.Dependencies |> List.map dependencyKey |> Set.ofList

            if trustedDependencies <> reportDependencies then
                addInvalid "PERCEPTA.EVIDENCE.DEPENDENCY_MISMATCH" "Dependency identities conflict with trusted release metadata."

        if invalid.Count > 0 then
            {
                Status = Invalid
                Findings = List.ofSeq invalid @ List.ofSeq unverifiable
            }
        elif unverifiable.Count > 0 then
            {
                Status = Unverifiable
                Findings = List.ofSeq unverifiable
            }
        else
            { Status = Valid; Findings = [] }
