namespace Flap

/// Runtime completion: given the words typed so far, compute candidates. Pure (no I/O), so it
/// works under Fable and is trivial to test. The console edge lives in `Command.parse`.
module Complete =
    open CompletionModel

    /// Name of the environment variable that puts `Command.parse` into completion mode for this
    /// app: `<NAME>_COMPLETE`, upper-cased with every non-alphanumeric character replaced by `_`
    /// (`my-tool` -> `MY_TOOL_COMPLETE`). Per-app so a nested Flap app doesn't inherit it.
    let envVarName (cmd: Command) : string =
        let isAlnum (c: char) = (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
        cmd.Name.ToUpperInvariant() |> String.map (fun c -> if isAlnum c then c else '_') |> fun s -> s + "_COMPLETE"

    let private startsWith (prefix: string) (s: string) = s.StartsWith(prefix, System.StringComparison.Ordinal)

    let private plain (v: string) : Candidate = { Value = v; Help = None }

    /// The sub-context for a subcommand, inheriting this context's global args (mirrors `contexts`).
    let private descend (ctx: Ctx) (sub: Command) : Ctx =
        let inherited = ctx.Args |> List.filter (fun a -> a.Global)
        let inh = inherited |> List.filter (fun g -> not (sub.Args |> List.exists (fun a -> a.Id = g.Id)))
        { CtxId = ctx.CtxId + "__" + ident sub.Name; Path = ctx.Path @ [ sub.Name ]; Cmd = sub; Args = sub.Args @ inh }

    let private findOption (ctx: Ctx) (word: string) : Arg option =
        valueOptions ctx |> List.tryFind (fun a -> optNames a |> List.contains word)

    let private findSub (ctx: Ctx) (word: string) : Command option =
        ctx.Cmd.Subcommands |> List.tryFind (fun s -> s.Name = word || List.contains word s.CommandAliases)

    /// Candidates for the value of `a`: the runtime completer if set (it receives the partial
    /// word and decides what to return), otherwise the static `PossibleValues` filtered by prefix.
    let private valuesFor (a: Arg) (cur: string) : Candidate list =
        match a.Completer with
        | Some f -> f cur
        | None -> a.PossibleValues |> List.filter (startsWith cur) |> List.map plain

    /// The positional argument covering 0-based value slot `pos`, if any.
    let private positionalAt (ctx: Ctx) (pos: int) : Arg option =
        ctx.Args
        |> List.filter (fun a -> a.Index.IsSome)
        |> List.sortBy (fun a -> a.Index.Value)
        |> List.fold
            (fun (start, found) a ->
                let n = max 1 a.MaxValues
                let unb = n >= unbounded
                let found =
                    match found with
                    | Some _ -> found
                    | None when pos >= start && (unb || pos < start + n) -> Some a
                    | None -> None
                (if unb then start + 1 else start + n), found)
            (0, None)
        |> snd

    /// Walk the words before the one being completed, tracking the current command context, the
    /// number of positional values seen, an option still waiting for its value, and `--`.
    let private walk (ctx: Ctx) (words: string list) : Ctx * int * Arg option * bool =
        let step (ctx, pos, pending: Arg option, afterDd) (w: string) =
            match pending with
            | Some _ -> ctx, pos, None, afterDd
            | None when afterDd -> ctx, pos + 1, None, afterDd
            | None when w = "--" -> ctx, pos, None, true
            | None when startsWith "--" w ->
                if w.Contains "=" then
                    ctx, pos, None, afterDd
                else
                    ctx, pos, findOption ctx w, afterDd
            | None when w.Length >= 2 && w.[0] = '-' ->
                // short option: `-m` waits for a value, `-mfast` carries it inline
                let pend = if w.Length = 2 then findOption ctx w else None
                ctx, pos, pend, afterDd
            | None ->
                match (if pos = 0 then findSub ctx w else None) with
                | Some sub -> descend ctx sub, 0, None, afterDd
                | None -> ctx, pos + 1, None, afterDd
        words |> List.fold step (ctx, 0, None, false)

    /// Completion candidates for `argv` (the words after the program name). The last element is
    /// the word being completed (`""` when the cursor follows a space). Never fails on partial input.
    let candidates (argv: string list) (cmd: Command) : Candidate list =
        let root = List.head (all cmd)
        let prior, cur =
            match List.rev argv with
            | [] -> [], ""
            | last :: restRev -> List.rev restRev, last
        let ctx, pos, pending, afterDd = walk root prior
        match pending with
        | Some a -> valuesFor a cur
        | None ->
            if not afterDd && startsWith "--" cur && cur.Contains "=" then
                // `--opt=par`: complete the value part of the option
                let name = cur.Substring(0, cur.IndexOf '=')
                let part = cur.Substring(cur.IndexOf '=' + 1)
                match findOption ctx name with
                | Some a -> valuesFor a part
                | None -> []
            elif not afterDd && startsWith "-" cur then
                flagsOf ctx |> List.filter (startsWith cur) |> List.map plain
            else
                let subs =
                    if pos = 0 && not afterDd then
                        subNames ctx.Cmd |> List.filter (startsWith cur) |> List.map plain
                    else
                        []
                let vals =
                    match positionalAt ctx pos with
                    | Some a -> valuesFor a cur
                    | None -> []
                subs @ vals

    let private oneLine (s: string) =
        s.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim()

    /// Format candidates for the shell's callback. bash: one value per line. Others:
    /// `value<TAB>description` per line (the description column is always present, possibly empty).
    let render (shell: Shell) (cs: Candidate list) : string =
        cs
        |> List.map (fun c ->
            let v = oneLine c.Value
            match shell with
            | Bash -> v + "\n"
            | Zsh
            | Fish
            | PowerShell
            | Nushell -> v + "\t" + (c.Help |> Option.map oneLine |> Option.defaultValue "") + "\n")
        |> String.concat ""
