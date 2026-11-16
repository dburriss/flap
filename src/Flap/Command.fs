namespace Flap

/// Builder functions for `Command`, designed for piping.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Command =

    let create (name: string) : Command =
        { Name = name
          VersionText = None
          About = None
          LongAbout = None
          BeforeHelp = None
          AfterHelp = None
          Author = None
          Args = []
          Subcommands = []
          Groups = []
          CommandAliases = []
          SubcommandRequired = false
          ArgRequiredElseHelp = false
          AllowExternalSubcommands = false
          HelpFlag = true
          VersionFlag = true
          HiddenCommand = false }

    let version (v: string) (c: Command) = { c with VersionText = Some v }
    let about (a: string) (c: Command) = { c with About = Some a }
    let longAbout (a: string) (c: Command) = { c with LongAbout = Some a }
    let beforeHelp (a: string) (c: Command) = { c with BeforeHelp = Some a }
    let afterHelp (a: string) (c: Command) = { c with AfterHelp = Some a }
    let author (a: string) (c: Command) = { c with Author = Some a }
    let arg (a: Arg) (c: Command) = { c with Args = c.Args @ [ a ] }
    let args (xs: Arg list) (c: Command) = { c with Args = c.Args @ xs }
    let subcommand (s: Command) (c: Command) = { c with Subcommands = c.Subcommands @ [ s ] }
    let subcommands (xs: Command list) (c: Command) = { c with Subcommands = c.Subcommands @ xs }
    let alias (a: string) (c: Command) = { c with CommandAliases = c.CommandAliases @ [ a ] }
    let subcommandRequired (c: Command) = { c with SubcommandRequired = true }
    let argRequiredElseHelp (c: Command) = { c with ArgRequiredElseHelp = true }
    let allowExternalSubcommands (c: Command) = { c with AllowExternalSubcommands = true }
    let disableHelpFlag (c: Command) = { c with HelpFlag = false }
    let disableVersionFlag (c: Command) = { c with VersionFlag = false }
    let hidden (c: Command) = { c with HiddenCommand = true }

    /// Declare a group. Members are arg ids.
    let group (id: string) (members: string list) (required: bool) (multiple: bool) (c: Command) =
        { c with Groups = c.Groups @ [ { GroupId = id; Members = members; GroupRequired = required; Multiple = multiple } ] }

    let private dups (xs: 'a list) =
        xs |> List.countBy id |> List.filter (fun (_, n) -> n > 1) |> List.map fst

    /// Validate a command definition. Returns a list of problems (empty when OK).
    let rec check (c: Command) : string list =
        let ids = c.Args |> List.map (fun a -> a.Id)
        let shorts = c.Args |> List.choose (fun a -> a.Short)
        let longs = c.Args |> List.collect (fun a -> Option.toList a.Long @ a.Aliases)
        let positions = c.Args |> List.choose (fun a -> a.Index)
        let known = set ids
        let groupIds = ArgGroup.all c |> List.map (fun g -> g.GroupId) |> set
        let refs =
            c.Args
            |> List.collect (fun a ->
                (a.Requires @ a.ConflictsWith @ a.RequiredUnless)
                |> List.filter (fun r -> not (known.Contains r) && not (groupIds.Contains r))
                |> List.map (fun r -> c.Name + ": arg '" + a.Id + "' references unknown arg '" + r + "'"))
        let positional =
            let sorted = positions |> List.sort
            if sorted <> List.init sorted.Length (fun i -> i + 1) then
                [ c.Name + ": positional indexes must be contiguous starting at 1" ]
            else
                []
        let trailing =
            let last = positions |> List.sortDescending |> List.tryHead
            c.Args
            |> List.filter (fun a -> a.TrailingVarArg)
            |> List.collect (fun a ->
                (if a.Index.IsNone || a.Index <> last then
                     [ c.Name + ": trailingVarArg arg '" + a.Id + "' must be the last positional" ]
                 else
                     [])
                @ (if a.MaxValues <= 1 then
                       [ c.Name + ": trailingVarArg arg '" + a.Id + "' must accept multiple values" ]
                   else
                       []))
        let noSpec =
            c.Args
            |> List.filter (fun a -> a.Index.IsNone && a.Short.IsNone && a.Long.IsNone)
            |> List.map (fun a -> c.Name + ": arg '" + a.Id + "' has no short, long or index")
        let mine =
            (dups ids |> List.map (fun d -> c.Name + ": duplicate arg id '" + d + "'"))
            @ (dups shorts |> List.map (fun d -> c.Name + ": duplicate short '-" + string d + "'"))
            @ (dups longs |> List.map (fun d -> c.Name + ": duplicate long '--" + d + "'"))
            @ (dups positions |> List.map (fun d -> c.Name + ": duplicate positional index " + string d))
            @ refs @ positional @ trailing @ noSpec
        mine @ (c.Subcommands |> List.collect check)

    /// Throw if the definition is invalid (use in tests / at startup, like clap's debug_assert).
    let debugAssert (c: Command) : Command =
        match check c with
        | [] -> c
        | errs -> failwith (String.concat "; " errs)

    /// Parse without side effects. `env` resolves environment variables for `Arg.env`.
    let tryParseWith (env: string -> string option) (argv: string list) (cmd: Command) : Result<Matches, ParseError> =
        match check cmd with
        | [] -> Parser.parseCommand env cmd.Name cmd [] argv
        | errs -> Error(DefinitionError(String.concat "; " errs))

    /// Parse without side effects, ignoring environment variables.
    let tryParse (argv: string list) (cmd: Command) : Result<Matches, ParseError> =
        tryParseWith (fun _ -> None) argv cmd

    /// Parse and handle help/version/errors through callbacks (testable edge).
    let parseWith
        (env: string -> string option)
        (out: string -> unit)
        (err: string -> unit)
        (exit: int -> Matches)
        (argv: string list)
        (cmd: Command)
        : Matches =
        match tryParseWith env argv cmd with
        | Ok m -> m
        | Error e ->
            let rendered = ParseError.render e
            let text = if rendered.EndsWith "\n" then rendered else rendered + "\n"
            if ParseError.exitCode e = 0 then out text else err text
            exit (ParseError.exitCode e)

#if !FABLE_COMPILER
    /// Parse real process arguments: prints help/version/errors and exits like clap.
    /// Not available under Fable; use `parseWith` or `tryParse` there.
    let parse (argv: string list) (cmd: Command) : Matches =
        let env (name: string) : string option =
            match System.Environment.GetEnvironmentVariable name with
            | null -> None
            | v -> Some v
        parseWith env (fun s -> System.Console.Out.Write s) (fun s -> System.Console.Error.Write s) (fun c -> exit c) argv cmd
#endif
