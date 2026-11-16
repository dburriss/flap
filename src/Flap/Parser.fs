namespace Flap

module Parser =

    // Minimal result builder so the parser reads linearly and stays Fable-friendly.
    type ResultBuilder() =
        member _.Bind(m, f) = Result.bind f m
        member _.Return x = Ok x
        member _.ReturnFrom m = m
        member _.Zero() = Ok()
        member _.Delay f = f
        member _.Run f = f ()
        member _.Combine(a, b) = Result.bind (fun () -> b ()) a

    let result = ResultBuilder()

    type State =
        { Vals: Map<string, string list>
          Src: Map<string, ValueSource>
          Sub: (string * Matches) option
          ForcePositional: bool }

    let private editDistance (a: string) (b: string) : int =
        let n = b.Length
        let mutable prev = Array.init (n + 1) id
        for i in 1 .. a.Length do
            let cur = Array.zeroCreate (n + 1)
            cur.[0] <- i
            for j in 1..n do
                let cost = if a.[i - 1] = b.[j - 1] then 0 else 1
                cur.[j] <- min (min (prev.[j] + 1) (cur.[j - 1] + 1)) (prev.[j - 1] + cost)
            prev <- cur
        prev.[n]

    let suggest (input: string) (candidates: string list) : string option =
        candidates
        |> List.map (fun c -> c, editDistance input c)
        |> List.filter (fun (c, d) -> d <= 2 || d * 3 <= c.Length)
        |> List.sortBy snd
        |> List.tryHead
        |> Option.map fst

    let private argDisplayName (a: Arg) = Help.argDisplay a

    let private isNegativeNumber (s: string) =
        s.Length > 1 && s.[0] = '-' && System.Char.IsDigit s.[1]

    let private looksLikeFlag (s: string) = s.Length > 1 && s.[0] = '-' && not (isNegativeNumber s)

    let rec parseCommand
        (env: string -> string option)
        (path: string)
        (cmd: Command)
        (globals: Arg list)
        (argv: string list)
        : Result<Matches, ParseError> =

        let inherited = globals |> List.filter (fun g -> not (cmd.Args |> List.exists (fun a -> a.Id = g.Id)))
        let allArgs = cmd.Args @ inherited
        let builtin = Help.builtins cmd
        let usage = Help.usage path cmd
        let childGlobals = inherited @ (cmd.Args |> List.filter (fun a -> a.Global))
        let positionals =
            allArgs |> List.filter (fun a -> a.Index.IsSome) |> List.sortBy (fun a -> a.Index.Value)

        let findLong (name: string) =
            (allArgs @ builtin)
            |> List.tryFind (fun a -> a.Long = Some name || List.contains name a.Aliases)
        let findShort (c: char) = (allArgs @ builtin) |> List.tryFind (fun a -> a.Short = Some c)
        let findSub (name: string) =
            cmd.Subcommands |> List.tryFind (fun s -> s.Name = name || List.contains name s.CommandAliases)

        let present (st: State) (id: string) =
            match Map.tryFind id st.Src with
            | Some DefaultValue
            | None -> false
            | _ -> true

        // Validate and store one raw value (after delimiter splitting).
        let addValue (st: State) (a: Arg) (src: ValueSource) (raw: string) : Result<State, ParseError> =
            let pieces =
                match a.Delimiter with
                | Some d -> raw.Split d |> List.ofArray
                | None -> [ raw ]
            let rec go (st: State) pieces =
                match pieces with
                | [] -> Ok st
                | v :: rest ->
                    let bad reason = Error(InvalidValue(argDisplayName a, v, reason, usage))
                    if not a.PossibleValues.IsEmpty && not (List.contains v a.PossibleValues) then
                        bad ("expected one of: " + String.concat ", " a.PossibleValues)
                    else
                        match a.Validator |> Option.map (fun f -> f v) with
                        | Some(Error e) -> bad e
                        | _ ->
                            let existing = Map.tryFind a.Id st.Vals |> Option.defaultValue []
                            let isNew = Map.tryFind a.Id st.Src <> Some src
                            let updated =
                                match a.Action with
                                | Append -> (if isNew then [] else existing) @ [ v ]
                                | _ -> if a.Index.IsSome && a.MaxValues > 1 then existing @ [ v ] else [ v ]
                            go
                                { st with
                                    Vals = Map.add a.Id updated st.Vals
                                    Src = Map.add a.Id src st.Src }
                                rest
            go st pieces

        let applyFlag (st: State) (a: Arg) : State =
            let set v =
                { st with
                    Vals = Map.add a.Id [ v ] st.Vals
                    Src = Map.add a.Id CommandLine st.Src }
            match a.Action with
            | SetTrue -> set "true"
            | SetFalse -> set "false"
            | Count ->
                let n =
                    match Map.tryFind a.Id st.Vals with
                    | Some [ s ] -> int s
                    | _ -> 0
                set (string (n + 1))
            | _ -> st

        // Consume values for an option. `first` is an inline value (from `=` or short cluster).
        let consumeValues (st: State) (a: Arg) (first: string option) (rest: string list) =
            let name = argDisplayName a
            let rec more (st: State) (taken: int) (rest: string list) =
                if taken >= a.MaxValues then
                    Ok(st, rest)
                else
                    match rest with
                    | v :: tl when not (looksLikeFlag v) ->
                        match addValue st a CommandLine v with
                        | Ok st -> more st (taken + 1) tl
                        | Error e -> Error e
                    | _ ->
                        if taken < a.MinValues then
                            if taken = 0 then Error(MissingValue(name, usage)) else Error(TooFewValues(name, usage))
                        else
                            Ok(st, rest)
            // mark present even if no value (optional value)
            let st = { st with Src = Map.add a.Id CommandLine st.Src }
            match first with
            | Some v ->
                match addValue st a CommandLine v with
                | Ok st -> more st 1 rest
                | Error e -> Error e
            | None ->
                match rest with
                | _ -> more st 0 rest

        let helpFor long =
            DisplayHelp(Help.render path cmd globals long)

        let rec loop (st: State) (tokens: string list) : Result<State, ParseError> =
            match tokens with
            | [] -> Ok st
            | "--" :: rest when not st.ForcePositional -> loop { st with ForcePositional = true } rest
            | tok :: rest when not st.ForcePositional && tok.StartsWith "--" ->
                let body = tok.Substring 2
                let name, inline' =
                    match body.IndexOf '=' with
                    | -1 -> body, None
                    | i -> body.Substring(0, i), Some(body.Substring(i + 1))
                match findLong name with
                | None ->
                    let candidates = (allArgs @ builtin) |> List.choose (fun a -> a.Long)
                    Error(UnknownArgument("--" + name, suggest name candidates |> Option.map (fun s -> "--" + s), usage))
                | Some a -> handleOption st a inline' rest (name = "help")
            | tok :: rest when not st.ForcePositional && looksLikeFlag tok ->
                handleShort st (tok.Substring 1) rest
            | tok :: rest -> handlePositional st tok rest

        and handleOption st (a: Arg) inline' rest isLongHelp =
            match a.Action with
            | Help -> Error(helpFor isLongHelp)
            | Version -> Error(DisplayVersion(Help.versionText path cmd))
            | SetTrue
            | SetFalse
            | Count ->
                match inline' with
                | Some _ -> Error(TooManyValues(argDisplayName a, defaultArg inline' "", usage))
                | None -> loop (applyFlag st a) rest
            | _ ->
                match consumeValues st a inline' rest with
                | Ok(st, rest) -> loop st rest
                | Error e -> Error e

        and handleShort st (cluster: string) rest =
            match cluster with
            | "" -> loop st rest
            | _ ->
                let c = cluster.[0]
                match findShort c with
                | None -> Error(UnknownArgument("-" + string c, None, usage))
                | Some a ->
                    let tail = cluster.Substring 1
                    match a.Action with
                    | Help -> Error(helpFor false)
                    | Version -> Error(DisplayVersion(Help.versionText path cmd))
                    | SetTrue
                    | SetFalse
                    | Count -> handleShort (applyFlag st a) tail rest
                    | _ ->
                        let inline' =
                            if tail = "" then None
                            elif tail.[0] = '=' then Some(tail.Substring 1)
                            else Some tail
                        match consumeValues st a inline' rest with
                        | Ok(st, rest) -> loop st rest
                        | Error e -> Error e

        and handlePositional st tok rest =
            match (if st.ForcePositional then None else findSub tok) with
            | Some sub ->
                match parseCommand env (path + " " + sub.Name) sub childGlobals rest with
                | Ok m -> Ok { st with Sub = Some(sub.Name, m) }
                | Error e -> Error e
            | None when tok = "help" && not st.ForcePositional && not cmd.Subcommands.IsEmpty && (positionals |> List.forall (fun p -> Map.containsKey p.Id st.Vals)) ->
                match rest with
                | name :: _ ->
                    match findSub name with
                    | Some sub -> Error(DisplayHelp(Help.render (path + " " + sub.Name) sub childGlobals true))
                    | None -> Error(UnknownSubcommand(name, suggest name (cmd.Subcommands |> List.map (fun s -> s.Name)), usage))
                | [] -> Error(helpFor true)
            | None ->
                let slot =
                    positionals
                    |> List.tryFind (fun p ->
                        let n = Map.tryFind p.Id st.Vals |> Option.defaultValue [] |> List.length
                        n < p.MaxValues)
                match slot with
                | Some p ->
                    match addValue st p CommandLine tok with
                    | Ok st -> loop st rest
                    | Error e -> Error e
                | None ->
                    if cmd.AllowExternalSubcommands then
                        let m = { Matches.empty tok with Values = Map.ofList [ "", rest ] }
                        Ok { st with Sub = Some(tok, m) }
                    elif not cmd.Subcommands.IsEmpty && positionals.IsEmpty then
                        Error(UnknownSubcommand(tok, suggest tok (cmd.Subcommands |> List.map (fun s -> s.Name)), usage))
                    else
                        Error(UnknownArgument(tok, None, usage))

        let expand (st: State) (ids: string list) : string list =
            let groups = ArgGroup.all cmd
            ids
            |> List.collect (fun id ->
                match groups |> List.tryFind (fun g -> g.GroupId = id) with
                | Some g -> g.Members
                | None -> [ id ])

        let validate (st: State) : Result<State, ParseError> =
            let isPresent = present st
            let nameOf id =
                match allArgs |> List.tryFind (fun a -> a.Id = id) with
                | Some a -> argDisplayName a
                | None -> id
            let missing =
                allArgs
                |> List.filter (fun a ->
                    (a.Required || not a.RequiredUnless.IsEmpty)
                    && not (isPresent a.Id)
                    && not (Map.containsKey a.Id st.Vals)
                    && not (expand st a.RequiredUnless |> List.exists isPresent))
                |> List.map argDisplayName
            let groups = ArgGroup.all cmd
            let groupErr =
                groups
                |> List.tryPick (fun g ->
                    let n = g.Members |> List.filter isPresent |> List.length
                    if g.GroupRequired && n = 0 then Some(GroupRequired(g.GroupId, usage))
                    elif not g.Multiple && n > 1 then Some(GroupConflict(g.GroupId, usage))
                    else None)
            let conflict =
                allArgs
                |> List.filter (fun a -> isPresent a.Id)
                |> List.tryPick (fun a ->
                    expand st a.ConflictsWith
                    |> List.tryFind (fun o -> o <> a.Id && isPresent o)
                    |> Option.map (fun o -> ArgumentConflict(argDisplayName a, nameOf o, usage)))
            let requiresErr =
                allArgs
                |> List.filter (fun a -> isPresent a.Id)
                |> List.tryPick (fun a ->
                    a.Requires
                    |> List.tryPick (fun r ->
                        if expand st [ r ] |> List.exists isPresent then None
                        else Some(MissingRequiredBy(argDisplayName a, nameOf r, usage))))
            if not missing.IsEmpty then Error(MissingRequired(missing, usage))
            elif groupErr.IsSome then Error groupErr.Value
            elif conflict.IsSome then Error conflict.Value
            elif requiresErr.IsSome then Error requiresErr.Value
            elif cmd.SubcommandRequired && st.Sub.IsNone then Error(MissingSubcommand usage)
            else Ok st

        let finalize (st: State) : Result<State, ParseError> =
            // environment fallbacks, then defaults. Invalid env values are errors.
            let envErr (a: Arg) (name: string) (v: string) (reason: string) =
                Error(InvalidValue(argDisplayName a, v, "from environment variable " + name + ": " + reason, usage))
            let applyEnv (st: State) (a: Arg) : Result<State, ParseError> =
                if Map.containsKey a.Id st.Vals || Map.containsKey a.Id st.Src then Ok st
                else
                    match a.Env with
                    | None -> Ok st
                    | Some name ->
                        match env name with
                        | Some v when v <> "" ->
                            // Flag-action args interpret the env value as a boolean
                            // (Value.bool semantics) instead of storing the raw string.
                            // SetTrue: truthy -> "true"; falsy -> "false".
                            // SetFalse (clap semantic: truthy env means the flag is
                            // "present"): truthy -> "false"; falsy -> nothing stored.
                            // Count: a numeric env value N is stored as the count N.
                            let set (x: string) =
                                Ok
                                    { st with
                                        Vals = Map.add a.Id [ x ] st.Vals
                                        Src = Map.add a.Id EnvVar st.Src }
                            match a.Action with
                            | SetTrue ->
                                match Value.bool v with
                                | Ok true -> set "true"
                                | Ok false -> set "false"
                                | Error _ -> envErr a name v "expected true or false"
                            | SetFalse ->
                                match Value.bool v with
                                | Ok true -> set "false"
                                | Ok false -> Ok st
                                | Error _ -> envErr a name v "expected true or false"
                            | Count ->
                                match System.Int32.TryParse v with
                                | true, _ -> set v
                                | _ -> envErr a name v "expected a number"
                            | _ ->
                                match addValue st a EnvVar v with
                                | Ok s -> Ok s
                                | Error(InvalidValue(n, bv, reason, u)) ->
                                    Error(InvalidValue(n, bv, "from environment variable " + name + ": " + reason, u))
                                | Error e -> Error e
                        | _ -> Ok st
            let withEnv =
                allArgs
                |> List.fold
                    (fun acc a ->
                        match acc with
                        | Error e -> Error e
                        | Ok st -> applyEnv st a)
                    (Ok st)
            withEnv
            |> Result.map (fun st ->
                allArgs
                |> List.fold
                    (fun (st: State) a ->
                        match a.Default with
                        | Some d when not (Map.containsKey a.Id st.Vals) ->
                            { st with
                                Vals = Map.add a.Id [ d ] st.Vals
                                Src = Map.add a.Id DefaultValue st.Src }
                        | _ -> st)
                    st)

        if cmd.ArgRequiredElseHelp && argv.IsEmpty then
            Error(helpFor false)
        else
            let init = { Vals = Map.empty; Src = Map.empty; Sub = None; ForcePositional = false }
            match loop init argv with
            | Error e -> Error e
            | Ok st ->
                match finalize st |> Result.bind validate with
                | Error e -> Error e
                | Ok st ->
                    Ok
                        { CommandName = cmd.Name
                          Values = st.Vals
                          Sources = st.Src
                          Sub = st.Sub }
