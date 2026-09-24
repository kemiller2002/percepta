namespace Percepta.Core

type ScreenId = ScreenId of string
type RegionId = RegionId of string
type CapabilityId = CapabilityId of string
type DomainPredicateId = DomainPredicateId of string
type VerificationId = VerificationId of string

type Hierarchy =
    | Primary
    | Secondary
    | Tertiary
    | Background

type Visibility =
    | AlwaysVisible
    | VisibleWhen of DomainPredicateId
    | MayCollapse
    | SecondaryNavigation

type CapabilityProjection =
    | MustExpose of CapabilityId
    | MustEnableWhenLegal of CapabilityId
    | MustDisableWhenIllegal of CapabilityId
    | MustExplainWhenUnavailable of CapabilityId
    | MustNavigateToBlocker of CapabilityId

type ForbiddenPattern =
    | ChatOnlyPrimaryInterface
    | HideBlockingObligation
    | DeleteFalsifiedHypothesis
    | ColorOnlyState
    | OptimisticPersistenceSuccess
    | UnsupportedConfidencePercentage
    | LinearizeCompetingHypotheses
    | NamedForbiddenPattern of string

type EvidenceKind =
    | ContractValidation
    | Structural
    | StateProjection
    | Interaction
    | Accessibility
    | Responsive
    | VisualRegression
    | SemanticVisualReview
    | AcceptedDeviationEvidence

type VerificationStatus =
    | Passed
    | Failed of reason: string
    | Unavailable of reason: string
    | NotApplicable of reason: string
    | AcceptedDeviation of reason: string

type RegionContract =
    {
        Id: RegionId
        Purpose: string
        Hierarchy: Hierarchy
        Visibility: Visibility
    }

type StateProjection =
    {
        When: DomainPredicateId
        RequiredObservations: string list
    }

type BreakpointContract =
    {
        Name: string
        MinimumWidthCssPx: int option
        MaximumWidthCssPx: int option
        RequiredObservations: string list
    }

type EvidenceRequirement =
    {
        Id: VerificationId
        Kind: EvidenceKind
        Description: string
        Required: bool
    }

type ScreenContract =
    {
        Id: ScreenId
        Purpose: string
        PrimaryQuestion: string
        Regions: RegionContract list
        Capabilities: CapabilityProjection list
        StateProjections: StateProjection list
        ForbiddenPatterns: ForbiddenPattern list
        Breakpoints: BreakpointContract list
        EvidenceRequirements: EvidenceRequirement list
    }

type VerificationResult =
    {
        Requirement: VerificationId
        Status: VerificationStatus
        EvidenceReference: string option
    }
