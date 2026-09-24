namespace Percepta.Core

module Validation =

    type ValidationIssue =
        | EmptyScreenPurpose
        | EmptyPrimaryQuestion
        | NoRegions
        | DuplicateRegion of RegionId
        | EmptyRegionPurpose of RegionId
        | DuplicateObservation of ObservationId
        | EmptyObservationDescription of ObservationId
        | InvalidViewport of name: string
        | UnknownBreakpointRegion of breakpoint: string * region: RegionId
        | DuplicateVerificationId of VerificationId
        | MissingRequiredEvidence

    let private duplicates values =
        values
        |> List.countBy id
        |> List.choose (fun (value, count) ->
            if count > 1 then Some value else None)

    let validate (contract: ScreenContract) =
        let regionIds = contract.Regions |> List.map (fun region -> region.Id) |> Set.ofList

        let observations =
            [
                yield!
                    contract.StateProjections
                    |> List.collect (fun projection -> projection.RequiredObservations)

                yield!
                    contract.Breakpoints
                    |> List.collect (fun breakpoint -> breakpoint.RequiredObservations)
            ]

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

            for duplicate in observations |> List.map (fun observation -> observation.Id) |> duplicates do
                DuplicateObservation duplicate

            for observation in observations do
                if System.String.IsNullOrWhiteSpace observation.Description then
                    EmptyObservationDescription observation.Id

            for breakpoint in contract.Breakpoints do
                if breakpoint.ViewportWidthCssPx <= 0 || breakpoint.ViewportHeightCssPx <= 0 then
                    InvalidViewport breakpoint.Name

                for region in breakpoint.RequiredRegionsVisible do
                    if not (Set.contains region regionIds) then
                        UnknownBreakpointRegion(breakpoint.Name, region)

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
