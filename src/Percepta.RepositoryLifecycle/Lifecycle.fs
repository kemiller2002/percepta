namespace Percepta.RepositoryLifecycle

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json

type InstallationManifest =
    { SchemaVersion: int
      Component: string
      InstalledVersion: string
      ConfigurationVersion: int
      ManagedRegionSha256: string }

type LifecycleState =
    | Uninstalled
    | Current
    | UpgradeRequired of installedVersion: string
    | NewerThanCli of installedVersion: string
    | Invalid of errors: string list

type PlannedChange =
    | WriteFile of relativePath: string * content: string

type Inspection =
    { Manifest: InstallationManifest option
      State: LifecycleState
      Errors: string list }

module Lifecycle =
    [<Literal>]
    let ToolVersion = "0.1.0"

    [<Literal>]
    let private ConfigurationVersion = 1

    [<Literal>]
    let private ManifestPath = ".echelon/percepta.json"

    [<Literal>]
    let private AgentsPath = "AGENTS.md"

    [<Literal>]
    let private BeginMarker = "<!-- echelon:percepta:start -->"

    [<Literal>]
    let private EndMarker = "<!-- echelon:percepta:end -->"

    let private regionBody =
        """## Percepta semantic UI contracts

Managed by `percepta-repo`. Do not edit inside this block.

For consequential UI creation or modification:

1. read the applicable Percepta screen contract before implementation;
2. preserve its purpose, primary user question, semantic regions, hierarchy, state projections, capability projections, forbidden patterns, and verification obligations;
3. treat application/Ordo state as authoritative for legality; Percepta verifies that the UI exposes that meaning and does not invent domain truth;
4. validate/compile the applicable contract and run rendered verification when a runnable target is available;
5. do not claim UI work complete while required Percepta evidence is Failed or Unavailable.

Semantic commands belong to the separate `percepta` executable. Repository installation commands belong to `percepta-repo`; do not treat lifecycle health as semantic UI evidence.

Stable verification hooks include:

- `data-percepta-region`
- `data-percepta-observation`
- `data-percepta-capability`
- `data-percepta-unavailable-reason-for`
- `data-percepta-blocker-link-for`"""

    let private canonicalRegion =
        String.concat Environment.NewLine [ BeginMarker; regionBody; EndMarker ]

    let private sha256 (value: string) =
        Encoding.UTF8.GetBytes(value)
        |> SHA256.HashData
        |> Convert.ToHexString
        |> fun value -> value.ToLowerInvariant()

    let private canonicalRegionSha = sha256 canonicalRegion

    let private targetPath target relativePath =
        Path.Combine(Path.GetFullPath target, relativePath)

    let private ensureParent path =
        match Path.GetDirectoryName path with
        | null -> ()
        | parent when String.IsNullOrWhiteSpace parent -> ()
        | parent -> Directory.CreateDirectory parent |> ignore

    let private atomicWrite path content =
        ensureParent path
        let temporary = $"{path}.percepta-{Guid.NewGuid():N}.tmp"

        try
            File.WriteAllText(temporary, content, UTF8Encoding(false))
            File.Move(temporary, path, true)
        finally
            if File.Exists temporary then
                File.Delete temporary

    let private tryProperty name (element: JsonElement) =
        let mutable value = Unchecked.defaultof<JsonElement>
        if element.TryGetProperty(name, &value) then Some value else None

    let private requiredString name (element: JsonElement) =
        match tryProperty name element with
        | Some value when value.ValueKind = JsonValueKind.String ->
            value.GetString() |> Option.ofObj
        | _ -> None

    let private requiredInt name (element: JsonElement) =
        match tryProperty name element with
        | Some value when value.ValueKind = JsonValueKind.Number ->
            match value.TryGetInt32() with
            | true, number -> Some number
            | _ -> None
        | _ -> None

    let private readManifest target =
        let path = targetPath target ManifestPath

        if not (File.Exists path) then
            Ok None
        else
            try
                use document = JsonDocument.Parse(File.ReadAllText path)
                let root = document.RootElement

                match
                    requiredInt "schemaVersion" root,
                    requiredString "component" root,
                    requiredString "installedVersion" root,
                    requiredInt "configurationVersion" root,
                    requiredString "managedRegionSha256" root
                with
                | Some schemaVersion, Some component, Some installedVersion, Some configurationVersion, Some managedRegionSha256 ->
                    Ok(
                        Some
                            { SchemaVersion = schemaVersion
                              Component = component
                              InstalledVersion = installedVersion
                              ConfigurationVersion = configurationVersion
                              ManagedRegionSha256 = managedRegionSha256 }
                    )
                | _ ->
                    Error [ $"{ManifestPath} is missing required lifecycle fields." ]
            with
            | :? JsonException as ex -> Error [ $"{ManifestPath} is not valid JSON: {ex.Message}" ]
            | ex -> Error [ $"Unable to read {ManifestPath}: {ex.Message}" ]

    let private manifestContent () =
        use stream = new MemoryStream()
        let mutable options = JsonWriterOptions()
        options.Indented <- true
        use writer = new Utf8JsonWriter(stream, options)
        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", 1)
        writer.WriteString("component", "percepta")
        writer.WriteString("installedVersion", ToolVersion)
        writer.WriteNumber("configurationVersion", ConfigurationVersion)
        writer.WriteString("managedRegionSha256", canonicalRegionSha)
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine

    type private RegionInspection =
        | RegionAbsent of fileContent: string option
        | RegionPresent of fileContent: string * startIndex: int * endExclusive: int * region: string
        | RegionInvalid of error: string

    let private inspectRegion target =
        let path = targetPath target AgentsPath

        if not (File.Exists path) then
            RegionAbsent None
        else
            let content = File.ReadAllText path
            let startIndex = content.IndexOf(BeginMarker, StringComparison.Ordinal)
            let endIndex = content.IndexOf(EndMarker, StringComparison.Ordinal)

            match startIndex, endIndex with
            | -1, -1 -> RegionAbsent(Some content)
            | -1, _ -> RegionInvalid $"{AgentsPath} contains the Percepta end marker without the begin marker."
            | _, -1 -> RegionInvalid $"{AgentsPath} contains the Percepta begin marker without the end marker."
            | startIndex, endIndex when endIndex < startIndex ->
                RegionInvalid $"{AgentsPath} has malformed Percepta managed-region marker order."
            | startIndex, endIndex ->
                let secondStart = content.IndexOf(BeginMarker, startIndex + BeginMarker.Length, StringComparison.Ordinal)
                let secondEnd = content.IndexOf(EndMarker, endIndex + EndMarker.Length, StringComparison.Ordinal)

                if secondStart >= 0 || secondEnd >= 0 then
                    RegionInvalid $"{AgentsPath} contains duplicate Percepta managed-region markers."
                else
                    let endExclusive = endIndex + EndMarker.Length
                    let region = content.Substring(startIndex, endExclusive - startIndex)
                    RegionPresent(content, startIndex, endExclusive, region)

    let private appendRegion existing =
        match existing with
        | None -> canonicalRegion + Environment.NewLine
        | Some content when content.Length = 0 -> canonicalRegion + Environment.NewLine
        | Some content ->
            let separator =
                if content.EndsWith(Environment.NewLine + Environment.NewLine, StringComparison.Ordinal) then
                    String.Empty
                elif content.EndsWith(Environment.NewLine, StringComparison.Ordinal) then
                    Environment.NewLine
                else
                    Environment.NewLine + Environment.NewLine

            content + separator + canonicalRegion + Environment.NewLine

    let private replaceRegion content startIndex endExclusive =
        content.Substring(0, startIndex)
        + canonicalRegion
        + content.Substring(endExclusive)

    let private parseVersion value =
        match Version.TryParse value with
        | true, version -> Ok version
        | _ -> Error $"Version '{value}' is not a valid semantic numeric version."

    let private compareInstalled installed =
        match parseVersion installed, parseVersion ToolVersion with
        | Ok installedVersion, Ok toolVersion -> Ok(compare installedVersion toolVersion)
        | Error error, _
        | _, Error error -> Error error

    let private manifestErrors (manifest: InstallationManifest) =
        [ if manifest.SchemaVersion <> 1 then
              yield $"Unsupported Percepta installation schemaVersion {manifest.SchemaVersion}."
          if manifest.Component <> "percepta" then
              yield $"Unexpected component '{manifest.Component}' in {ManifestPath}."
          if manifest.ConfigurationVersion <> ConfigurationVersion then
              yield
                  $"Unsupported Percepta configurationVersion {manifest.ConfigurationVersion}; this CLI supports {ConfigurationVersion}." ]

    let private regionIntegrityErrors target (manifest: InstallationManifest) =
        match inspectRegion target with
        | RegionInvalid error -> [ error ]
        | RegionAbsent _ -> [ $"{AgentsPath} is missing the managed Percepta region." ]
        | RegionPresent(_, _, _, region) ->
            let actualSha = sha256 region

            [ if not (String.Equals(actualSha, manifest.ManagedRegionSha256, StringComparison.OrdinalIgnoreCase)) then
                  yield
                      $"{AgentsPath} Percepta region differs from the version recorded by {ManifestPath}; Conditor/Percepta will not overwrite a local edit." ]

    let private currentVerificationErrors target (manifest: InstallationManifest) =
        manifestErrors manifest
        @ [ if not (String.Equals(manifest.ManagedRegionSha256, canonicalRegionSha, StringComparison.OrdinalIgnoreCase)) then
                yield $"{ManifestPath} does not describe the canonical managed region for Percepta {ToolVersion}." ]
        @ (match inspectRegion target with
           | RegionInvalid error -> [ error ]
           | RegionAbsent _ -> [ $"{AgentsPath} is missing the managed Percepta region." ]
           | RegionPresent(_, _, _, region) ->
               if region = canonicalRegion then
                   []
               else
                   [ $"{AgentsPath} Percepta region is not canonical for Percepta {ToolVersion}." ])

    let inspect target =
        match readManifest target with
        | Error errors ->
            { Manifest = None
              State = Invalid errors
              Errors = errors }
        | Ok None ->
            { Manifest = None
              State = Uninstalled
              Errors = [] }
        | Ok(Some manifest) ->
            let baseErrors = manifestErrors manifest

            if not baseErrors.IsEmpty then
                { Manifest = Some manifest
                  State = Invalid baseErrors
                  Errors = baseErrors }
            else
                match compareInstalled manifest.InstalledVersion with
                | Error error ->
                    { Manifest = Some manifest
                      State = Invalid [ error ]
                      Errors = [ error ] }
                | Ok comparison when comparison > 0 ->
                    { Manifest = Some manifest
                      State = NewerThanCli manifest.InstalledVersion
                      Errors = [] }
                | Ok comparison when comparison < 0 ->
                    let errors = regionIntegrityErrors target manifest

                    if errors.IsEmpty then
                        { Manifest = Some manifest
                          State = UpgradeRequired manifest.InstalledVersion
                          Errors = [] }
                    else
                        { Manifest = Some manifest
                          State = Invalid errors
                          Errors = errors }
                | Ok _ ->
                    let errors = currentVerificationErrors target manifest

                    if errors.IsEmpty then
                        { Manifest = Some manifest
                          State = Current
                          Errors = [] }
                    else
                        { Manifest = Some manifest
                          State = Invalid errors
                          Errors = errors }

    let verify target =
        let inspection = inspect target

        match inspection.State with
        | Current -> Ok()
        | Uninstalled -> Error [ "Percepta repository lifecycle is not installed. Run 'percepta-repo init'." ]
        | UpgradeRequired version ->
            Error [ $"Percepta repository lifecycle {version} is installed; run 'percepta-repo upgrade' to {ToolVersion}." ]
        | NewerThanCli version ->
            Error [ $"Repository was installed by newer Percepta lifecycle {version}; upgrade this CLI." ]
        | Invalid errors -> Error errors

    let private writeAgentsChange existing =
        PlannedChange.WriteFile(AgentsPath, appendRegion existing)

    let private replaceAgentsChange content startIndex endExclusive =
        PlannedChange.WriteFile(AgentsPath, replaceRegion content startIndex endExclusive)

    let private manifestChange () =
        PlannedChange.WriteFile(ManifestPath, manifestContent ())

    let planInit target =
        match readManifest target with
        | Error errors -> Error errors
        | Ok None ->
            match inspectRegion target with
            | RegionInvalid error -> Error [ error ]
            | RegionAbsent existing ->
                Ok [ writeAgentsChange existing; manifestChange () ]
            | RegionPresent(_, _, _, region) when region = canonicalRegion ->
                Ok [ manifestChange () ]
            | RegionPresent _ ->
                Error
                    [ $"{AgentsPath} already contains a non-canonical Percepta managed region without an installation manifest; refusing adoption." ]
        | Ok(Some manifest) ->
            let errors = manifestErrors manifest

            if not errors.IsEmpty then
                Error errors
            else
                match compareInstalled manifest.InstalledVersion with
                | Error error -> Error [ error ]
                | Ok comparison when comparison < 0 ->
                    Error
                        [ $"Percepta lifecycle {manifest.InstalledVersion} is installed; use 'percepta-repo upgrade' rather than init." ]
                | Ok comparison when comparison > 0 ->
                    Error
                        [ $"Repository was installed by newer Percepta lifecycle {manifest.InstalledVersion}; upgrade this CLI." ]
                | Ok _ ->
                    if not (String.Equals(manifest.ManagedRegionSha256, canonicalRegionSha, StringComparison.OrdinalIgnoreCase)) then
                        Error [ $"{ManifestPath} does not describe the canonical region for current Percepta lifecycle {ToolVersion}." ]
                    else
                        match inspectRegion target with
                        | RegionInvalid error -> Error [ error ]
                        | RegionAbsent existing -> Ok [ writeAgentsChange existing ]
                        | RegionPresent(_, _, _, region) when region = canonicalRegion -> Ok []
                        | RegionPresent _ ->
                            Error
                                [ $"{AgentsPath} Percepta region was locally modified; init will not overwrite it." ]

    let planUpgrade target =
        match readManifest target with
        | Error errors -> Error errors
        | Ok None -> Error [ "Percepta repository lifecycle is not installed; run 'percepta-repo init' first." ]
        | Ok(Some manifest) ->
            let errors = manifestErrors manifest

            if not errors.IsEmpty then
                Error errors
            else
                match compareInstalled manifest.InstalledVersion with
                | Error error -> Error [ error ]
                | Ok comparison when comparison > 0 ->
                    Error
                        [ $"Repository was installed by newer Percepta lifecycle {manifest.InstalledVersion}; upgrade this CLI." ]
                | Ok comparison when comparison = 0 ->
                    planInit target
                | Ok _ ->
                    match inspectRegion target with
                    | RegionInvalid error -> Error [ error ]
                    | RegionAbsent existing ->
                        Ok [ writeAgentsChange existing; manifestChange () ]
                    | RegionPresent(content, startIndex, endExclusive, region) ->
                        let actualSha = sha256 region

                        if not (String.Equals(actualSha, manifest.ManagedRegionSha256, StringComparison.OrdinalIgnoreCase)) then
                            Error
                                [ $"{AgentsPath} Percepta region was locally modified after lifecycle {manifest.InstalledVersion} installed it; upgrade will not overwrite it." ]
                        else
                            Ok [ replaceAgentsChange content startIndex endExclusive; manifestChange () ]

    let apply target changes =
        try
            for change in changes do
                match change with
                | PlannedChange.WriteFile(relativePath, content) ->
                    atomicWrite (targetPath target relativePath) content

            Ok()
        with ex ->
            Error [ $"Percepta repository lifecycle write failed: {ex.Message}" ]

    let describeChange =
        function
        | PlannedChange.WriteFile(relativePath, _) -> $"write {relativePath}"

    let manifestPath = ManifestPath
    let agentsPath = AgentsPath
    let managedRegion = canonicalRegion
    let managedRegionSha256 = canonicalRegionSha
