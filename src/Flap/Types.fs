namespace Flap

/// A shell that completion scripts can be generated for.
type Shell =
    | Bash
    | Zsh
    | Fish
    | PowerShell
    | Nushell

/// What an argument does when it is seen on the command line.
type ArgAction =
    | Set
    | Append
    | SetTrue
    | SetFalse
    | Count
    | Help
    | Version

/// Where a value came from.
type ValueSource =
    | DefaultValue
    | EnvVar
    | CommandLine

/// What kind of value an argument takes, used by shell completions.
type ValueHint =
    /// Any filesystem path (file or directory).
    | AnyPath
    /// A file path.
    | FilePath
    /// A directory path.
    | DirPath
    /// Anything else: no path completion.
    | Other

/// A completion suggestion returned by an argument's completer.
type Candidate =
    { /// The text inserted when the candidate is chosen.
      Value: string
      /// Optional description shown next to the candidate by shells that support it.
      Help: string option }

/// A command line argument definition (flag, option or positional).
type Arg =
    { Id: string
      Short: char option
      Long: string option
      Aliases: string list
      HelpText: string option
      ValueName: string option
      Action: ArgAction
      Required: bool
      Default: string option
      Env: string option
      Index: int option
      MinValues: int
      MaxValues: int
      Delimiter: char option
      PossibleValues: string list
      Validator: (string -> Result<unit, string>) option
      Hidden: bool
      Global: bool
      Requires: string list
      ConflictsWith: string list
      RequiredUnless: string list
      Group: string list
      AllowHyphenValues: bool
      TrailingVarArg: bool
      HelpHeading: string option
      ValueHint: ValueHint option
      /// Computes completion candidates at runtime from the partial word being completed.
      Completer: (string -> Candidate list) option }

/// A named set of arguments.
type ArgGroup =
    { GroupId: string
      Members: string list
      GroupRequired: bool
      Multiple: bool }

/// A command (or subcommand) definition.
type Command =
    { Name: string
      VersionText: string option
      About: string option
      LongAbout: string option
      BeforeHelp: string option
      AfterHelp: string option
      Author: string option
      Args: Arg list
      Subcommands: Command list
      Groups: ArgGroup list
      CommandAliases: string list
      SubcommandRequired: bool
      /// When a subcommand is given, this command's required args and required groups are not enforced.
      SubcommandNegatesRequired: bool
      ArgRequiredElseHelp: bool
      AllowExternalSubcommands: bool
      HelpFlag: bool
      VersionFlag: bool
      HiddenCommand: bool }

/// Why parsing did not produce matches.
type ParseError =
    | DisplayHelp of text: string
    | DisplayVersion of text: string
    | MissingRequired of args: string list * usage: string
    | UnknownArgument of arg: string * suggestion: string option * usage: string
    | UnknownSubcommand of name: string * suggestion: string option * usage: string
    | MissingValue of arg: string * usage: string
    | InvalidValue of arg: string * value: string * reason: string * usage: string
    | TooManyValues of arg: string * value: string * usage: string
    | TooFewValues of arg: string * usage: string
    | ArgumentConflict of arg: string * other: string * usage: string
    | MissingRequiredBy of arg: string * required: string * usage: string
    | MissingSubcommand of usage: string
    | GroupConflict of group: string * usage: string
    | GroupRequired of group: string * usage: string
    | DefinitionError of string

module ParseError =
    /// The message to show the user (help/version text, or an `error:` message).
    let render (e: ParseError) : string =
        match e with
        | DisplayHelp t -> t
        | DisplayVersion t -> t
        | DefinitionError m -> "error: invalid command definition: " + m
        | MissingRequired(args, u) ->
            "error: the following required arguments were not provided:\n"
            + (args |> List.map (fun a -> "  " + a) |> String.concat "\n")
            + "\n\n" + u
        | UnknownArgument(a, s, u) ->
            "error: unexpected argument '" + a + "' found"
            + (match s with
               | Some s -> "\n\n  tip: a similar argument exists: '" + s + "'"
               | None -> "")
            + "\n\n" + u
        | UnknownSubcommand(a, s, u) ->
            "error: unrecognized subcommand '" + a + "'"
            + (match s with
               | Some s -> "\n\n  tip: a similar subcommand exists: '" + s + "'"
               | None -> "")
            + "\n\n" + u
        | MissingValue(a, u) -> "error: a value is required for '" + a + "' but none was supplied\n\n" + u
        | InvalidValue(a, v, r, u) -> "error: invalid value '" + v + "' for '" + a + "': " + r + "\n\n" + u
        | TooManyValues(a, v, u) -> "error: unexpected value '" + v + "' for '" + a + "' found; no more were expected\n\n" + u
        | TooFewValues(a, u) -> "error: more values required for '" + a + "'\n\n" + u
        | ArgumentConflict(a, o, u) -> "error: the argument '" + a + "' cannot be used with '" + o + "'\n\n" + u
        | MissingRequiredBy(a, r, u) -> "error: the argument '" + a + "' requires '" + r + "' to also be provided\n\n" + u
        | MissingSubcommand u -> "error: a subcommand is required but one was not provided\n\n" + u
        | GroupConflict(g, u) -> "error: the arguments in group '" + g + "' cannot be used together\n\n" + u
        | GroupRequired(g, u) -> "error: one of the arguments in group '" + g + "' is required\n\n" + u

    /// clap-compatible exit code: 0 for help/version, 2 for usage errors.
    let exitCode (e: ParseError) : int =
        match e with
        | DisplayHelp _
        | DisplayVersion _ -> 0
        | _ -> 2

module ArgGroup =
    /// All groups of a command, including those implied by `Arg.group`.
    let all (c: Command) : ArgGroup list =
        let implied =
            c.Args |> List.collect (fun a -> a.Group |> List.map (fun g -> g, a.Id))
        let ids =
            (c.Groups |> List.map (fun g -> g.GroupId)) @ (implied |> List.map fst) |> List.distinct
        ids
        |> List.map (fun id ->
            let declared = c.Groups |> List.tryFind (fun g -> g.GroupId = id)
            let extra = implied |> List.filter (fun (g, _) -> g = id) |> List.map snd
            match declared with
            | Some g -> { g with Members = (g.Members @ extra) |> List.distinct }
            | None -> { GroupId = id; Members = extra; GroupRequired = false; Multiple = false })
