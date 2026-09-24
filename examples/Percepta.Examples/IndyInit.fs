namespace Percepta.Examples

open Percepta.Core

module IndyInit =

    let private observation id description =
        {
            Id = ObservationId id
            Description = description
        }

    let private evidence id kind description required =
        {
            Id = VerificationId id
            Kind = kind
            Description = description
            Required = required
        }

    let investigationWorkspace : ScreenContract =
        {
            Id = ScreenId "investigation-workspace"
            Purpose = "Investigate an observed failure without prematurely converging on an explanation."
            PrimaryQuestion = "What currently explains the observation, and what should be tested next?"
            Regions =
                [
                    {
                        Id = RegionId "observation"
                        Purpose = "Preserve the triggering observation and its source."
                        Hierarchy = Primary
                        Visibility = AlwaysVisible
                    }
                    {
                        Id = RegionId "hypotheses"
                        Purpose = "Compare active, weakened, and falsified explanations."
                        Hierarchy = Primary
                        Visibility = AlwaysVisible
                    }
                    {
                        Id = RegionId "contradictions"
                        Purpose = "Expose evidence that conflicts with the current explanation."
                        Hierarchy = Primary
                        Visibility = VisibleWhen(DomainPredicateId "contradictions-present")
                    }
                    {
                        Id = RegionId "unknowns"
                        Purpose = "Expose unresolved information, especially blockers."
                        Hierarchy = Primary
                        Visibility = AlwaysVisible
                    }
                    {
                        Id = RegionId "obligations"
                        Purpose = "Show work required before legal progress can continue."
                        Hierarchy = Primary
                        Visibility = VisibleWhen(DomainPredicateId "obligations-present")
                    }
                    {
                        Id = RegionId "evidence"
                        Purpose = "Expose evidence and its relationship to hypotheses."
                        Hierarchy = Secondary
                        Visibility = AlwaysVisible
                    }
                    {
                        Id = RegionId "legal-actions"
                        Purpose = "Show available and unavailable next actions."
                        Hierarchy = Primary
                        Visibility = AlwaysVisible
                    }
                    {
                        Id = RegionId "persistence-state"
                        Purpose = "Distinguish local, pending, synced, conflicted, and failed persistence."
                        Hierarchy = Secondary
                        Visibility = AlwaysVisible
                    }
                ]
            Capabilities =
                [
                    MustExpose(CapabilityId "confirm-root-cause")
                    MustDisableWhenIllegal(CapabilityId "confirm-root-cause")
                    MustExplainWhenUnavailable(CapabilityId "confirm-root-cause")
                    MustNavigateToBlocker(CapabilityId "confirm-root-cause")
                ]
            StateProjections =
                [
                    {
                        When = DomainPredicateId "hypothesis-is-falsified"
                        RequiredObservations =
                            [
                                observation "falsified-hypothesis-retained" "Falsified hypothesis remains represented."
                                observation "falsified-state-noncolor" "Falsified state is perceivable without color alone."
                                observation "inactive-actions-unavailable" "Actions that require an active hypothesis are unavailable."
                            ]
                    }
                    {
                        When = DomainPredicateId "blocking-unknown-exists"
                        RequiredObservations =
                            [
                                observation "blocker-visible" "Blocker is visible in the active workspace."
                                observation "dependent-action-unavailable" "Dependent action is unavailable."
                                observation "unavailable-reason-visible" "Reason for unavailability is visible."
                                observation "blocker-navigation-available" "User can navigate to the blocker."
                            ]
                    }
                    {
                        When = DomainPredicateId "github-write-is-pending"
                        RequiredObservations =
                            [
                                observation "persistence-pending-visible" "Persistence state is Pending."
                                observation "no-false-remote-success" "UI does not claim remote success."
                            ]
                    }
                    {
                        When = DomainPredicateId "corrective-action-not-verified"
                        RequiredObservations =
                            [
                                observation "unresolved-not-resolved" "Investigation is not represented as Resolved."
                                observation "verification-obligation-visible" "Verification obligation remains visible."
                            ]
                    }
                ]
            ForbiddenPatterns =
                [
                    ChatOnlyPrimaryInterface
                    HideBlockingObligation
                    DeleteFalsifiedHypothesis
                    ColorOnlyState
                    OptimisticPersistenceSuccess
                    UnsupportedConfidencePercentage
                    LinearizeCompetingHypotheses
                ]
            Breakpoints =
                [
                    {
                        Name = "ipad-portrait"
                        ViewportWidthCssPx = 820
                        ViewportHeightCssPx = 1180
                        RequiredRegionsVisible =
                            [ RegionId "hypotheses"; RegionId "unknowns"; RegionId "legal-actions" ]
                        RequiredObservations = []
                    }
                    {
                        Name = "ipad-landscape"
                        ViewportWidthCssPx = 1180
                        ViewportHeightCssPx = 820
                        RequiredRegionsVisible =
                            [ RegionId "hypotheses"; RegionId "unknowns"; RegionId "legal-actions" ]
                        RequiredObservations = []
                    }
                    {
                        Name = "narrow-phone"
                        ViewportWidthCssPx = 390
                        ViewportHeightCssPx = 844
                        RequiredRegionsVisible =
                            [ RegionId "hypotheses"; RegionId "unknowns"; RegionId "legal-actions" ]
                        RequiredObservations = []
                    }
                    {
                        Name = "desktop-reference"
                        ViewportWidthCssPx = 1440
                        ViewportHeightCssPx = 900
                        RequiredRegionsVisible =
                            [ RegionId "observation"; RegionId "hypotheses"; RegionId "unknowns"; RegionId "legal-actions" ]
                        RequiredObservations = []
                    }
                ]
            EvidenceRequirements =
                [
                    evidence "contract-validation" ContractValidation "Contract is internally valid." true
                    evidence "structural" Structural "Required semantic regions and implementation hooks are present." true
                    evidence "state-projection" StateProjection "Declared domain states project to the required user-facing meaning." true
                    evidence "interaction" Interaction "Capabilities are discoverable, constrained, and explain unavailable actions." true
                    evidence "accessibility" Accessibility "Applicable accessibility obligations pass." true
                    evidence "responsive" Responsive "Required semantics survive the declared viewport classes." true
                    evidence "visual-regression" VisualRegression "No material clipping, overlap, missing region, or hierarchy regression is observed." false
                    evidence "semantic-visual-review" SemanticVisualReview "Visual review supports the declared information hierarchy and product meaning." false
                ]
        }
