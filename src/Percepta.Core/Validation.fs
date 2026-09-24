namespace Percepta.Core

module Validation =

    type ValidationIssue =
        | EmptyScreenPurpose
        | EmptyPrimaryQuestion
        | NoRegions
        | DuplicateRegion of RegionId
        | EmptyRegionPurpose of RegionId
        | InvalidBreakpointRange of name: string
        | DuplicateVerificationId of VerificationId
        | MissingRequiredEvidence

    let private duplicates values =
        values
        |> List.countBy id
        |> List.choose (fun (value, count) ->
            if count > 1 then Some value else None)

    let validate (contract: ScreenContract) =
        [
            if System.String.IsNullOrWhiteSpace contract.Purpose then
                EmptyScreenPurpose

            if System.String.IsNullOrWhiteSpace contract.PrimaryQuestion then
                EmptyPrimaryQuestion

            if List.isEmpty contract.Regions then
                NoRegions

            for duplicate in
                contract.Regions
                |> List.map (fun region -> region.Id)
                |> duplicates do
                DuplicateRegion duplicate

            for region in contract.Regions do
                if System.String.IsNullOrWhiteSpace region.Purpose then
                    EmptyRegionPurpose region.Id

            for breakpoint in contract.Breakpoints do
                match breakpoint.MinimumWidthCssPx, breakpoint.MaximumWidthCssPx with
                | Some minimum, Some maximum when minimum > maximum ->
                    InvalidBreakpointRange breakpoint.Name
                | _ ->
                    ()

            for duplicate in
                contract.EvidenceRequirements
                |> List.map (fun requirement -> requirement.Id)
                |> duplicates do
                DuplicateVerificationId duplicate

            if
                contract.EvidenceRequirements
                |> List.exists (fun requirement -> requirement.Required)
                |> not
            then
                MissingRequiredEvidence
        ]

    let isValid contract =
        validate contract |> List.isEmpty
