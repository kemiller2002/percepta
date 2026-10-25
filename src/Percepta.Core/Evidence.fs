namespace Percepta.Core

module Evidence =

    type EvidenceRecord =
        {
            Requirement: VerificationId
            Kind: EvidenceKind
            Required: bool
            Status: VerificationStatus
            Summary: string
            EvidenceReferences: string list
            Source: string
        }

    let isAcceptable =
        function
        | Passed
        | NotApplicable _
        | AcceptedDeviation _ -> true
        | Failed _
        | Unavailable _ -> false

    let evaluateCompletion (contract: ScreenContract) (results: EvidenceRecord list) =
        contract.EvidenceRequirements
        |> List.filter (fun requirement -> requirement.Required)
        |> List.forall (fun requirement ->
            results
            |> List.tryFind (fun result -> result.Requirement = requirement.Id)
            |> Option.map (fun result -> isAcceptable result.Status)
            |> Option.defaultValue false)
