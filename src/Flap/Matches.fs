namespace Flap

open System

/// Explicit value parsers (no reflection, so AOT/Fable safe).
module Value =
    let string (s: string) : Result<string, string> = Ok s

    let int (s: string) : Result<int, string> =
        match Int32.TryParse s with
        | true, v -> Ok v
        | _ -> Error "not a valid integer"

    let int64 (s: string) : Result<int64, string> =
        match Int64.TryParse s with
        | true, v -> Ok v
        | _ -> Error "not a valid integer"

    let float (s: string) : Result<float, string> =
#if FABLE_COMPILER
        match Double.TryParse s with
#else
        match Double.TryParse(s, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
#endif
        | true, v -> Ok v
        | _ -> Error "not a valid number"

    let bool (s: string) : Result<bool, string> =
        match s.ToLowerInvariant() with
        | "true" | "yes" | "1" | "on" -> Ok true
        | "false" | "no" | "0" | "off" -> Ok false
        | _ -> Error "expected true or false"

    /// Map a string to one of the given cases.
    let oneOf (cases: (string * 'T) list) (s: string) : Result<'T, string> =
        match cases |> List.tryFind (fun (k, _) -> k = s) with
        | Some(_, v) -> Ok v
        | None -> Error("expected one of: " + (cases |> List.map fst |> String.concat ", "))

/// The result of a successful parse.
type Matches =
    { CommandName: string
      Values: Map<string, string list>
      Sources: Map<string, ValueSource>
      Sub: (string * Matches) option }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Matches =

    let empty (name: string) =
        { CommandName = name; Values = Map.empty; Sources = Map.empty; Sub = None }

    /// True when the arg has a value / was supplied (including defaults).
    let contains (id: string) (m: Matches) = m.Values.ContainsKey id

    let getMany (id: string) (m: Matches) : string list =
        m.Values |> Map.tryFind id |> Option.defaultValue []

    let tryString (id: string) (m: Matches) : string option =
        getMany id m |> List.tryLast

    /// Typed access using an explicit parser (see `Value`). Error if the value does not parse.
    let tryGetWith (parse: string -> Result<'T, string>) (id: string) (m: Matches) : Result<'T option, string> =
        match tryString id m with
        | None -> Ok None
        | Some s ->
            match parse s with
            | Ok v -> Ok(Some v)
            | Error e -> Error("invalid value '" + s + "' for '" + id + "': " + e)

    let getManyWith (parse: string -> Result<'T, string>) (id: string) (m: Matches) : Result<'T list, string> =
        let folder acc s =
            match acc, parse s with
            | Ok xs, Ok v -> Ok(v :: xs)
            | Error e, _ -> Error e
            | _, Error e -> Error("invalid value '" + s + "' for '" + id + "': " + e)
        getMany id m |> List.fold folder (Ok []) |> Result.map List.rev

    /// Optional typed access: Ok None if absent, Error if present but not parseable.
    let tryInt (id: string) (m: Matches) : Result<int option, string> = tryGetWith Value.int id m
    let tryBool (id: string) (m: Matches) : Result<bool option, string> = tryGetWith Value.bool id m
    let tryFloat (id: string) (m: Matches) : Result<float option, string> = tryGetWith Value.float id m

    /// Required typed access using an explicit parser. Error if absent or if the value does not parse.
    let getWith (parse: string -> Result<'T, string>) (id: string) (m: Matches) : Result<'T, string> =
        tryGetWith parse id m
        |> Result.bind (function
            | Some v -> Ok v
            | None -> Error("no value for argument '" + id + "'"))

    let getInt id m = getWith Value.int id m
    let getBool id m = getWith Value.bool id m
    let getFloat id m = getWith Value.float id m

    /// Value of a required/defaulted arg; throws if absent (like clap's `get_one().unwrap()`).
    let getString (id: string) (m: Matches) : string =
        match tryString id m with
        | Some s -> s
        | None -> failwith ("no value for argument '" + id + "'")

    /// Whether a flag (`SetTrue`/`SetFalse`) is on.
    let flag (id: string) (m: Matches) : bool =
        match tryString id m with
        | Some "true" -> true
        | _ -> false

    /// Occurrences of a `Count` flag.
    let count (id: string) (m: Matches) : int =
        match tryString id m with
        | Some s ->
            match Int32.TryParse s with
            | true, n -> n
            | _ -> 0
        | None -> 0

    let valueSource (id: string) (m: Matches) : ValueSource option = Map.tryFind id m.Sources

    let subcommand (m: Matches) : (string * Matches) option = m.Sub

    /// Matches of a specific subcommand, if it was used.
    let trySubcommand (name: string) (m: Matches) : Matches option =
        match m.Sub with
        | Some(n, sm) when n = name -> Some sm
        | _ -> None

    /// Raw args passed to an external subcommand.
    let externalArgs (m: Matches) : string list = getMany "" m
