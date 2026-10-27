namespace Percepta.Adapter.Playwright

open System
open System.IO
open System.Security.Cryptography
open System.Text.Json
open System.Threading.Tasks
open Microsoft.Playwright
open Percepta.Core

module BrowserVerification =

    type AdapterEvidence =
        {
            Kind: EvidenceKind
            Status: VerificationStatus
            Summary: string
            EvidenceReferences: string list
        }

    type Options =
        {
            Url: string
            StateFixturePaths: string list
            ScreenshotDirectory: string
            BaselineDirectory: string
            UpdateBaselines: bool
        }

    type private Fixture =
        {
            Name: string
            Predicates: Set<string>
            Capabilities: Map<string, string>
            RawJson: string
        }

    let private screenId (ScreenId value) = value
    let private regionId (RegionId value) = value
    let private capabilityId (CapabilityId value) = value
    let private predicateId (DomainPredicateId value) = value
    let private observationId (ObservationId value) = value

    let private normalizeUrl (value: string) =
        match Uri.TryCreate(value, UriKind.Absolute) with
        | true, uri when uri.Scheme = Uri.UriSchemeHttp || uri.Scheme = Uri.UriSchemeHttps || uri.Scheme = Uri.UriSchemeFile ->
            uri.AbsoluteUri
        | _ ->
            Path.GetFullPath(value) |> Uri |> fun uri -> uri.AbsoluteUri

    let private loadFixture path =
        let raw = File.ReadAllText(path)
        use document = JsonDocument.Parse(raw)
        let root = document.RootElement

        let name =
            let mutable property = Unchecked.defaultof<JsonElement>

            if root.TryGetProperty("name", &property) && property.ValueKind = JsonValueKind.String then
                property.GetString()
            else
                Path.GetFileNameWithoutExtension(path)

        let predicates =
            let mutable property = Unchecked.defaultof<JsonElement>

            if root.TryGetProperty("predicates", &property) && property.ValueKind = JsonValueKind.Array then
                property.EnumerateArray()
                |> Seq.choose (fun item ->
                    if item.ValueKind = JsonValueKind.String then Some(item.GetString()) else None)
                |> Set.ofSeq
            else
                Set.empty

        let capabilities =
            let mutable property = Unchecked.defaultof<JsonElement>

            if root.TryGetProperty("capabilities", &property) && property.ValueKind = JsonValueKind.Object then
                property.EnumerateObject()
                |> Seq.choose (fun item ->
                    if item.Value.ValueKind = JsonValueKind.String then
                        Some(item.Name, item.Value.GetString().Trim().ToLowerInvariant())
                    else
                        None)
                |> Map.ofSeq
            else
                Map.empty

        {
            Name = name
            Predicates = predicates
            Capabilities = capabilities
            RawJson = raw
        }

    let private evidence kind status summary references =
        {
            Kind = kind
            Status = status
            Summary = summary
            EvidenceReferences = references
        }

    let private statusFromProblems unavailableReason problems =
        if not (List.isEmpty problems) then
            Failed(String.concat " | " problems)
        else
            match unavailableReason with
            | Some reason -> Unavailable reason
            | None -> Passed

    let private visible (page: IPage) selector =
        task {
            let locator = page.Locator(selector)
            let! count = locator.CountAsync()

            if count = 0 then
                return false
            else
                return! locator.First.IsVisibleAsync()
        }

    let private nonEmptyText (page: IPage) selector =
        task {
            let locator = page.Locator(selector)
            let! count = locator.CountAsync()

            if count = 0 then
                return false
            else
                let! text = locator.First.InnerTextAsync()
                return not (String.IsNullOrWhiteSpace text)
        }

    let private applyFixture (page: IPage) (fixture: Fixture) =
        task {
            let! _ =
                page.EvaluateAsync(
                    // The hook may return a promise; the state counts as applied only once it settles.
                    "async json => { if (typeof window.__perceptaSetState !== 'function') throw new Error('window.__perceptaSetState is not defined'); await window.__perceptaSetState(JSON.parse(json)); }",
                    fixture.RawJson
                )

            return ()
        }

    let private checkStructural (page: IPage) (contract: ScreenContract) =
        task {
            let problems = ResizeArray<string>()

            for region in contract.Regions do
                match region.Visibility with
                | AlwaysVisible ->
                    let! isVisible = visible page $"[data-percepta-region=\"{regionId region.Id}\"]"

                    if not isVisible then
                        problems.Add($"Required always-visible region '{regionId region.Id}' is not visible.")
                | _ ->
                    ()

            if contract.ForbiddenPatterns |> List.contains ChatOnlyPrimaryInterface then
                let! chatOnly = visible page "[data-percepta-chat-primary=\"true\"]"

                if chatOnly then
                    problems.Add("Chat-only primary interface marker is present.")

            if contract.ForbiddenPatterns |> List.contains UnsupportedConfidencePercentage then
                let! unsupported = visible page "[data-percepta-confidence-kind=\"unsupported\"]"

                if unsupported then
                    problems.Add("Unsupported confidence percentage is visible.")

            if contract.ForbiddenPatterns |> List.contains LinearizeCompetingHypotheses then
                let! linearized = visible page "[data-percepta-linearized-competing=\"true\"]"

                if linearized then
                    problems.Add("Competing hypotheses are forced into a linearized flow.")

            if contract.ForbiddenPatterns |> List.contains ColorOnlyState then
                let! colorOnlyCount =
                    page.Locator("[data-percepta-state]").EvaluateAllAsync<int>(
                        "els => els.filter(el => !((el.textContent || '').trim()) && !(el.getAttribute('aria-label') || '').trim()).length"
                    )

                if colorOnlyCount > 0 then
                    problems.Add($"{colorOnlyCount} state element(s) communicate state without text or an accessible label.")

            // Component boundaries: each contract region is one element, regions do not nest inside one
            // another, and capability hooks sit on native interactive controls (the semantic and interaction
            // authority for Forma components), not on generic containers.
            let regionIds = contract.Regions |> List.map (fun region -> regionId region.Id) |> List.toArray

            let! boundaryProblems =
                page.EvaluateAsync<string array>(
                    """ids => {
                      const problems = [];
                      const roots = new Map();
                      for (const id of ids) {
                        const matches = [...document.querySelectorAll('[data-percepta-region]')].filter(el => el.getAttribute('data-percepta-region') === id);
                        if (matches.length > 1) problems.push(`Region '${id}' has ${matches.length} boundaries; a region must be exactly one element.`);
                        if (matches.length >= 1) roots.set(id, matches[0]);
                      }
                      for (const [id, el] of roots) {
                        for (const [other, otherEl] of roots) {
                          if (id !== other && otherEl.contains(el)) problems.push(`Region '${id}' is nested inside region '${other}'.`);
                        }
                      }
                      const interactive = 'button, a[href], input, select, textarea, summary, [role=button], [role=link], [role=menuitem], [role=tab], [role=switch], [role=checkbox]';
                      for (const el of document.querySelectorAll('[data-percepta-capability]')) {
                        if (!el.matches(interactive)) problems.push(`Capability '${el.getAttribute('data-percepta-capability')}' is bound to a non-interactive <${el.tagName.toLowerCase()}>.`);
                      }
                      return problems;
                    }""",
                    regionIds
                )

            problems.AddRange boundaryProblems

            return
                evidence
                    Structural
                    (if problems.Count = 0 then Passed else Failed(String.concat " | " problems))
                    (if problems.Count = 0 then "Required semantic structure is present." else "Structural verification failed.")
                    []
        }

    let private checkStateProjections (page: IPage) (contract: ScreenContract) (fixtures: Fixture list) =
        task {
            let problems = ResizeArray<string>()
            let uncovered = ResizeArray<string>()

            for projection in contract.StateProjections do
                let predicate = predicateId projection.When

                match fixtures |> List.tryFind (fun fixture -> Set.contains predicate fixture.Predicates) with
                | None ->
                    uncovered.Add predicate
                | Some fixture ->
                    do! applyFixture page fixture

                    for observation in projection.RequiredObservations do
                        let! isVisible =
                            visible page $"[data-percepta-observation=\"{observationId observation.Id}\"]"

                        if not isVisible then
                            problems.Add(
                                $"Fixture '{fixture.Name}' activates '{predicate}', but observation '{observationId observation.Id}' is not visible."
                            )

            let unavailable =
                if uncovered.Count = 0 then
                    None
                else
                    let uncoveredText = String.concat ", " uncovered
                    Some($"No state fixture covers: {uncoveredText}.")

            return
                evidence
                    StateProjection
                    (statusFromProblems unavailable (List.ofSeq problems))
                    (if problems.Count = 0 && uncovered.Count = 0 then
                         "All declared state projections are exercised and visible."
                     else
                         "State projection coverage is incomplete or incorrect.")
                    (fixtures |> List.map (fun fixture -> fixture.Name))
        }

    let private checkInteraction (page: IPage) (contract: ScreenContract) (fixtures: Fixture list) =
        task {
            let problems = ResizeArray<string>()
            let unavailable = ResizeArray<string>()

            for projection in contract.Capabilities do
                let capability =
                    match projection with
                    | MustExpose id
                    | MustEnableWhenLegal id
                    | MustDisableWhenIllegal id
                    | MustExplainWhenUnavailable id
                    | MustNavigateToBlocker id -> id

                let id = capabilityId capability
                let selector = $"[data-percepta-capability=\"{id}\"]"

                match projection with
                | MustExpose _ ->
                    let! isVisible = visible page selector

                    if not isVisible then
                        problems.Add($"Capability '{id}' is not exposed.")
                | MustEnableWhenLegal _ ->
                    match fixtures |> List.tryFind (fun fixture -> Map.tryFind id fixture.Capabilities = Some "legal") with
                    | None -> unavailable.Add($"No fixture declares capability '{id}' legal.")
                    | Some fixture ->
                        do! applyFixture page fixture
                        let locator = page.Locator(selector)
                        let! count = locator.CountAsync()

                        if count = 0 then
                            problems.Add($"Capability '{id}' is missing in legal fixture '{fixture.Name}'.")
                        else
                            let! disabled = locator.First.IsDisabledAsync()

                            if disabled then
                                problems.Add($"Capability '{id}' is disabled even though fixture '{fixture.Name}' declares it legal.")
                | MustDisableWhenIllegal _ ->
                    match fixtures |> List.tryFind (fun fixture -> Map.tryFind id fixture.Capabilities = Some "illegal") with
                    | None -> unavailable.Add($"No fixture declares capability '{id}' illegal.")
                    | Some fixture ->
                        do! applyFixture page fixture
                        let locator = page.Locator(selector)
                        let! count = locator.CountAsync()

                        if count = 0 then
                            problems.Add($"Capability '{id}' is missing in illegal fixture '{fixture.Name}'.")
                        else
                            let! disabled = locator.First.IsDisabledAsync()
                            let! ariaDisabled = locator.First.GetAttributeAsync("aria-disabled")

                            if not disabled && ariaDisabled <> "true" then
                                problems.Add($"Capability '{id}' remains executable in illegal fixture '{fixture.Name}'.")
                | MustExplainWhenUnavailable _ ->
                    match fixtures |> List.tryFind (fun fixture -> Map.tryFind id fixture.Capabilities = Some "illegal") with
                    | None -> unavailable.Add($"No fixture declares capability '{id}' unavailable.")
                    | Some fixture ->
                        do! applyFixture page fixture
                        let! hasReason = nonEmptyText page $"[data-percepta-unavailable-reason-for=\"{id}\"]"

                        if not hasReason then
                            problems.Add($"Capability '{id}' has no visible unavailable explanation.")
                | MustNavigateToBlocker _ ->
                    match fixtures |> List.tryFind (fun fixture -> Map.tryFind id fixture.Capabilities = Some "illegal") with
                    | None -> unavailable.Add($"No fixture declares capability '{id}' blocked.")
                    | Some fixture ->
                        do! applyFixture page fixture
                        let locator = page.Locator($"[data-percepta-blocker-link-for=\"{id}\"]")
                        let! count = locator.CountAsync()

                        if count = 0 then
                            problems.Add($"Capability '{id}' has no visible path to blocking information.")
                        else
                            let! isVisible = locator.First.IsVisibleAsync()

                            if not isVisible then
                                problems.Add($"Capability '{id}' has no visible path to blocking information.")

                            let! href = locator.First.GetAttributeAsync("href")

                            if String.IsNullOrWhiteSpace href || not (href.StartsWith("#", StringComparison.Ordinal)) then
                                problems.Add($"Capability '{id}' blocker navigation does not target an in-page blocker.")
                            else
                                let targetId = href.Substring(1)
                                let! targetCount = page.Locator($"#{targetId}").CountAsync()

                                if targetCount = 0 then
                                    problems.Add($"Capability '{id}' blocker target '#{targetId}' does not exist.")

            let unavailableReason =
                if unavailable.Count = 0 then None else Some(String.concat " | " unavailable)

            return
                evidence
                    Interaction
                    (statusFromProblems unavailableReason (List.ofSeq problems))
                    (if problems.Count = 0 && unavailable.Count = 0 then
                         "Capability exposure and legality projections are correct."
                     else
                         "Interaction verification is incomplete or incorrect.")
                    (fixtures |> List.map (fun fixture -> fixture.Name))
        }

    /// Presses Tab until every expected capability was focused or the budget is spent.
    let rec private tabWalk (page: IPage) (expected: Set<string>) remaining (reached: Map<string, bool>) : Task<Map<string, bool>> =
        task {
            if remaining = 0 || Set.isSubset expected (reached |> Map.keys |> Set.ofSeq) then
                return reached
            else
                do! page.Keyboard.PressAsync("Tab")

                let! focused =
                    page.EvaluateAsync<string array>(
                        """() => {
                          const el = document.activeElement;
                          const id = el && el.getAttribute && el.getAttribute('data-percepta-capability');
                          if (!id) return [];
                          const style = getComputedStyle(el);
                          const indicated = style.outlineStyle !== 'none' && parseFloat(style.outlineWidth || '0') > 0 || style.boxShadow !== 'none' || style.outlineStyle === 'auto';
                          return [id, String(indicated)];
                        }"""
                    )

                let next =
                    match focused with
                    | [| id; indicated |] -> Map.add id (indicated = "true") reached
                    | _ -> reached

                return! tabWalk page expected (remaining - 1) next
        }


    /// Keyboard accessibility: every visible, enabled capability control must be reachable with Tab from
    /// the start of the document and must show a focus indicator when reached by keyboard.
    let private checkKeyboardReachability (page: IPage) =
        task {
            let! expected =
                page.EvaluateAsync<string array>(
                    """() => [...document.querySelectorAll('[data-percepta-capability]')]
                      .filter(el => el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden')
                      .filter(el => !el.disabled && el.getAttribute('aria-disabled') !== 'true')
                      .map(el => el.getAttribute('data-percepta-capability'))"""
                )

            if expected.Length = 0 then
                return [||]
            else
                let! focusableCount =
                    page.EvaluateAsync<int>(
                        """() => {
                          if (document.activeElement && document.activeElement !== document.body) document.activeElement.blur();
                          return document.querySelectorAll('a[href], button, input, select, textarea, summary, [tabindex]').length;
                        }"""
                    )

                let! reached = tabWalk page (Set.ofArray expected) (min 400 (focusableCount + 2)) Map.empty

                return
                    expected
                    |> Array.distinct
                    |> Array.choose (fun id ->
                        match Map.tryFind id reached with
                        | None -> Some $"Capability '{id}' is not reachable with the keyboard (Tab)."
                        | Some false -> Some $"Capability '{id}' has no visible focus indicator."
                        | Some true -> None)
        }

    /// Keyboard reachability in every supplied state (capabilities are often enabled only in some states),
    /// or in the current state when no state fixture is supplied.
    let rec private keyboardAcrossStates (page: IPage) (fixtures: Fixture list) (problems: string list) : Task<string list> =
        task {
            match fixtures with
            | [] -> return problems
            | fixture :: rest ->
                do! applyFixture page fixture
                let! found = checkKeyboardReachability page
                let labelled = found |> Array.map (fun problem -> $"[{fixture.Name}] {problem}") |> List.ofArray
                return! keyboardAcrossStates page rest (problems @ labelled)
        }

    let private checkAccessibility (page: IPage) (fixtures: Fixture list) =
        task {
            let! problems =
                page.EvaluateAsync<string array>(
                    """() => {
                      const problems = [];
                      const html = document.documentElement;
                      if (!html.getAttribute('lang')) problems.push('html[lang] is missing.');
                      if (!document.querySelector('main')) problems.push('A main landmark is missing.');
                      if (document.querySelectorAll('h1').length !== 1) problems.push('Exactly one h1 is required.');
                      const ids = [...document.querySelectorAll('[id]')].map(el => el.id).filter(Boolean);
                      const duplicates = ids.filter((id, i) => ids.indexOf(id) !== i);
                      if (duplicates.length) problems.push('Duplicate DOM id(s): ' + [...new Set(duplicates)].join(', '));
                      for (const img of document.querySelectorAll('img')) {
                        if (!img.hasAttribute('alt')) problems.push('An image is missing alt text.');
                      }
                      for (const el of document.querySelectorAll('button, a[href], input, select, textarea')) {
                        const style = getComputedStyle(el);
                        const visible = style.display !== 'none' && style.visibility !== 'hidden';
                        if (!visible) continue;
                        const name = (el.getAttribute('aria-label') || el.getAttribute('title') || el.textContent || '').trim();
                        const labelledBy = el.getAttribute('aria-labelledby');
                        if (!name && !labelledBy) problems.push('Interactive element lacks an accessible name: ' + el.tagName.toLowerCase());
                      }
                      for (const el of document.querySelectorAll('[tabindex]')) {
                        if (el.tabIndex > 0) problems.push('Positive tabindex overrides the document focus order: ' + el.tagName.toLowerCase());
                      }
                      return problems;
                    }"""
                )

            let! keyboardProblems =
                match fixtures with
                | [] -> checkKeyboardReachability page
                | _ -> task {
                    let! found = keyboardAcrossStates page fixtures []
                    return Array.ofList found }

            let problems = Array.append problems keyboardProblems

            return
                evidence
                    Accessibility
                    (if problems.Length = 0 then Passed else Failed(String.concat " | " problems))
                    (if problems.Length = 0 then "Basic accessibility invariants pass." else "Accessibility verification failed.")
                    []
        }

    let private sha256 path =
        use stream = File.OpenRead(path)
        SHA256.HashData(stream) |> Convert.ToHexString |> fun value -> value.ToLowerInvariant()

    let private checkResponsiveAndScreenshots (page: IPage) (contract: ScreenContract) (options: Options) (fixture: Fixture option) =
        task {
            Directory.CreateDirectory(options.ScreenshotDirectory) |> ignore
            Directory.CreateDirectory(options.BaselineDirectory) |> ignore

            let responsiveProblems = ResizeArray<string>()
            let visualProblems = ResizeArray<string>()
            let visualUnavailable = ResizeArray<string>()
            let screenshots = ResizeArray<string>()

            for breakpoint in contract.Breakpoints do
                do! page.SetViewportSizeAsync(breakpoint.ViewportWidthCssPx, breakpoint.ViewportHeightCssPx)

                match fixture with
                | Some state -> do! applyFixture page state
                | None -> ()

                for region in breakpoint.RequiredRegionsVisible do
                    let! isVisible = visible page $"[data-percepta-region=\"{regionId region}\"]"

                    if not isVisible then
                        responsiveProblems.Add($"Breakpoint '{breakpoint.Name}' hides required region '{regionId region}'.")

                for observation in breakpoint.RequiredObservations do
                    let! isVisible =
                        visible page $"[data-percepta-observation=\"{observationId observation.Id}\"]"

                    if not isVisible then
                        responsiveProblems.Add(
                            $"Breakpoint '{breakpoint.Name}' does not expose observation '{observationId observation.Id}'."
                        )

                let! noHorizontalOverflow =
                    page.EvaluateAsync<bool>(
                        "() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1"
                    )

                if not noHorizontalOverflow then
                    responsiveProblems.Add($"Breakpoint '{breakpoint.Name}' has horizontal document overflow.")

                let screenshotPath =
                    Path.Combine(options.ScreenshotDirectory, $"{breakpoint.Name}.png")

                do!
                    page.ScreenshotAsync(
                        PageScreenshotOptions(
                            Path = screenshotPath,
                            FullPage = true
                        )
                    )
                    :> Task

                screenshots.Add screenshotPath

                let baselinePath =
                    Path.Combine(options.BaselineDirectory, $"{breakpoint.Name}.png")

                if options.UpdateBaselines then
                    File.Copy(screenshotPath, baselinePath, true)
                elif File.Exists baselinePath then
                    if sha256 screenshotPath <> sha256 baselinePath then
                        visualProblems.Add($"Screenshot for '{breakpoint.Name}' differs from its exact baseline.")
                else
                    visualUnavailable.Add($"No baseline exists for '{breakpoint.Name}'.")

            let responsive =
                evidence
                    Responsive
                    (if responsiveProblems.Count = 0 then Passed else Failed(String.concat " | " responsiveProblems))
                    (if responsiveProblems.Count = 0 then
                         "Required semantics survive all declared viewport classes."
                     else
                         "Responsive verification failed.")
                    (List.ofSeq screenshots)

            let visualStatus =
                if visualProblems.Count > 0 then
                    Failed(String.concat " | " visualProblems)
                elif options.UpdateBaselines then
                    Passed
                elif visualUnavailable.Count > 0 then
                    Unavailable(String.concat " | " visualUnavailable)
                else
                    Passed

            let visual =
                evidence
                    VisualRegression
                    visualStatus
                    (if options.UpdateBaselines then
                         "Visual baselines were updated from current screenshots."
                     elif visualProblems.Count = 0 && visualUnavailable.Count = 0 then
                         "Screenshots match exact baselines."
                     else
                         "Visual regression evidence is incomplete or differs.")
                    (List.ofSeq screenshots)

            return responsive, visual
        }

    /// An application screen a contract is bound to (see Percepta.Core.Binding and docs/SCREEN-BINDING.md).
    type ScreenTarget =
        {
            /// Expected value of the data-percepta-screen attribute on the rendered screen root.
            AppScreen: string
            /// The application declares that it implements window.__perceptaSetState.
            StateInjection: bool
            /// How long to wait for the screen identity to appear (applications that boot asynchronously).
            ReadyTimeoutMs: float
        }

    let private adapterKinds =
        [ Structural; StateProjection; Interaction; Accessibility; Responsive; VisualRegression ]

    let private runChecks (page: IPage) options contract fixtures =
        task {
            match fixtures with
            | first :: _ -> do! applyFixture page first
            | [] -> ()

            let! structural = checkStructural page contract
            let! stateProjection = checkStateProjections page contract fixtures
            let! interaction = checkInteraction page contract fixtures
            let! accessibility = checkAccessibility page fixtures
            let! responsive, visual =
                checkResponsiveAndScreenshots page contract options (fixtures |> List.tryHead)

            return [ structural; stateProjection; interaction; accessibility; responsive; visual ]
        }

    let private withBrowser (url: string) (body: IPage -> Task<AdapterEvidence list>) =
        task {
            try
                let! playwright = Microsoft.Playwright.Playwright.CreateAsync()
                use playwright = playwright

                let! browser =
                    playwright.Chromium.LaunchAsync(
                        BrowserTypeLaunchOptions(Headless = true)
                    )

                let! page = browser.NewPageAsync()

                do! page.GotoAsync(url) :> Task

                let! results = body page
                do! browser.CloseAsync()
                return results
            with ex ->
                return
                    adapterKinds
                    |> List.map (fun kind ->
                        evidence
                            kind
                            (Unavailable ex.Message)
                            "Browser verification could not run."
                            [])
        }

    let verify options contract =
        let fixtures = options.StateFixturePaths |> List.map loadFixture
        withBrowser (normalizeUrl options.Url) (fun page -> runChecks page options contract fixtures)

    /// Waits for the bound screen's identity. Error carries the identities actually rendered.
    let private establishScreen (page: IPage) (target: ScreenTarget) =
        task {
            let selector = $"[data-percepta-screen=\"{target.AppScreen}\"]"

            try
                let! _ =
                    page.WaitForSelectorAsync(
                        selector,
                        PageWaitForSelectorOptions(State = WaitForSelectorState.Attached, Timeout = Nullable(float32 target.ReadyTimeoutMs))
                    )

                let! count = page.Locator(selector).CountAsync()

                return
                    if count = 1 then
                        Ok()
                    else
                        Error $"Screen identity '{target.AppScreen}' is rendered {count} times; exactly one screen root is required."
            with :? TimeoutException ->
                let! rendered =
                    page.EvaluateAsync<string array>(
                        "() => [...document.querySelectorAll('[data-percepta-screen]')].map(el => el.getAttribute('data-percepta-screen'))"
                    )

                let found =
                    if rendered.Length = 0 then "no data-percepta-screen element" else String.Join(", ", rendered)

                return Error $"Expected screen '{target.AppScreen}' was not rendered within {target.ReadyTimeoutMs} ms (found: {found})."
        }

    /// Replaces the status of state-dependent evidence. A failure the checks already found is never
    /// downgraded: it is kept, and a new failure reason is appended to it.
    let private overrideKinds kinds status summary (results: AdapterEvidence list) =
        results
        |> List.map (fun result ->
            match List.contains result.Kind kinds, result.Status, status with
            | false, _, _ -> result
            | true, Failed existing, Failed added -> { result with Status = Failed $"{added} | {existing}"; Summary = summary }
            | true, Failed _, _ -> result
            | true, _, _ -> { result with Status = status; Summary = summary })

    let private legalityDependent (contract: ScreenContract) =
        contract.Capabilities
        |> List.exists (function
            | MustExpose _ -> false
            | _ -> true)

    /// Verifies a contract against the application screen it is bound to. The screen identity must be
    /// established before any other check runs; state-dependent checks report the state-injection gap
    /// instead of silently checking the default state.
    let verifyBound (target: ScreenTarget) (options: Options) (contract: ScreenContract) =
        let fixtures = options.StateFixturePaths |> List.map loadFixture

        withBrowser (normalizeUrl options.Url) (fun page ->
            task {
                match! establishScreen page target with
                | Error reason ->
                    return
                        adapterKinds
                        |> List.map (fun kind ->
                            evidence
                                kind
                                (Failed $"SCREEN-IDENTITY: {reason}")
                                "Checks were not run: the bound screen was not established."
                                [])
                | Ok() ->
                    let! hookPresent =
                        page.EvaluateAsync<bool>("() => typeof window.__perceptaSetState === 'function'")

                    let stateDependent =
                        [
                            if not (List.isEmpty contract.StateProjections) then StateProjection
                            if legalityDependent contract then Interaction
                        ]

                    match target.StateInjection, hookPresent with
                    | true, true ->
                        return! runChecks page options contract fixtures
                    | true, false ->
                        let! results = runChecks page options contract []

                        return
                            overrideKinds
                                stateDependent
                                (Failed $"STATE-INJECTION-MISSING: the binding declares state injection, but window.__perceptaSetState is not a function on screen '{target.AppScreen}'.")
                                "State-dependent obligations cannot be exercised."
                                results
                    | false, _ ->
                        let! results = runChecks page options contract []

                        return
                            overrideKinds
                                stateDependent
                                (Unavailable "STATE-INJECTION-UNSUPPORTED: the application declares no state-injection hook, so state-dependent obligations cannot be exercised.")
                                "State-dependent obligations cannot be exercised."
                                results
            })


    let doctor () =
        task {
            try
                let! playwright = Microsoft.Playwright.Playwright.CreateAsync()
                use playwright = playwright
                let! browser = playwright.Chromium.LaunchAsync(BrowserTypeLaunchOptions(Headless = true))
                do! browser.CloseAsync()
                return Ok "Playwright Chromium launched successfully."
            with ex ->
                return Error ex.Message
        }

    let installChromium () =
        Microsoft.Playwright.Program.Main([| "install"; "--with-deps"; "chromium" |])
