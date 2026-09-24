namespace Percepta.Core

open System
open System.IO
open System.Text
open System.Text.Json

module ContractSerialization =

    let private value (ScreenId v) = v
    let private regionValue (RegionId v) = v
    let private capabilityValue (CapabilityId v) = v
    let private predicateValue (DomainPredicateId v) = v
    let private observationValue (ObservationId v) = v
    let private verificationValue (VerificationId v) = v

    let private hierarchyText =
        function
        | Primary -> "primary"
        | Secondary -> "secondary"
        | Tertiary -> "tertiary"
        | Background -> "background"

    let private parseHierarchy (text: string) =
        match text.Trim().ToLowerInvariant() with
        | "primary" -> Primary
        | "secondary" -> Secondary
        | "tertiary" -> Tertiary
        | "background" -> Background
        | other -> raise (FormatException($"Unknown hierarchy '{other}'."))

    let private evidenceKindText = Compilation.evidenceKindName

    let private parseEvidenceKind (text: string) =
        match text.Trim().ToLowerInvariant() with
        | "contract-validation" -> ContractValidation
        | "structural" -> Structural
        | "state-projection" -> StateProjection
        | "interaction" -> Interaction
        | "accessibility" -> Accessibility
        | "responsive" -> Responsive
        | "visual-regression" -> VisualRegression
        | "semantic-visual-review" -> SemanticVisualReview
        | "accepted-deviation" -> AcceptedDeviationEvidence
        | other -> raise (FormatException($"Unknown evidence kind '{other}'."))

    let private forbiddenText =
        function
        | ChatOnlyPrimaryInterface -> "chat-only-primary-interface"
        | HideBlockingObligation -> "hide-blocking-obligation"
        | DeleteFalsifiedHypothesis -> "delete-falsified-hypothesis"
        | ColorOnlyState -> "color-only-state"
        | OptimisticPersistenceSuccess -> "optimistic-persistence-success"
        | UnsupportedConfidencePercentage -> "unsupported-confidence-percentage"
        | LinearizeCompetingHypotheses -> "linearize-competing-hypotheses"
        | NamedForbiddenPattern name -> $"named:{name}"

    let private parseForbidden (text: string) =
        match text.Trim() with
        | "chat-only-primary-interface" -> ChatOnlyPrimaryInterface
        | "hide-blocking-obligation" -> HideBlockingObligation
        | "delete-falsified-hypothesis" -> DeleteFalsifiedHypothesis
        | "color-only-state" -> ColorOnlyState
        | "optimistic-persistence-success" -> OptimisticPersistenceSuccess
        | "unsupported-confidence-percentage" -> UnsupportedConfidencePercentage
        | "linearize-competing-hypotheses" -> LinearizeCompetingHypotheses
        | named when named.StartsWith("named:", StringComparison.Ordinal) ->
            NamedForbiddenPattern(named.Substring("named:".Length))
        | other -> raise (FormatException($"Unknown forbidden pattern '{other}'."))

    let private requireProperty (name: string) (element: JsonElement) =
        let mutable property = Unchecked.defaultof<JsonElement>

        if element.TryGetProperty(name, &property) then
            property
        else
            raise (FormatException($"Missing required property '{name}'."))

    let private stringProperty name element =
        let property = requireProperty name element

        match property.ValueKind with
        | JsonValueKind.String -> property.GetString()
        | _ -> raise (FormatException($"Property '{name}' must be a string."))

    let private intProperty name element =
        let property = requireProperty name element

        match property.TryGetInt32() with
        | true, value -> value
        | _ -> raise (FormatException($"Property '{name}' must be an integer."))

    let private boolProperty name element =
        let property = requireProperty name element

        match property.ValueKind with
        | JsonValueKind.True -> true
        | JsonValueKind.False -> false
        | _ -> raise (FormatException($"Property '{name}' must be a boolean."))

    let private arrayProperty name element =
        let property = requireProperty name element

        if property.ValueKind <> JsonValueKind.Array then
            raise (FormatException($"Property '{name}' must be an array."))

        property.EnumerateArray() |> Seq.toList

    let private parseVisibility element =
        let kind = stringProperty "kind" element

        match kind with
        | "always" -> AlwaysVisible
        | "when" -> VisibleWhen(DomainPredicateId(stringProperty "predicate" element))
        | "may-collapse" -> MayCollapse
        | "secondary-navigation" -> SecondaryNavigation
        | other -> raise (FormatException($"Unknown visibility kind '{other}'."))

    let private writeVisibility (writer: Utf8JsonWriter) visibility =
        writer.WriteStartObject()

        match visibility with
        | AlwaysVisible -> writer.WriteString("kind", "always")
        | VisibleWhen predicate ->
            writer.WriteString("kind", "when")
            writer.WriteString("predicate", predicateValue predicate)
        | MayCollapse -> writer.WriteString("kind", "may-collapse")
        | SecondaryNavigation -> writer.WriteString("kind", "secondary-navigation")

        writer.WriteEndObject()

    let private parseCapability element =
        let capability = CapabilityId(stringProperty "capability" element)

        match stringProperty "kind" element with
        | "expose" -> MustExpose capability
        | "enable-when-legal" -> MustEnableWhenLegal capability
        | "disable-when-illegal" -> MustDisableWhenIllegal capability
        | "explain-when-unavailable" -> MustExplainWhenUnavailable capability
        | "navigate-to-blocker" -> MustNavigateToBlocker capability
        | other -> raise (FormatException($"Unknown capability projection '{other}'."))

    let private writeCapability (writer: Utf8JsonWriter) projection =
        writer.WriteStartObject()

        match projection with
        | MustExpose capability ->
            writer.WriteString("kind", "expose")
            writer.WriteString("capability", capabilityValue capability)
        | MustEnableWhenLegal capability ->
            writer.WriteString("kind", "enable-when-legal")
            writer.WriteString("capability", capabilityValue capability)
        | MustDisableWhenIllegal capability ->
            writer.WriteString("kind", "disable-when-illegal")
            writer.WriteString("capability", capabilityValue capability)
        | MustExplainWhenUnavailable capability ->
            writer.WriteString("kind", "explain-when-unavailable")
            writer.WriteString("capability", capabilityValue capability)
        | MustNavigateToBlocker capability ->
            writer.WriteString("kind", "navigate-to-blocker")
            writer.WriteString("capability", capabilityValue capability)

        writer.WriteEndObject()

    let private parseObservation element =
        {
            Id = ObservationId(stringProperty "id" element)
            Description = stringProperty "description" element
        }

    let private writeObservation (writer: Utf8JsonWriter) observation =
        writer.WriteStartObject()
        writer.WriteString("id", observationValue observation.Id)
        writer.WriteString("description", observation.Description)
        writer.WriteEndObject()

    let deserialize (text: string) =
        try
            use document = JsonDocument.Parse(text)
            let root = document.RootElement

            let schemaVersion = intProperty "schemaVersion" root

            if schemaVersion <> 1 then
                raise (FormatException($"Unsupported contract schemaVersion '{schemaVersion}'."))

            let regions =
                arrayProperty "regions" root
                |> List.map (fun element ->
                    {
                        Id = RegionId(stringProperty "id" element)
                        Purpose = stringProperty "purpose" element
                        Hierarchy = parseHierarchy (stringProperty "hierarchy" element)
                        Visibility = parseVisibility (requireProperty "visibility" element)
                    })

            let capabilities =
                arrayProperty "capabilities" root |> List.map parseCapability

            let stateProjections =
                arrayProperty "stateProjections" root
                |> List.map (fun element ->
                    {
                        When = DomainPredicateId(stringProperty "when" element)
                        RequiredObservations =
                            arrayProperty "observations" element |> List.map parseObservation
                    })

            let forbidden =
                arrayProperty "forbiddenPatterns" root
                |> List.map (fun element ->
                    if element.ValueKind <> JsonValueKind.String then
                        raise (FormatException("Forbidden patterns must be strings."))

                    parseForbidden (element.GetString()))

            let breakpoints =
                arrayProperty "breakpoints" root
                |> List.map (fun element ->
                    {
                        Name = stringProperty "name" element
                        ViewportWidthCssPx = intProperty "width" element
                        ViewportHeightCssPx = intProperty "height" element
                        RequiredRegionsVisible =
                            arrayProperty "requiredRegionsVisible" element
                            |> List.map (fun region ->
                                if region.ValueKind <> JsonValueKind.String then
                                    raise (FormatException("Breakpoint region identifiers must be strings."))

                                RegionId(region.GetString()))
                        RequiredObservations =
                            arrayProperty "observations" element |> List.map parseObservation
                    })

            let evidenceRequirements =
                arrayProperty "evidenceRequirements" root
                |> List.map (fun element ->
                    {
                        Id = VerificationId(stringProperty "id" element)
                        Kind = parseEvidenceKind (stringProperty "kind" element)
                        Description = stringProperty "description" element
                        Required = boolProperty "required" element
                    })

            Ok
                {
                    Id = ScreenId(stringProperty "screen" root)
                    Purpose = stringProperty "purpose" root
                    PrimaryQuestion = stringProperty "primaryQuestion" root
                    Regions = regions
                    Capabilities = capabilities
                    StateProjections = stateProjections
                    ForbiddenPatterns = forbidden
                    Breakpoints = breakpoints
                    EvidenceRequirements = evidenceRequirements
                }
        with
        | :? JsonException as ex -> Error [ $"Invalid JSON: {ex.Message}" ]
        | :? FormatException as ex -> Error [ ex.Message ]

    let serialize contract =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", 1)
        writer.WriteString("screen", value contract.Id)
        writer.WriteString("purpose", contract.Purpose)
        writer.WriteString("primaryQuestion", contract.PrimaryQuestion)

        writer.WritePropertyName("regions")
        writer.WriteStartArray()

        for region in contract.Regions do
            writer.WriteStartObject()
            writer.WriteString("id", regionValue region.Id)
            writer.WriteString("purpose", region.Purpose)
            writer.WriteString("hierarchy", hierarchyText region.Hierarchy)
            writer.WritePropertyName("visibility")
            writeVisibility writer region.Visibility
            writer.WriteEndObject()

        writer.WriteEndArray()

        writer.WritePropertyName("capabilities")
        writer.WriteStartArray()

        for capability in contract.Capabilities do
            writeCapability writer capability

        writer.WriteEndArray()

        writer.WritePropertyName("stateProjections")
        writer.WriteStartArray()

        for projection in contract.StateProjections do
            writer.WriteStartObject()
            writer.WriteString("when", predicateValue projection.When)
            writer.WritePropertyName("observations")
            writer.WriteStartArray()

            for observation in projection.RequiredObservations do
                writeObservation writer observation

            writer.WriteEndArray()
            writer.WriteEndObject()

        writer.WriteEndArray()

        writer.WritePropertyName("forbiddenPatterns")
        writer.WriteStartArray()

        for pattern in contract.ForbiddenPatterns do
            writer.WriteStringValue(forbiddenText pattern)

        writer.WriteEndArray()

        writer.WritePropertyName("breakpoints")
        writer.WriteStartArray()

        for breakpoint in contract.Breakpoints do
            writer.WriteStartObject()
            writer.WriteString("name", breakpoint.Name)
            writer.WriteNumber("width", breakpoint.ViewportWidthCssPx)
            writer.WriteNumber("height", breakpoint.ViewportHeightCssPx)
            writer.WritePropertyName("requiredRegionsVisible")
            writer.WriteStartArray()

            for region in breakpoint.RequiredRegionsVisible do
                writer.WriteStringValue(regionValue region)

            writer.WriteEndArray()
            writer.WritePropertyName("observations")
            writer.WriteStartArray()

            for observation in breakpoint.RequiredObservations do
                writeObservation writer observation

            writer.WriteEndArray()
            writer.WriteEndObject()

        writer.WriteEndArray()

        writer.WritePropertyName("evidenceRequirements")
        writer.WriteStartArray()

        for requirement in contract.EvidenceRequirements do
            writer.WriteStartObject()
            writer.WriteString("id", verificationValue requirement.Id)
            writer.WriteString("kind", evidenceKindText requirement.Kind)
            writer.WriteString("description", requirement.Description)
            writer.WriteBoolean("required", requirement.Required)
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()

        Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine
