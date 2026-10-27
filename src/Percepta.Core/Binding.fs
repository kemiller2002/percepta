namespace Percepta.Core

open System
open System.Text.Json

/// Screen bindings: how an application declares which Percepta screen contracts it implements,
/// where each implemented screen is rendered, and whether it supports test-state injection.
///
/// A contract names a product screen. An application has its own screen identities and routes.
/// The binding is the only place the two are joined. A contract the binding does not mention is
/// NOT-BOUND, which always fails; a contract the binding explicitly declares not applicable is
/// reported as NOT-APPLICABLE with its reason and is never reported as passed.
/// See docs/SCREEN-BINDING.md.
module Binding =

    [<Literal>]
    let SchemaName = "percepta.screen-bindings/1"

    type BoundScreen =
        {
            /// Contract screen id, e.g. "S02-investigation-workspace".
            Contract: ScreenId
            /// The application's own screen id, rendered as data-percepta-screen on the screen root.
            AppScreen: string
            /// Location of the screen relative to the verification base URL (path, query and hash).
            Path: string
            /// State fixture files (relative to the bindings file) exercised on this screen.
            Fixtures: string list
        }

    type ScreenDeclaration =
        | Bound of BoundScreen
        | DeclaredNotApplicable of contract: ScreenId * reason: string

    type ScreenBindings =
        {
            Application: string
            /// The application implements window.__perceptaSetState (docs/SCREEN-BINDING.md).
            StateInjection: bool
            Screens: ScreenDeclaration list
        }

    type Resolution =
        | ResolvedBound of BoundScreen
        | ResolvedNotApplicable of reason: string
        | NotBound

    type ScreenVerdict =
        | Pass
        | Fail of reason: string
        | NotBoundVerdict
        | NotApplicableVerdict of reason: string

    let private contractOf =
        function
        | Bound screen -> screen.Contract
        | DeclaredNotApplicable(contract, _) -> contract

    let private screenText (ScreenId value) = value

    let private tryString (element: JsonElement) (name: string) =
        match element.TryGetProperty(name) with
        | true, value when value.ValueKind = JsonValueKind.String && not (String.IsNullOrWhiteSpace(value.GetString())) ->
            Some(value.GetString().Trim())
        | _ -> None

    let private stringArray (element: JsonElement) (name: string) =
        match element.TryGetProperty(name) with
        | true, value when value.ValueKind = JsonValueKind.Array ->
            value.EnumerateArray()
            |> Seq.filter (fun item -> item.ValueKind = JsonValueKind.String)
            |> Seq.map (fun item -> item.GetString())
            |> List.ofSeq
        | _ -> []

    let private parseScreen index (element: JsonElement) : Result<ScreenDeclaration, string> =
        let at = $"screens[{index}]"

        match tryString element "contract", tryString element "notApplicable" with
        | None, _ -> Error $"{at}: 'contract' is required."
        | Some contract, Some reason ->
            if element.TryGetProperty("appScreen") |> fst then
                Error $"{at}: a not-applicable screen must not also declare 'appScreen'."
            else
                Ok(DeclaredNotApplicable(ScreenId contract, reason))
        | Some contract, None ->
            match tryString element "appScreen", tryString element "path" with
            | Some appScreen, Some path ->
                Ok(
                    Bound
                        {
                            Contract = ScreenId contract
                            AppScreen = appScreen
                            Path = path
                            Fixtures = stringArray element "fixtures"
                        }
                )
            | _ ->
                Error $"{at}: screen '{contract}' must declare either 'appScreen' and 'path', or a non-empty 'notApplicable' reason."

    let private duplicates (screens: ScreenDeclaration list) =
        screens
        |> List.countBy contractOf
        |> List.filter (fun (_, count) -> count > 1)
        |> List.map (fun (contract, _) -> $"Screen '{screenText contract}' is declared more than once.")

    /// Parses a percepta.screen-bindings/1 document. Pure; never throws.
    let parse (json: string) : Result<ScreenBindings, string list> =
        try
            use document = JsonDocument.Parse(json)
            let root = document.RootElement

            let header =
                [
                    match tryString root "schema" with
                    | Some schema when schema = SchemaName -> ()
                    | other ->
                        let found = defaultArg other "nothing"
                        $"'schema' must be '{SchemaName}' (found {found})."
                    match tryString root "application" with
                    | Some _ -> ()
                    | None -> "'application' is required."
                    match root.TryGetProperty("stateInjection") with
                    | true, value when value.ValueKind = JsonValueKind.True || value.ValueKind = JsonValueKind.False -> ()
                    | _ -> "'stateInjection' must be true or false."
                ]

            let screens =
                match root.TryGetProperty("screens") with
                | true, value when value.ValueKind = JsonValueKind.Array ->
                    value.EnumerateArray() |> Seq.mapi parseScreen |> List.ofSeq
                | _ -> [ Error "'screens' must be an array." ]

            let screenErrors = screens |> List.choose (function Error e -> Some e | Ok _ -> None)
            let declared = screens |> List.choose (function Ok s -> Some s | Error _ -> None)

            match header @ screenErrors @ duplicates declared with
            | [] ->
                Ok
                    {
                        Application = (tryString root "application").Value
                        StateInjection = root.GetProperty("stateInjection").GetBoolean()
                        Screens = declared
                    }
            | errors -> Error errors
        with :? JsonException as ex ->
            Error [ $"Bindings are not valid JSON: {ex.Message}" ]

    /// How the application declares a contract's screen. A contract it does not mention is NotBound.
    let resolve (bindings: ScreenBindings) (contract: ScreenId) : Resolution =
        bindings.Screens
        |> List.tryFind (fun screen -> contractOf screen = contract)
        |> Option.map (function
            | Bound screen -> ResolvedBound screen
            | DeclaredNotApplicable(_, reason) -> ResolvedNotApplicable reason)
        |> Option.defaultValue NotBound

    /// Bindings that name a contract that does not exist (stale or misspelled declarations).
    let orphans (bindings: ScreenBindings) (contracts: ScreenId list) : ScreenId list =
        let known = Set.ofList contracts

        bindings.Screens
        |> List.map contractOf
        |> List.filter (fun contract -> not (Set.contains contract known))

    /// The verdict for one contract. Only a bound screen whose required evidence is all acceptable passes.
    let verdict (resolution: Resolution) (records: Evidence.EvidenceRecord list) : ScreenVerdict =
        match resolution with
        | NotBound -> NotBoundVerdict
        | ResolvedNotApplicable reason -> NotApplicableVerdict reason
        | ResolvedBound _ ->
            let blocking =
                records
                |> List.filter (fun record -> record.Required && not (Evidence.isAcceptable record.Status))

            match records, blocking with
            | [], _ -> Fail "No evidence was produced."
            | _, [] -> Pass
            | _, failures ->
                failures
                |> List.map (fun record ->
                    let (VerificationId id) = record.Requirement
                    id)
                |> String.concat ", "
                |> fun ids -> Fail $"Required evidence not acceptable: {ids}."

    let verdictName =
        function
        | Pass -> "PASS"
        | Fail _ -> "FAIL"
        | NotBoundVerdict -> "NOT-BOUND"
        | NotApplicableVerdict _ -> "NOT-APPLICABLE"

    /// The application run is complete only when every contract passes. Declared not-applicable
    /// screens are tolerated only when the caller explicitly allows them; NOT-BOUND, FAIL and
    /// orphan bindings are never tolerated.
    let complete (allowNotApplicable: bool) (orphanCount: int) (verdicts: ScreenVerdict list) =
        orphanCount = 0
        && not (List.isEmpty verdicts)
        && verdicts
           |> List.forall (function
               | Pass -> true
               | NotApplicableVerdict _ -> allowNotApplicable
               | Fail _
               | NotBoundVerdict -> false)
