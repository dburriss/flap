namespace Flap

/// Builder functions for `Arg`, designed for piping.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Arg =

    let create (id: string) : Arg =
        { Id = id
          Short = None
          Long = None
          Aliases = []
          HelpText = None
          ValueName = None
          Action = Set
          Required = false
          Default = None
          Env = None
          Index = None
          MinValues = 1
          MaxValues = 1
          Delimiter = None
          PossibleValues = []
          Validator = None
          Hidden = false
          Global = false
          Requires = []
          ConflictsWith = []
          RequiredUnless = []
          Group = []
          AllowHyphenValues = false
          TrailingVarArg = false
          HelpHeading = None
          ValueHint = None
          Completer = None }

    let short (c: char) (a: Arg) = { a with Short = Some c }
    let long (l: string) (a: Arg) = { a with Long = Some l }
    let alias (l: string) (a: Arg) = { a with Aliases = a.Aliases @ [ l ] }
    let help (h: string) (a: Arg) = { a with HelpText = Some h }
    let valueName (n: string) (a: Arg) = { a with ValueName = Some n }
    let action (act: ArgAction) (a: Arg) = { a with Action = act }
    let required (a: Arg) = { a with Required = true }
    let defaultValue (v: string) (a: Arg) = { a with Default = Some v }
    let env (name: string) (a: Arg) = { a with Env = Some name }
    let helpHeading (h: string) (a: Arg) = { a with HelpHeading = Some h }
    /// Hint the kind of value (e.g. `FilePath`, `DirPath`) so shell completions can complete paths.
    let valueHint (h: ValueHint) (a: Arg) = { a with ValueHint = Some h }
    let hidden (a: Arg) = { a with Hidden = true }
    /// `global` is reserved in F#; propagates the arg to all subcommands.
    let global' (a: Arg) = { a with Global = true }
    let delimiter (c: char) (a: Arg) = { a with Delimiter = Some c }
    let possibleValues (vs: string list) (a: Arg) = { a with PossibleValues = vs }
    let validate (f: string -> Result<unit, string>) (a: Arg) = { a with Validator = Some f }

    /// Complete this argument's value at runtime. The function receives the partial word being
    /// completed and returns candidates. The generated shell script calls back into the program for it.
    let completer (f: string -> Candidate list) (a: Arg) = { a with Completer = Some f }

    /// Like `completer`, for suggestions without descriptions.
    let completerValues (f: string -> string list) (a: Arg) =
        { a with Completer = Some(fun w -> f w |> List.map (fun v -> { Value = v; Help = None })) }
    let requires (id: string) (a: Arg) = { a with Requires = a.Requires @ [ id ] }
    let conflictsWith (id: string) (a: Arg) = { a with ConflictsWith = a.ConflictsWith @ [ id ] }
    let requiredUnless (id: string) (a: Arg) = { a with RequiredUnless = a.RequiredUnless @ [ id ] }
    let group (g: string) (a: Arg) = { a with Group = a.Group @ [ g ] }

    /// Positional argument at 1-based index.
    let positional (index: int) (a: Arg) = { a with Index = Some index }

    /// Accept between `min` and `max` values (for options this is per occurrence, for positionals total).
    let numArgs (min: int) (max: int) (a: Arg) = { a with MinValues = min; MaxValues = max }

    /// Boolean flag (no value).
    let flag (a: Arg) = { a with Action = SetTrue; MinValues = 0; MaxValues = 0 }

    /// Flag that counts occurrences (`-vvv`).
    let count (a: Arg) = { a with Action = Count; MinValues = 0; MaxValues = 0 }

    /// Option that may be repeated, collecting all values.
    let append (a: Arg) = { a with Action = Append }

    /// Positional/option taking any number of values.
    let manyValues (a: Arg) = { a with Action = Append; MinValues = 1; MaxValues = System.Int32.MaxValue }

    /// Allow the value of an option (the token right after it) or a positional to start with `-`.
    /// For a positional, a dash-leading token is accepted as its value unless it is a known flag
    /// (a trailingVarArg positional accepts known flags too).
    let allowHyphenValues (a: Arg) = { a with AllowHyphenValues = true }

    /// Last positional: after its first value, every remaining token is taken verbatim (no flag,
    /// subcommand, help or version handling). Implies multi-value: if the arg is not already
    /// multi-value (MaxValues > 1) it becomes Append with 1..Int32.MaxValue values.
    let trailingVarArg (a: Arg) =
        if a.MaxValues > 1 then
            { a with TrailingVarArg = true }
        else
            { a with TrailingVarArg = true; Action = Append; MinValues = 1; MaxValues = System.Int32.MaxValue }

    let isPositional (a: Arg) = a.Index.IsSome
    let takesValue (a: Arg) =
        match a.Action with
        | Set
        | Append -> true
        | _ -> false
