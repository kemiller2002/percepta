namespace Percepta.Examples

open Percepta.Core

module IndyInit =

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
                                "Falsified hypothesis remains represented."
                                "Falsified state is perceivable without color alone."
                                "Actions that require an active hypothesis are unavailable."
                            ]
                    }
                    {
                        When = DomainPredicateId "blocking-unknown-exists"
                        RequiredObservations =
                            [
                                "Blocker is visible in the active workspace."
                                "Dependent action is unavailable."
                                "Reason for unavailability is visible."
                                "User can navigate to the blocker."
                            ]
                    }
                    {
                        When = DomainPredicateId "github-write-is-pending"
                        RequiredObservations =
                            [
                                "Persistence state is Pending."
                                "UI does not claim remote success."
                            ]
                    }
                    {
                        When = DomainPredicateId "corrective-action-not-verified"
                        RequiredObservations =
                            [
                                "Investigation is not represented as Resolved."
                                "Verification obligation remains visible."
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
                        MinimumWidthCssPx = None
                        MaximumWidthCssPx = None
                        RequiredObservations =
                            [
                                "Active hypotheses remain directly reachable."
                                "Blocking unknowns remain visible without secondary navigation."
                                "Legal actions remain discoverable."
                            ]
                    }
                    {
                        Name = "ipad-landscape"
                        MinimumWidthCssPx = None
                        MaximumWidthCssPx = None
                        RequiredObservations =
                            [
                                "Competing hypotheses can be compared."
                                "Blocking state remains visible."
                            ]
                    }
                    {
                        Name = "narrow-phone"
                        MinimumWidthCssPx = None
                        MaximumWidthCssPx = Some 480
                        RequiredObservations =
                            [
                                "Semantic hierarchy survives stacking."
                                "No critical blocker moves behind secondary navigation."
                            ]
                    }
                    {
                        Name = "desktop-reference"
                        MinimumWidthCssPx = Some 1024
                        MaximumWidthCssPx = None
                        RequiredObservations =
                            [
                                "Primary investigation state is visually dominant."
                            ]
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
