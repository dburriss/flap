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
          Group = [] }

    let short (c: char) (a: Arg) = { a with Short = Some c }
    let long (l: string) (a: Arg) = { a with Long = Some l }
    let alias (l: string) (a: Arg) = { a with Aliases = a.Aliases @ [ l ] }
    let help (h: string) (a: Arg) = { a with HelpText = Some h }
    let valueName (n: string) (a: Arg) = { a with ValueName = Some n }
    let action (act: ArgAction) (a: Arg) = { a with Action = act }
    let required (a: Arg) = { a with Required = true }
    let defaultValue (v: string) (a: Arg) = { a with Default = Some v }
    let env (name: string) (a: Arg) = { a with Env = Some name }
    let hidden (a: Arg) = { a with Hidden = true }
    /// `global` is reserved in F#; propagates the arg to all subcommands.
    let global' (a: Arg) = { a with Global = true }
    let delimiter (c: char) (a: Arg) = { a with Delimiter = Some c }
    let possibleValues (vs: string list) (a: Arg) = { a with PossibleValues = vs }
    let validate (f: string -> Result<unit, string>) (a: Arg) = { a with Validator = Some f }
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

    let isPositional (a: Arg) = a.Index.IsSome
    let takesValue (a: Arg) =
        match a.Action with
        | Set
        | Append -> true
        | _ -> false
