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

    type EvidenceReport =
        {
            SchemaVersion: int
            Screen: ScreenId
            ContractSha256: string
            GeneratedAtUtc: string
            Complete: bool
            Results: EvidenceRecord list
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

    let createReport contractSha256 generatedAtUtc contract results =
        {
            SchemaVersion = 1
            Screen = contract.Id
            ContractSha256 = contractSha256
            GeneratedAtUtc = generatedAtUtc
            Complete = evaluateCompletion contract results
            Results = results
        }
