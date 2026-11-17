namespace Flap

/// Pure model shared by the static script generators (`Completions`) and the runtime
/// completion engine (`Complete`). Internal: not part of the public API.
module internal CompletionModel =


    /// What to complete for a value: fixed values and/or a path kind ('f' = any path/file, 'd' = directory).
    type ValueSpec = { Values: string list; Path: char option }

    /// A positional completion slot: 0-based first/last value slot (None = unbounded).
    type Slot = { First: int; Last: int option; Spec: ValueSpec }

    /// A command in the tree, with the args it accepts (own args + inherited globals, like the parser).
    type Ctx = { CtxId: string; Path: string list; Cmd: Command; Args: Arg list }

    let ident (s: string) = s.Replace('-', '_')

    let rec contexts (id: string) (path: string list) (inherited: Arg list) (cmd: Command) : Ctx list =
        let inh = inherited |> List.filter (fun g -> not (cmd.Args |> List.exists (fun a -> a.Id = g.Id)))
        let child = inh @ (cmd.Args |> List.filter (fun a -> a.Global))
        { CtxId = id; Path = path; Cmd = cmd; Args = cmd.Args @ inh }
        :: (cmd.Subcommands |> List.collect (fun s -> contexts (id + "__" + ident s.Name) (path @ [ s.Name ]) child s))

    let all (cmd: Command) = contexts (ident cmd.Name) [ cmd.Name ] [] cmd

    /// (word, child id) pairs for every subcommand name/alias of a context.
    let transitionsOf (ctx: Ctx) : (string * string) list =
        ctx.Cmd.Subcommands
        |> List.collect (fun s -> (s.Name :: s.CommandAliases) |> List.map (fun n -> n, ctx.CtxId + "__" + ident s.Name))

    let visibleOptions (ctx: Ctx) : Arg list =
        (Help.builtins ctx.Cmd @ ctx.Args) |> List.filter (fun a -> a.Index.IsNone && not a.Hidden)

    let flagsOf (ctx: Ctx) : string list =
        visibleOptions ctx
        |> List.collect (fun a ->
            (a.Short |> Option.map (fun c -> "-" + string c) |> Option.toList)
            @ (a.Long |> Option.map (fun l -> "--" + l) |> Option.toList))

    let subNames (cmd: Command) =
        cmd.Subcommands |> List.filter (fun s -> not s.HiddenCommand) |> List.collect (fun s -> s.Name :: s.CommandAliases)

    /// All spellings of an option: `-s`, `--long`, `--alias`.
    let optNames (a: Arg) : string list =
        (a.Short |> Option.map (fun c -> "-" + string c) |> Option.toList)
        @ (a.Long |> Option.map (fun l -> "--" + l) |> Option.toList)
        @ (a.Aliases |> List.map (fun l -> "--" + l))

    let pathOf (a: Arg) : char option =
        match a.ValueHint with
        | Some AnyPath
        | Some FilePath -> Some 'f'
        | Some DirPath -> Some 'd'
        | Some Other
        | None -> None

    let specOf (a: Arg) : ValueSpec option =
        let spec = { Values = a.PossibleValues; Path = pathOf a }
        if spec.Values.IsEmpty && spec.Path.IsNone then None else Some spec

    /// Visible value-taking options (these consume the next word).
    let valueOptions (ctx: Ctx) : Arg list =
        visibleOptions ctx |> List.filter Arg.takesValue

    /// Value-taking options with something to complete, with all their spellings.
    let optionSpecs (ctx: Ctx) : (string list * ValueSpec) list =
        valueOptions ctx |> List.choose (fun a -> specOf a |> Option.map (fun s -> optNames a, s))

    let unbounded = 100000

    let slots (ctx: Ctx) : Slot list =
        ctx.Args
        |> List.filter (fun a -> a.Index.IsSome)
        |> List.sortBy (fun a -> a.Index.Value)
        |> List.fold
            (fun (start, acc) a ->
                let n = max 1 a.MaxValues
                let last = if n >= unbounded then None else Some(start + n - 1)
                let next = if n >= unbounded then start + 1 else start + n
                let acc =
                    match specOf a with
                    | Some s when not a.Hidden -> acc @ [ { First = start; Last = last; Spec = s } ]
                    | _ -> acc
                next, acc)
            (0, [])
        |> snd
