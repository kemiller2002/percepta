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
                    "json => { if (typeof window.__perceptaSetState !== 'function') throw new Error('window.__perceptaSetState is not defined'); window.__perceptaSetState(JSON.parse(json)); }",
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

    let private checkAccessibility (page: IPage) =
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
                      return problems;
                    }"""
                )

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

    let verify options contract =
        task {
            let fixtures = options.StateFixturePaths |> List.map loadFixture
            let url = normalizeUrl options.Url

            try
                let! playwright = Microsoft.Playwright.Playwright.CreateAsync()
                use playwright = playwright

                let! browser =
                    playwright.Chromium.LaunchAsync(
                        BrowserTypeLaunchOptions(Headless = true)
                    )

                let! page = browser.NewPageAsync()

                do! page.GotoAsync(url) :> Task

                match fixtures with
                | first :: _ -> do! applyFixture page first
                | [] -> ()

                let! structural = checkStructural page contract
                let! stateProjection = checkStateProjections page contract fixtures
                let! interaction = checkInteraction page contract fixtures
                let! accessibility = checkAccessibility page
                let! responsive, visual =
                    checkResponsiveAndScreenshots page contract options (fixtures |> List.tryHead)

                do! browser.CloseAsync()

                return
                    [
                        structural
                        stateProjection
                        interaction
                        accessibility
                        responsive
                        visual
                    ]
            with ex ->
                let unavailable kind =
                    evidence
                        kind
                        (Unavailable ex.Message)
                        "Browser verification could not run."
                        []

                return
                    [
                        unavailable Structural
                        unavailable StateProjection
                        unavailable Interaction
                        unavailable Accessibility
                        unavailable Responsive
                        unavailable VisualRegression
                    ]
        }

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
