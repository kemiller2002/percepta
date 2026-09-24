namespace Percepta.Core

open System.Text

module Compilation =

    type VerificationObligation =
        {
            Id: VerificationId
            Kind: EvidenceKind
            Description: string
            Required: bool
        }

    type CompiledScreenContract =
        {
            Contract: ScreenContract
            AgentGuidance: string
            VerificationPlan: VerificationObligation list
        }

    let private screenId (ScreenId value) = value
    let private regionId (RegionId value) = value
    let private capabilityId (CapabilityId value) = value
    let private predicateId (DomainPredicateId value) = value
    let private verificationId (VerificationId value) = value

    let private hierarchyName =
        function
        | Primary -> "Primary"
        | Secondary -> "Secondary"
        | Tertiary -> "Tertiary"
        | Background -> "Background"

    let private visibilityName =
        function
        | AlwaysVisible -> "always visible"
        | VisibleWhen predicate -> $"visible when {predicateId predicate}"
        | MayCollapse -> "may collapse"
        | SecondaryNavigation -> "secondary navigation"

    let private capabilityProjectionText =
        function
        | MustExpose capability ->
            $"Expose capability '{capabilityId capability}'."
        | MustEnableWhenLegal capability ->
            $"Enable capability '{capabilityId capability}' when the domain reports it legal."
        | MustDisableWhenIllegal capability ->
            $"Disable capability '{capabilityId capability}' when the domain reports it illegal."
        | MustExplainWhenUnavailable capability ->
            $"Explain why capability '{capabilityId capability}' is unavailable."
        | MustNavigateToBlocker capability ->
            $"Provide a path from unavailable capability '{capabilityId capability}' to its blocking information."

    let private forbiddenPatternName =
        function
        | ChatOnlyPrimaryInterface -> "chat-only primary interface"
        | HideBlockingObligation -> "hidden blocking obligation"
        | DeleteFalsifiedHypothesis -> "deletion of falsified hypotheses or equivalent negative knowledge"
        | ColorOnlyState -> "color-only state communication"
        | OptimisticPersistenceSuccess -> "optimistic persistence success before confirmation"
        | UnsupportedConfidencePercentage -> "unsupported confidence percentage"
        | LinearizeCompetingHypotheses -> "forced linearization of competing hypotheses"
        | NamedForbiddenPattern name -> name

    let private evidenceKindName =
        function
        | ContractValidation -> "contract validation"
        | Structural -> "structural"
        | StateProjection -> "state projection"
        | Interaction -> "interaction"
        | Accessibility -> "accessibility"
        | Responsive -> "responsive"
        | VisualRegression -> "visual regression"
        | SemanticVisualReview -> "semantic visual review"
        | AcceptedDeviationEvidence -> "accepted deviation"

    let private appendLine (builder: StringBuilder) (text: string) =
        builder.AppendLine(text) |> ignore

    let private buildAgentGuidance (contract: ScreenContract) =
        let builder = StringBuilder()

        appendLine builder $"# Percepta implementation contract: {screenId contract.Id}"
        appendLine builder ""
        appendLine builder "## Product meaning"
        appendLine builder $"Purpose: {contract.Purpose}"
        appendLine builder $"Primary user question: {contract.PrimaryQuestion}"
        appendLine builder ""
        appendLine builder "Implementation details may vary. The product meaning below may not be silently redefined."
        appendLine builder ""

        appendLine builder "## Required semantic regions"

        for region in contract.Regions do
            appendLine
                builder
                $"- {regionId region.Id}: {region.Purpose} [hierarchy: {hierarchyName region.Hierarchy}; visibility: {visibilityName region.Visibility}]"

        appendLine builder ""
        appendLine builder "## Capability projection"

        if List.isEmpty contract.Capabilities then
            appendLine builder "- No capability projection obligations declared."
        else
            for capability in contract.Capabilities do
                appendLine builder $"- {capabilityProjectionText capability}"

        appendLine builder ""
        appendLine builder "## Domain-state projection"

        if List.isEmpty contract.StateProjections then
            appendLine builder "- No domain-state projections declared."
        else
            for projection in contract.StateProjections do
                appendLine builder $"- When '{predicateId projection.When}':"

                for observation in projection.RequiredObservations do
                    appendLine builder $"  - {observation}"

        appendLine builder ""
        appendLine builder "## Forbidden outcomes"

        if List.isEmpty contract.ForbiddenPatterns then
            appendLine builder "- No forbidden patterns declared."
        else
            for pattern in contract.ForbiddenPatterns do
                appendLine builder $"- Do not produce: {forbiddenPatternName pattern}."

        appendLine builder ""
        appendLine builder "## Responsive semantic obligations"

        if List.isEmpty contract.Breakpoints then
            appendLine builder "- No breakpoint-specific semantic obligations declared."
        else
            for breakpoint in contract.Breakpoints do
                let range =
                    match breakpoint.MinimumWidthCssPx, breakpoint.MaximumWidthCssPx with
                    | None, None -> "viewport class"
                    | Some minimum, None -> $"min {minimum}px"
                    | None, Some maximum -> $"max {maximum}px"
                    | Some minimum, Some maximum -> $"{minimum}px-{maximum}px"

                appendLine builder $"- {breakpoint.Name} ({range}):"

                for observation in breakpoint.RequiredObservations do
                    appendLine builder $"  - {observation}"

        appendLine builder ""
        appendLine builder "## Required verification evidence"

        for requirement in contract.EvidenceRequirements do
            let necessity =
                if requirement.Required then "required" else "supporting"

            appendLine
                builder
                $"- {verificationId requirement.Id}: {evidenceKindName requirement.Kind} [{necessity}] - {requirement.Description}"

        builder.ToString().TrimEnd()

    let private buildVerificationPlan (contract: ScreenContract) =
        contract.EvidenceRequirements
        |> List.map (fun requirement ->
            {
                Id = requirement.Id
                Kind = requirement.Kind
                Description = requirement.Description
                Required = requirement.Required
            })

    let compile (contract: ScreenContract) =
        match Validation.validate contract with
        | [] ->
            Ok
                {
                    Contract = contract
                    AgentGuidance = buildAgentGuidance contract
                    VerificationPlan = buildVerificationPlan contract
                }
        | issues ->
            Error issues
