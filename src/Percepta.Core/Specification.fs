namespace Percepta.Core

open System
open System.Text.Json
open System.Text.RegularExpressions

/// Specification preflight (PER-SPEC-PREFLIGHT): finds contradictory and incomplete specifications
/// before implementation starts. Self-contained: it reads state-machine tables, invariants, frozen
/// interface types and the command catalog, and shares nothing with the screen-contract model.
///
/// Every rule is structural and generic (no requirement or transition id is named in code). A bound
/// extracted from prose is never reported above `likely-contradiction`: semantic confidence is not a
/// deterministic acceptance result.
module Specification =
    type Transition =
        { Id: string
          State: string
          Event: string
          Guard: string
          Next: string
          Effects: string list }

    type Machine =
        { Id: string
          Initial: string
          Transitions: Transition list }

    type Invariant =
        { Id: string
          Statement: string
          Machines: string list }

    type TypeDefinition =
        | Record of name: string * fields: (string * string) list
        | Union of name: string * cases: (string * string option) list

    type Operation =
        { Interface: string
          Name: string
          Signature: string }

    type Catalog =
        { Carrier: string option
          Commands: string list }

    type Specification =
        { Machines: Machine list
          Invariants: Invariant list
          Types: TypeDefinition list
          Operations: Operation list
          Catalog: Catalog
          /// Obligations the verification compiler refused (`dokimos.verification-refusals/1`).
          UnsupportedObligations: (string * string) list }

    type FindingKind =
        | ProvenContradiction
        | LikelyContradiction
        | MissingSpecification
        | MissingInterface

    type Finding =
        { Kind: FindingKind
          Rule: string
          Subjects: string list
          Detail: string
          Counterexample: string list
          OwnerDecision: bool
          OwnerAction: string
          Blocking: bool }

    // ------------------------------------------------------------------ text helpers

    let private numberWords =
        [ "one", 1; "two", 2; "three", 3; "four", 4; "five", 5; "six", 6; "seven", 7; "eight", 8; "nine", 9; "ten", 10; "eleven", 11; "twelve", 12 ]
        |> Map.ofList

    let private number (text: string) =
        match Int32.TryParse text with
        | true, n -> Some n
        | _ -> numberWords |> Map.tryFind (text.ToLowerInvariant())

    let private singular (phrase: string) =
        let words = phrase.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
        let last = words[words.Length - 1]
        let last = if last.EndsWith "s" && last.Length > 3 then last.Substring(0, last.Length - 1) else last
        String.Join(" ", Array.append words[.. words.Length - 2] [| last |])

    /// A rate bound stated in an invariant: at most `Max` of `Effect` in `WindowSeconds`, under `Condition`.
    type RateBound =
        { Invariant: string
          Effect: string
          Max: int
          WindowSeconds: int
          Condition: string }

    let private window = Regex(@"\b(?:in|per|within)\s+(\d+)\s+seconds?\b|\bper\s+minute\b", RegexOptions.IgnoreCase)

    let rateBounds (invariant: Invariant) : RateBound list =
        invariant.Statement.Split(';')
        |> Array.toList
        |> List.collect (fun clause ->
            let w = window.Match clause
            let at = clause.IndexOf("at most", StringComparison.OrdinalIgnoreCase)
            if not w.Success || at < 0 || w.Index < at then []
            else
                let seconds = if w.Groups[1].Success then int w.Groups[1].Value else 60
                let condition = clause.Substring(0, at).Trim().Trim(',')
                clause.Substring(at + "at most".Length, w.Index - at - "at most".Length).Split([| " and "; "," |], StringSplitOptions.RemoveEmptyEntries)
                |> Array.toList
                |> List.choose (fun part ->
                    let m = Regex.Match(part.Trim(), @"^(\w+)\s+([a-z][a-z\s-]*[a-z])$", RegexOptions.IgnoreCase)
                    if not m.Success then None
                    else
                        number m.Groups[1].Value
                        |> Option.map (fun max ->
                            { Invariant = invariant.Id; Effect = singular m.Groups[2].Value; Max = max; WindowSeconds = seconds; Condition = condition })))

    /// A guard atom that limits how often a transition can repeat: a counter still below its limit,
    /// or any reference to time. A saturated predicate ("spent", "reached") or `true` limits nothing.
    let private bounding = Regex(@"fewer than|less than|at most|not reached|limit not|remaining|after at least|\b\d+\s*s\b|seconds?|minute|clock|timer|window|budget", RegexOptions.IgnoreCase)

    /// The effect is named as whole words, not negated ("send no mutation"), and the effect list is
    /// not stated as "none" (an effect that only confirms or records emits nothing).
    let private emits (effect: string) (transition: Transition) =
        let word = Regex($@"\b{Regex.Escape effect}s?\b", RegexOptions.IgnoreCase)
        transition.Effects
        |> List.exists (fun e ->
            let m = word.Match e
            m.Success
            && not (Regex.IsMatch(e, @"^\s*none\b", RegexOptions.IgnoreCase))
            && not (Regex.IsMatch(e.Substring(0, m.Index), @"\b(no|never|without)\s+(\w+\s+){0,2}$", RegexOptions.IgnoreCase)))

    /// Shortest legal path from the machine's initial state to `target` (transition list).
    let pathTo (machine: Machine) (target: string) =
        let rec bfs (frontier: (string * Transition list) list) (seen: Set<string>) =
            match frontier with
            | [] -> None
            | (state, path) :: _ when state = target -> Some(List.rev path)
            | (state, path) :: rest ->
                let next =
                    machine.Transitions
                    |> List.filter (fun t -> t.State = state && not (seen.Contains t.Next))
                    |> List.distinctBy _.Next
                    |> List.map (fun t -> t.Next, t :: path)
                bfs (rest @ next) (Set.union seen (next |> List.map fst |> Set.ofList))
        bfs [ machine.Initial, [] ] (Set.singleton machine.Initial)

    let private step (t: Transition) = $"{t.Id}: {t.State} --{t.Event} [{t.Guard}]--> {t.Next}"

    // ------------------------------------------------------------------ rule 1: rate bounds vs loops

    /// An invariant bounds an effect per time window, but a machine it governs has a reachable
    /// self-loop that emits the effect under a guard that limits nothing and references no clock:
    /// repeating the loop exceeds the bound in an arbitrarily short time.
    let rateBoundContradictions (spec: Specification) : Finding list =
        spec.Invariants
        |> List.collect (fun invariant ->
            rateBounds invariant
            |> List.collect (fun bound ->
                spec.Machines
                |> List.filter (fun m -> List.contains m.Id invariant.Machines)
                |> List.collect (fun machine ->
                    machine.Transitions
                    |> List.filter (fun t -> t.Next = t.State && emits bound.Effect t && not (bounding.IsMatch t.Guard))
                    |> List.choose (fun loop ->
                        pathTo machine loop.State
                        |> Option.map (fun path ->
                            { Kind = LikelyContradiction
                              Rule = "rate-bound-vs-unbounded-loop"
                              Subjects = [ invariant.Id; machine.Id; loop.Id ]
                              Detail =
                                $"{invariant.Id} allows at most {bound.Max} {bound.Effect}(s) in {bound.WindowSeconds} s ({bound.Condition}), but {machine.Id} {loop.Id} ({loop.State} x {loop.Event}, guard '{loop.Guard}') emits a {bound.Effect} on every repetition, and no guard on it limits repetition or references a clock."
                              Counterexample =
                                (path |> List.map step)
                                @ (if loop.Guard = "true" then []
                                   else [ $"(steps that make '{loop.Guard}' hold come first; they add {bound.Effect}s, never remove one)" ])
                                @ [ for n in 1 .. bound.Max + 1 -> $"{step loop}  [{bound.Effect} #{n}]" ]
                                @ [ $"{bound.Max + 1} {bound.Effect}s with no time between them: exceeds {bound.Max} in {bound.WindowSeconds} s" ]
                              OwnerDecision = true
                              OwnerAction = $"decide which governs: bound {loop.Id} (a clock, a count or a rate in its guard) or amend {invariant.Id}"
                              Blocking = true })))))
        |> List.distinctBy (fun f -> f.Subjects)

    // ------------------------------------------------------------------ rule 2: guard coverage

    let private belowLimit = Regex(@"fewer than|less than|not reached|limit not|remaining|at most", RegexOptions.IgnoreCase)
    let private atLimit = Regex(@"\balready (made|sent|used|done)\b|(?<!not )\blimit reached|\bspent\b|exhausted|no \w+ left|limit hit", RegexOptions.IgnoreCase)

    /// A (state, event) pair legal only on one side of a counter (below the limit, or at it) has no
    /// transition for the other side: the input fails closed there, which no table states as intended.
    let guardCoverageGaps (spec: Specification) : Finding list =
        spec.Machines
        |> List.collect (fun machine ->
            machine.Transitions
            |> List.groupBy (fun t -> t.State, t.Event)
            |> List.choose (fun ((state, event), transitions) ->
                let guards = transitions |> List.map _.Guard
                let below = guards |> List.exists belowLimit.IsMatch
                let at = guards |> List.exists atLimit.IsMatch
                if below = at then None
                else
                    let missing = if below then "the counter at its limit" else "the counter still below its limit"
                    Some
                        { Kind = MissingSpecification
                          Rule = "counter-guard-without-complement"
                          Subjects = machine.Id :: (transitions |> List.map _.Id)
                          Detail =
                            $"""{machine.Id} ({state}, {event}) is specified only when '{String.Join("' or '", guards)}'; no transition covers {missing}, so that input fails closed without a stated outcome."""
                          Counterexample =
                            (pathTo machine state |> Option.defaultValue [] |> List.map step)
                            @ [ $"{state} x {event} with {missing}: no legal transition" ]
                          OwnerDecision = true
                          OwnerAction = $"state the transition for ({state}, {event}) with {missing}, or record that failing closed is intended"
                          Blocking = false }))

    // ------------------------------------------------------------------ rule 3: carrier sufficiency

    let private primitives =
        set [ "Guid"; "int"; "int32"; "int64"; "uint64"; "bool"; "DateTime"; "DateTimeOffset"; "decimal"; "float"; "double"; "unit"; "TimeSpan" ]

    let private discriminatingLeaves = set [ "string"; "JsonElement"; "JsonNode"; "obj"; "byte[]" ]

    let private typeNames (expression: string) =
        Regex.Matches(expression, @"[A-Za-z_][\w.]*(\[\])?")
        |> Seq.map _.Value
        |> Seq.filter (fun n -> not (List.contains n [ "option"; "list"; "array"; "seq"; "Map"; "Set"; "Result"; "of" ]))
        |> List.ofSeq

    type Capacity =
        | Discriminates of via: string
        | Opaque of typeName: string
        | Indistinct

    /// Can a value of `name` say which of several kinds it is? Through a string, a multi-case union,
    /// a payload type, or a type this specification does not define (opaque: unknown).
    let rec capacity (types: Map<string, TypeDefinition>) (seen: Set<string>) (name: string) : Capacity =
        if primitives.Contains name || seen.Contains name then Indistinct
        elif discriminatingLeaves.Contains name then Discriminates name
        else
            match types |> Map.tryFind name with
            | None -> Opaque name
            | Some(Union(_, cases)) when cases.Length > 1 -> Discriminates $"{name} ({cases.Length} cases)"
            | Some(Union(_, cases)) -> cases |> List.choose snd |> List.collect typeNames |> combine types (seen.Add name)
            | Some(Record(_, fields)) -> fields |> List.collect (snd >> typeNames) |> combine types (seen.Add name)

    and private combine types seen names =
        let results = names |> List.map (capacity types seen)
        results
        |> List.tryFind (function Discriminates _ -> true | _ -> false)
        |> Option.orElse (results |> List.tryFind (function Opaque _ -> true | _ -> false))
        |> Option.defaultValue Indistinct

    let private render (types: Map<string, TypeDefinition>) name =
        match types |> Map.tryFind name with
        | Some(Record(_, fields)) -> $"""{name} = {{ {String.Join("; ", fields |> List.map (fun (f, t) -> $"{f}: {t}"))} }}"""
        | Some(Union(_, cases)) -> $"""{name} = {String.Join(" | ", cases |> List.map (fun (c, p) -> match p with Some t -> $"{c} of {t}" | None -> c))}"""
        | None -> name

    /// The catalog says every command is carried inside one type. A consumer that must tell the
    /// commands apart receives that type, so it must be able to represent which command it is.
    let carrierSufficiency (spec: Specification) : Finding list =
        let types =
            spec.Types |> List.map (fun t -> (match t with Record(n, _) | Union(n, _) -> n), t) |> Map.ofList
        match spec.Catalog.Carrier with
        | Some carrier when spec.Catalog.Commands.Length > 1 && types.ContainsKey carrier ->
            match capacity types Set.empty carrier with
            | Discriminates _ | Opaque _ -> []
            | Indistinct ->
                let consumers =
                    spec.Operations
                    |> List.filter (fun o ->
                        let inputs = o.Signature.Substring(0, max 0 (o.Signature.LastIndexOf "->"))
                        Regex.IsMatch(inputs, $@"\b{carrier}\b"))
                let carriers =
                    spec.Types
                    |> List.choose (fun t ->
                        match t with
                        | Record(n, fields) when fields |> List.exists (fun (_, ty) -> Regex.IsMatch(ty, $@"\b{carrier}\b")) ->
                            Some(n, fields |> List.filter (fun (_, ty) -> Regex.IsMatch(ty, $@"\b{carrier}\b")) |> List.map fst)
                        | Union(n, cases) when cases |> List.exists (fun (_, p) -> p |> Option.exists (fun ty -> Regex.IsMatch(ty, $@"\b{carrier}\b"))) ->
                            Some(n, cases |> List.filter (fun (_, p) -> p |> Option.exists (fun ty -> Regex.IsMatch(ty, $@"\b{carrier}\b"))) |> List.map fst)
                        | _ -> None)
                [ { Kind = MissingInterface
                    Rule = "carrier-cannot-discriminate"
                    Subjects = carrier :: (consumers |> List.map (fun o -> $"{o.Interface}.{o.Name}"))
                    Detail =
                      $"""The catalog carries all {spec.Catalog.Commands.Length} commands ({String.Join(", ", spec.Catalog.Commands |> List.truncate 4)}, ...) inside {carrier}, but `{render types carrier}` holds only identities and versions: no field can say which command it is or carry its body. {String.Join(", ", consumers |> List.map (fun o -> $"{o.Interface}.{o.Name}"))} must distinguish them from that value alone."""
                    Counterexample =
                      [ $"two different commands (e.g. {spec.Catalog.Commands.Head} and {spec.Catalog.Commands.Tail.Head}) with the same identities and version are the same {carrier} value" ]
                      @ (carriers |> List.map (fun (n, members) -> $"""the body is therefore also lost wherever {n} carries {carrier} ({String.Join(", ", members)})"""))
                    OwnerDecision = true
                    OwnerAction = $"add the command (a union of the catalog commands, with payloads) to {carrier}, or name the out-of-band source consumers must read it from"
                    Blocking = true } ]
        | _ -> []

    let analyze (spec: Specification) =
        rateBoundContradictions spec @ guardCoverageGaps spec @ carrierSufficiency spec

    // ------------------------------------------------------------------ readers (Indy Init shapes)

    let private prop (name: string) (e: JsonElement) =
        match e.ValueKind with
        | JsonValueKind.Object ->
            match e.TryGetProperty name with
            | true, v -> Some v
            | _ -> None
        | _ -> None

    let private str name e =
        prop name e |> Option.filter (fun v -> v.ValueKind = JsonValueKind.String) |> Option.map (fun v -> v.GetString() |> Option.ofObj |> Option.defaultValue "") |> Option.defaultValue ""

    let private items name e =
        prop name e |> Option.filter (fun v -> v.ValueKind = JsonValueKind.Array) |> Option.map (fun v -> v.EnumerateArray() |> List.ofSeq) |> Option.defaultValue []

    let private strs name e =
        items name e |> List.filter (fun v -> v.ValueKind = JsonValueKind.String) |> List.map (fun v -> v.GetString() |> Option.ofObj |> Option.defaultValue "")

    /// `indy-init.v41-state-machine-contracts/1` (or the v4 `state-machines.json` pair layout).
    let readMachines (json: string) : Machine list =
        use doc = JsonDocument.Parse json
        items "machines" doc.RootElement
        |> List.map (fun m ->
            { Id = str "id" m
              Initial = str "initial" m
              Transitions =
                items "pairs" m
                |> List.filter (fun p -> str "outcome" p = "legal")
                |> List.collect (fun p ->
                    items "transitions" p
                    |> List.map (fun t ->
                        { Id = str "id" t; State = str "state" p; Event = str "event" p; Guard = str "guard" t
                          Next = str "next" t; Effects = strs "effects" t })) })

    let readInvariants (json: string) : Invariant list =
        use doc = JsonDocument.Parse json
        items "invariants" doc.RootElement
        |> List.map (fun i -> { Id = str "id" i; Statement = str "statement" i; Machines = strs "machines" i })

    /// F# `type` definitions: records `{ A: T; ... }`, unions `| A of T` and single-case `X of T`.
    let parseTypes (fsharp: string) : TypeDefinition list =
        let lines = fsharp.Replace("\r", "").Split('\n')
        let starts =
            lines
            |> Array.mapi (fun i l -> i, Regex.Match(l, @"^\s*(?:type|and)\s+(?:private\s+|internal\s+)?([A-Z]\w*)(?:<[^>]*>)?\s*=\s*(.*)$"))
            |> Array.filter (fun (_, m) -> m.Success)
        starts
        |> Array.mapi (fun k (i, m) ->
            let stop = if k + 1 < starts.Length then fst starts[k + 1] else lines.Length
            let body =
                (m.Groups[2].Value :: (lines[i + 1 .. stop - 1] |> Array.toList))
                |> List.map (fun l -> let c = l.IndexOf "//" in if c >= 0 then l.Substring(0, c) else l)
                |> String.concat " "
            let name = m.Groups[1].Value
            if body.Contains "{" then
                let inner = body.Substring(body.IndexOf '{' + 1, max 0 (body.LastIndexOf '}' - body.IndexOf '{' - 1))
                Regex.Matches(inner, @"([A-Z]\w*)\s*:\s*([^;:]+?)(?=\s+[A-Z]\w*\s*:|;|$)")
                |> Seq.map (fun f -> f.Groups[1].Value, f.Groups[2].Value.Trim())
                |> List.ofSeq
                |> fun fields -> Some(Record(name, fields))
            else
                let cases =
                    body.Split('|', StringSplitOptions.RemoveEmptyEntries)
                    |> Array.map (fun c -> c.Trim())
                    |> Array.filter (fun c -> Regex.IsMatch(c, @"^[A-Z]\w*"))
                    |> Array.map (fun c ->
                        let m = Regex.Match(c, @"^([A-Z]\w*)(?:\s+of\s+(.+))?$")
                        m.Groups[1].Value, (if m.Groups[2].Success then Some(m.Groups[2].Value.Trim()) else None))
                    |> List.ofArray
                if cases.IsEmpty then None else Some(Union(name, cases)))
        |> Array.choose id
        |> List.ofArray

    /// `indy-init.v41-interface-specifications`: the domain stubs and each interface's F# and operations.
    let readInterfaces (json: string) : TypeDefinition list * Operation list =
        use doc = JsonDocument.Parse json
        let root = doc.RootElement
        let stubs = prop "domainStubs" root |> Option.map (str "fsharp") |> Option.defaultValue ""
        let interfaces = items "interfaces" root
        let types = parseTypes stubs @ (interfaces |> List.collect (str "fsharp" >> parseTypes))
        let operations =
            interfaces
            |> List.collect (fun i -> items "operations" i |> List.map (fun o -> { Interface = str "id" i; Name = str "name" o; Signature = str "signature" o }))
        types, operations

    /// Command names are the first column of every markdown table headed `Command`; the carrier is
    /// the type named by "carried inside X".
    let readCatalog (markdown: string) : Catalog =
        let lines = markdown.Replace("\r", "").Split('\n')
        let commands =
            lines
            |> Array.fold
                (fun (inTable, acc) line ->
                    let cells = line.Trim().Trim('|').Split('|') |> Array.map (fun c -> c.Trim().Trim('`'))
                    if line.TrimStart().StartsWith "|" && cells.Length > 1 && cells[0] = "Command" then true, acc
                    elif line.TrimStart().StartsWith "|" && inTable then
                        (if Regex.IsMatch(cells[0], @"^[A-Z][A-Za-z]+$") then true, cells[0] :: acc else true, acc)
                    else false, acc)
                (false, [])
            |> snd
            |> List.rev
            |> List.distinct
        let carrier = Regex.Match(markdown, @"carried inside\s+`?([A-Z]\w*)")
        { Carrier = (if carrier.Success then Some carrier.Groups[1].Value else None); Commands = commands }

    let readRefusals (json: string) =
        use doc = JsonDocument.Parse json
        items "refusals" doc.RootElement |> List.map (fun r -> str "subject" r, str "reason" r)

    // ------------------------------------------------------------------ report

    [<Literal>]
    let ReportSchema = "percepta.specification-preflight/1"

    let kindText =
        function
        | ProvenContradiction -> "proven-contradiction"
        | LikelyContradiction -> "likely-contradiction"
        | MissingSpecification -> "missing-specification"
        | MissingInterface -> "missing-interface"

    let toJson (spec: Specification) (findings: Finding list) =
        let finding (f: Finding) =
            {| kind = kindText f.Kind; rule = f.Rule; subjects = f.Subjects; detail = f.Detail; counterexample = f.Counterexample
               requiresOwnerDecision = f.OwnerDecision; ownerAction = f.OwnerAction; blocking = f.Blocking |}
        let isContradiction f = f.Kind = ProvenContradiction || f.Kind = LikelyContradiction
        JsonSerializer.Serialize(
            {| schema = ReportSchema
               blocked = findings |> List.exists _.Blocking
               analysed =
                {| machines = spec.Machines.Length; transitions = spec.Machines |> List.sumBy _.Transitions.Length
                   invariants = spec.Invariants.Length; types = spec.Types.Length; operations = spec.Operations.Length
                   catalogCommands = spec.Catalog.Commands.Length |}
               blockingContradictions = findings |> List.filter (fun f -> isContradiction f && f.Blocking) |> List.map finding
               missingInterfaces = findings |> List.filter (fun f -> f.Kind = MissingInterface) |> List.map finding
               missingDecisions = findings |> List.filter (fun f -> f.Kind = MissingSpecification) |> List.map finding
               unsupportedVerificationObligations = spec.UnsupportedObligations |> List.map (fun (s, r) -> {| subject = s; reason = r |})
               suggestedOwnerActions = findings |> List.filter _.OwnerDecision |> List.map _.OwnerAction |> List.distinct
               warnings = findings |> List.filter (fun f -> not f.Blocking && f.Kind <> MissingSpecification) |> List.map finding |},
            JsonSerializerOptions(WriteIndented = true))
