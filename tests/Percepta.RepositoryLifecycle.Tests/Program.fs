open System
open System.IO
open System.Security.Cryptography
open System.Text
open Percepta.RepositoryLifecycle

let mutable failures = 0

let check name condition =
    if condition then
        printfn "PASS %s" name
    else
        failures <- failures + 1
        eprintfn "FAIL %s" name

let withTarget action =
    let root = Path.Combine(Path.GetTempPath(), $"percepta-repo-tests-{Guid.NewGuid():N}")
    Directory.CreateDirectory root |> ignore

    try
        action root
    finally
        if Directory.Exists root then
            Directory.Delete(root, true)

let hashFile path =
    File.ReadAllBytes path
    |> SHA256.HashData
    |> Convert.ToHexString

let applyOrFail name target plan =
    match plan with
    | Error errors ->
        check ($"{name} plan") false
        errors |> List.iter (eprintfn "%s")
    | Ok changes ->
        match Lifecycle.apply target changes with
        | Error errors ->
            check ($"{name} apply") false
            errors |> List.iter (eprintfn "%s")
        | Ok() ->
            check ($"{name} apply") true

withTarget (fun target ->
    applyOrFail "fresh init" target (Lifecycle.planInit target)

    check
        "fresh init verifies"
        (Lifecycle.verify target = Ok())

    check
        "fresh init writes lifecycle manifest"
        (File.Exists(Path.Combine(target, Lifecycle.manifestPath)))

    check
        "fresh init writes AGENTS region"
        (File.ReadAllText(Path.Combine(target, Lifecycle.agentsPath)).Contains(Lifecycle.managedRegion)))

withTarget (fun target ->
    applyOrFail "idempotent first init" target (Lifecycle.planInit target)

    let manifestPath = Path.Combine(target, Lifecycle.manifestPath)
    let agentsPath = Path.Combine(target, Lifecycle.agentsPath)
    let beforeManifest = hashFile manifestPath
    let beforeAgents = hashFile agentsPath

    match Lifecycle.planInit target with
    | Error _ ->
        check "second init plans cleanly" false
    | Ok changes ->
        check "second init plans zero changes" changes.IsEmpty
        applyOrFail "idempotent second init" target (Ok changes)

    check "second init manifest byte stable" (beforeManifest = hashFile manifestPath)
    check "second init AGENTS byte stable" (beforeAgents = hashFile agentsPath))

withTarget (fun target ->
    let agentsPath = Path.Combine(target, Lifecycle.agentsPath)
    let original = "# Existing repository instructions" + Environment.NewLine + Environment.NewLine
    File.WriteAllText(agentsPath, original)

    applyOrFail "preserve shared AGENTS" target (Lifecycle.planInit target)

    let actual = File.ReadAllText agentsPath
    check "existing AGENTS prefix preserved" (actual.StartsWith(original, StringComparison.Ordinal))
    check "Percepta region appended" (actual.Contains(Lifecycle.managedRegion)))

withTarget (fun target ->
    applyOrFail "edit guard setup" target (Lifecycle.planInit target)
    let agentsPath = Path.Combine(target, Lifecycle.agentsPath)
    let text = File.ReadAllText agentsPath
    File.WriteAllText(agentsPath, text.Replace("Managed by `percepta-repo`.", "Locally edited Percepta region."))

    match Lifecycle.planInit target with
    | Error errors ->
        check
            "local managed-region edit blocks init"
            (errors |> List.exists (fun error -> error.Contains("locally modified")))
    | Ok _ ->
        check "local managed-region edit blocks init" false)

withTarget (fun target ->
    applyOrFail "deletion repair setup" target (Lifecycle.planInit target)
    let agentsPath = Path.Combine(target, Lifecycle.agentsPath)
    File.Delete agentsPath

    match Lifecycle.planInit target with
    | Error _ ->
        check "missing managed region is repairable" false
    | Ok changes ->
        check "missing managed region plans one write" (changes.Length = 1)
        applyOrFail "missing region repair" target (Ok changes)
        check "repaired installation verifies" (Lifecycle.verify target = Ok()))

withTarget (fun target ->
    let agentsPath = Path.Combine(target, Lifecycle.agentsPath)
    File.WriteAllText(
        agentsPath,
        "<!-- echelon:percepta:start -->" + Environment.NewLine + "orphan edit" + Environment.NewLine + "<!-- echelon:percepta:end -->"
    )

    match Lifecycle.planInit target with
    | Error errors ->
        check
            "orphan noncanonical region blocks adoption"
            (errors |> List.exists (fun error -> error.Contains("refusing adoption")))
    | Ok _ ->
        check "orphan noncanonical region blocks adoption" false)

withTarget (fun target ->
    let agentsPath = Path.Combine(target, Lifecycle.agentsPath)
    let manifestPath = Path.Combine(target, Lifecycle.manifestPath)
    Directory.CreateDirectory(Path.GetDirectoryName manifestPath) |> ignore
    File.WriteAllText(agentsPath, Lifecycle.managedRegion + Environment.NewLine)

    let legacyManifest =
        $"""{{"schemaVersion":1,"component":"percepta","installedVersion":"0.0.9","configurationVersion":1,"managedRegionSha256":"{Lifecycle.managedRegionSha256}"}}"""

    File.WriteAllText(manifestPath, legacyManifest)

    match Lifecycle.inspect target with
    | { State = UpgradeRequired "0.0.9" } ->
        check "legacy installation requires upgrade" true
    | _ ->
        check "legacy installation requires upgrade" false

    match Lifecycle.planUpgrade target with
    | Error errors ->
        check "legacy installation upgrade plans" false
        errors |> List.iter (eprintfn "%s")
    | Ok changes ->
        check "legacy upgrade includes manifest reconciliation" (changes.Length >= 1)
        applyOrFail "legacy upgrade" target (Ok changes)
        check "upgraded installation verifies" (Lifecycle.verify target = Ok()))

withTarget (fun target ->
    applyOrFail "upgrade edit setup" target (Lifecycle.planInit target)
    let agentsPath = Path.Combine(target, Lifecycle.agentsPath)
    let manifestPath = Path.Combine(target, Lifecycle.manifestPath)
    let current = File.ReadAllText agentsPath
    File.WriteAllText(agentsPath, current.Replace("semantic UI contracts", "edited semantic UI contracts"))

    let legacyManifest =
        $"""{{"schemaVersion":1,"component":"percepta","installedVersion":"0.0.9","configurationVersion":1,"managedRegionSha256":"{Lifecycle.managedRegionSha256}"}}"""

    File.WriteAllText(manifestPath, legacyManifest)

    match Lifecycle.planUpgrade target with
    | Error errors ->
        check
            "upgrade refuses locally edited prior region"
            (errors |> List.exists (fun error -> error.Contains("locally modified")))
    | Ok _ ->
        check "upgrade refuses locally edited prior region" false)

let exitCode =
    if failures = 0 then
        printfn "All Percepta repository lifecycle tests passed."
        0
    else
        eprintfn "%d Percepta repository lifecycle test(s) failed." failures
        1

[<EntryPoint>]
let main _ = exitCode
