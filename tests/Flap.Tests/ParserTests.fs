module Flap.Tests.ParserTests

open Xunit
open Flap

let app =
    Command.create "app"
    |> Command.version "1.2.3"
    |> Command.about "Does things"
    |> Command.arg (Arg.create "name" |> Arg.short 'n' |> Arg.long "name" |> Arg.help "Your name")
    |> Command.arg (Arg.create "verbose" |> Arg.short 'v' |> Arg.long "verbose" |> Arg.count)
    |> Command.arg (Arg.create "quiet" |> Arg.short 'q' |> Arg.long "quiet" |> Arg.flag)
    |> Command.arg (Arg.create "mode" |> Arg.long "mode" |> Arg.possibleValues [ "fast"; "slow" ] |> Arg.defaultValue "fast")
    |> Command.arg (Arg.create "tag" |> Arg.short 't' |> Arg.long "tag" |> Arg.append)
    |> Command.arg (Arg.create "file" |> Arg.positional 1 |> Arg.required)
    |> Command.subcommand (
        Command.create "build"
        |> Command.about "Build it"
        |> Command.arg (Arg.create "release" |> Arg.long "release" |> Arg.flag)
    )

let ok argv =
    match Command.tryParse argv app with
    | Ok m -> m
    | Error e -> failwithf "unexpected error: %s" (ParseError.render e)

let err argv =
    match Command.tryParse argv app with
    | Ok _ -> failwith "expected error"
    | Error e -> e

[<Fact>]
let ``long, short, inline and clusters`` () =
    let m = ok [ "--name=bob"; "-vvq"; "f.txt" ]
    Assert.Equal("bob", Matches.getString "name" m)
    Assert.Equal(2, Matches.count "verbose" m)
    Assert.True(Matches.flag "quiet" m)
    Assert.Equal("f.txt", Matches.getString "file" m)

[<Fact>]
let ``short value attached and separate`` () =
    Assert.Equal(Some "x", ok [ "-nx"; "f" ] |> Matches.tryString "name")
    Assert.Equal(Some "x", ok [ "-n"; "x"; "f" ] |> Matches.tryString "name")

[<Fact>]
let ``defaults and sources`` () =
    let m = ok [ "f" ]
    Assert.Equal("fast", Matches.getString "mode" m)
    Assert.Equal(Some DefaultValue, Matches.valueSource "mode" m)
    Assert.False(Matches.flag "quiet" m)

[<Fact>]
let ``append collects values`` () =
    let m = ok [ "-t"; "a"; "--tag"; "b"; "f" ]
    Assert.Equal<string list>([ "a"; "b" ], Matches.getMany "tag" m)

[<Fact>]
let ``double dash makes positionals`` () =
    Assert.Equal("-weird", ok [ "--"; "-weird" ] |> Matches.getString "file")

[<Fact>]
let ``subcommand parsed`` () =
    let m = ok [ "f"; "build"; "--release" ]
    let sub = Matches.trySubcommand "build" m
    Assert.True(sub.IsSome)
    Assert.True(Matches.flag "release" sub.Value)

[<Fact>]
let ``missing required`` () =
    match err [] with
    | MissingRequired([ "<FILE>" ], _) -> ()
    | e -> failwithf "%A" (ParseError.render e)

[<Fact>]
let ``invalid possible value`` () =
    match err [ "--mode"; "medium"; "f" ] with
    | InvalidValue("--mode", "medium", _, _) -> ()
    | e -> failwith (ParseError.render e)

[<Fact>]
let ``unknown arg suggests`` () =
    match err [ "--verbos"; "f" ] with
    | UnknownArgument(_, Some "--verbose", _) -> ()
    | e -> failwith (ParseError.render e)

[<Fact>]
let ``too many positionals`` () =
    match err [ "a"; "b" ] with
    | UnknownArgument("b", _, _) -> ()
    | e -> failwith (ParseError.render e)

[<Fact>]
let ``missing option value`` () =
    match err [ "f"; "--name" ] with
    | MissingValue("--name", _) -> ()
    | e -> failwith (ParseError.render e)

[<Fact>]
let ``help and version`` () =
    match err [ "--help" ] with
    | DisplayHelp t ->
        Assert.Contains("Usage: app [OPTIONS] <FILE> [COMMAND]", t)
        Assert.Contains("-n, --name <NAME>", t)
        Assert.Contains("[default: fast]", t)
        Assert.Contains("build", t)
    | _ -> failwith "expected help"
    match err [ "-V" ] with
    | DisplayVersion "app 1.2.3\n" -> ()
    | _ -> failwith "expected version"
    Assert.Equal(0, ParseError.exitCode (DisplayHelp ""))
    Assert.Equal(2, ParseError.exitCode (MissingSubcommand ""))

[<Fact>]
let ``help subcommand`` () =
    match err [ "f"; "help"; "build" ] with
    | DisplayHelp t -> Assert.Contains("Usage: app build", t)
    | _ -> failwith "expected help"

[<Fact>]
let ``conflicts, requires, groups`` () =
    let c =
        Command.create "c"
        |> Command.arg (Arg.create "a" |> Arg.long "a" |> Arg.flag |> Arg.conflictsWith "b")
        |> Command.arg (Arg.create "b" |> Arg.long "b" |> Arg.flag)
        |> Command.arg (Arg.create "x" |> Arg.long "x" |> Arg.flag |> Arg.requires "a")
        |> Command.arg (Arg.create "j" |> Arg.long "j" |> Arg.flag |> Arg.group "fmt")
        |> Command.arg (Arg.create "y" |> Arg.long "y" |> Arg.flag |> Arg.group "fmt")
    let run argv = Command.tryParse argv c
    Assert.True(match run [ "--a"; "--b" ] with Error(ArgumentConflict _) -> true | _ -> false)
    Assert.True(match run [ "--x" ] with Error(MissingRequiredBy _) -> true | _ -> false)
    Assert.True(match run [ "--x"; "--a" ] with Ok _ -> true | _ -> false)
    Assert.True(match run [ "--j"; "--y" ] with Error(GroupConflict _) -> true | _ -> false)

[<Fact>]
let ``env fallback`` () =
    let c = Command.create "c" |> Command.arg (Arg.create "p" |> Arg.long "p" |> Arg.env "P")
    let m = Command.tryParseWith (fun n -> if n = "P" then Some "7" else None) [] c |> Result.defaultWith (fun _ -> failwith "x")
    Assert.Equal(Ok(Some 7), Matches.tryInt "p" m)
    Assert.Equal(Some EnvVar, Matches.valueSource "p" m)

[<Fact>]
let ``multi values and delimiter`` () =
    let c =
        Command.create "c"
        |> Command.arg (Arg.create "l" |> Arg.long "l" |> Arg.delimiter ',' |> Arg.append)
        |> Command.arg (Arg.create "rest" |> Arg.positional 1 |> Arg.manyValues)
    let m = Command.tryParse [ "--l"; "a,b"; "x"; "y"; "z" ] c |> Result.defaultWith (fun _ -> failwith "x")
    Assert.Equal<string list>([ "a"; "b" ], Matches.getMany "l" m)
    Assert.Equal<string list>([ "x"; "y"; "z" ], Matches.getMany "rest" m)

[<Fact>]
let ``global args propagate to subcommands`` () =
    let c =
        Command.create "c"
        |> Command.arg (Arg.create "v" |> Arg.long "v" |> Arg.flag |> Arg.global')
        |> Command.subcommand (Command.create "s")
    let m = Command.tryParse [ "s"; "--v" ] c
    Assert.True(match m with Ok m -> Matches.flag "v" (Matches.trySubcommand "s" m).Value | _ -> false)

[<Fact>]
let ``external subcommands`` () =
    let c = Command.create "c" |> Command.allowExternalSubcommands
    match Command.tryParse [ "foo"; "--bar"; "x" ] c with
    | Ok m ->
        let n, sm = (Matches.subcommand m).Value
        Assert.Equal("foo", n)
        Assert.Equal<string list>([ "--bar"; "x" ], Matches.externalArgs sm)
    | Error e -> failwith (ParseError.render e)

[<Fact>]
let ``definition problems detected`` () =
    let bad =
        Command.create "c"
        |> Command.arg (Arg.create "a" |> Arg.short 'x')
        |> Command.arg (Arg.create "b" |> Arg.short 'x')
    Assert.NotEmpty(Command.check bad)
    Assert.Throws<exn>(fun () -> Command.debugAssert bad |> ignore) |> ignore

[<Fact>]
let ``subcommand required`` () =
    let c = Command.create "c" |> Command.subcommand (Command.create "s") |> Command.subcommandRequired
    Assert.True(match Command.tryParse [] c with Error(MissingSubcommand _) -> true | _ -> false)

[<Fact>]
let ``missing subcommand error has no stray quotes`` () =
    let c = Command.create "c" |> Command.subcommand (Command.create "s") |> Command.subcommandRequired
    match Command.tryParse [] c with
    | Error e ->
        let rendered = ParseError.render e
        Assert.StartsWith("error: a subcommand is required but one was not provided", rendered)
        Assert.DoesNotContain("'a subcommand", rendered)
    | Ok _ -> failwith "expected MissingSubcommand error"

[<Fact>]
let ``completions generate for all shells`` () =
    for sh in [ Bash; Zsh; Fish; PowerShell ] do
        let s = Completions.generate sh app
        Assert.Contains((if sh = Fish then "-l verbose" else "--verbose"), s)
        Assert.Contains("build", s)

[<Fact>]
let ``getWith returns parsed value when present and valid`` () =
    let m = ok [ "--name=42"; "f" ]
    Assert.Equal(Ok 42, Matches.getWith Value.int "name" m)

[<Fact>]
let ``getWith errors when absent`` () =
    let m = ok [ "f" ]
    Assert.Equal(Error "no value for argument 'name'", Matches.getWith Value.int "name" m)

[<Fact>]
let ``getWith errors when value does not parse`` () =
    let m = ok [ "--name=abc"; "f" ]
    Assert.Equal(Error "invalid value 'abc' for 'name': not a valid integer", Matches.getWith Value.int "name" m)

[<Fact>]
let ``getInt getBool getFloat parse present values`` () =
    Assert.Equal(Ok 7, Matches.getInt "name" (ok [ "--name=7"; "f" ]))
    Assert.Equal(Ok true, Matches.getBool "name" (ok [ "--name=yes"; "f" ]))
    Assert.Equal(Ok 1.5, Matches.getFloat "name" (ok [ "--name=1.5"; "f" ]))

[<Fact>]
let ``getInt getBool getFloat error when absent or invalid`` () =
    Assert.Equal(Error "no value for argument 'name'", Matches.getInt "name" (ok [ "f" ]))
    Assert.Equal(Error "invalid value 'x' for 'name': expected true or false", Matches.getBool "name" (ok [ "--name=x"; "f" ]))
    Assert.Equal(Error "invalid value 'x' for 'name': not a valid number", Matches.getFloat "name" (ok [ "--name=x"; "f" ]))

[<Fact>]
let ``tryInt tryBool tryFloat return Error for invalid values`` () =
    let c =
        Command.create "c"
        |> Command.arg (Arg.create "n" |> Arg.long "n")
    let m = Command.tryParse [ "--n"; "abc" ] c |> Result.defaultWith (fun _ -> failwith "x")
    Assert.True(Result.isError (Matches.tryInt "n" m))
    Assert.True(Result.isError (Matches.tryBool "n" m))
    Assert.True(Result.isError (Matches.tryFloat "n" m))

[<Fact>]
let ``tryInt tryBool tryFloat return Ok for valid or absent values`` () =
    let c =
        Command.create "c"
        |> Command.arg (Arg.create "i" |> Arg.long "i")
        |> Command.arg (Arg.create "b" |> Arg.long "b")
        |> Command.arg (Arg.create "f" |> Arg.long "f")
        |> Command.arg (Arg.create "none" |> Arg.long "none")
    let m = Command.tryParse [ "--i"; "5"; "--b"; "true"; "--f"; "1.5" ] c |> Result.defaultWith (fun _ -> failwith "x")
    Assert.Equal(Ok(Some 5), Matches.tryInt "i" m)
    Assert.Equal(Ok(Some true), Matches.tryBool "b" m)
    Assert.Equal(Ok(Some 1.5), Matches.tryFloat "f" m)
    Assert.Equal(Ok None, Matches.tryInt "none" m)
    Assert.Equal(Ok None, Matches.tryBool "none" m)
    Assert.Equal(Ok None, Matches.tryFloat "none" m)

let private envFlagCmd =
    Command.create "c" |> Command.arg (Arg.create "d" |> Arg.long "d" |> Arg.flag |> Arg.env "D")

let private parseEnvFlag (v: string) (args: string list) =
    Command.tryParseWith (fun n -> if n = "D" then Some v else None) args envFlagCmd
    |> Result.defaultWith (fun _ -> failwith "x")

[<Theory>]
[<InlineData("1")>]
[<InlineData("true")>]
[<InlineData("yes")>]
[<InlineData("on")>]
[<InlineData("TRUE")>]
let ``env flag truthy values read as true`` (v: string) =
    let m = parseEnvFlag v []
    Assert.True(Matches.flag "d" m)
    Assert.Equal(Some EnvVar, Matches.valueSource "d" m)

[<Theory>]
[<InlineData("0")>]
[<InlineData("false")>]
[<InlineData("no")>]
[<InlineData("off")>]
let ``env flag falsy values read as false`` (v: string) =
    Assert.False(Matches.flag "d" (parseEnvFlag v []))

[<Fact>]
let ``command-line flag wins over env flag`` () =
    let m = parseEnvFlag "0" [ "--d" ]
    Assert.True(Matches.flag "d" m)
    Assert.Equal(Some CommandLine, Matches.valueSource "d" m)

[<Fact>]
let ``env set-false flag truthy stores false`` () =
    let c =
        Command.create "c"
        |> Command.arg (let a = Arg.create "d" |> Arg.long "d" |> Arg.flag |> Arg.env "D" in { a with Action = SetFalse })
    let m = Command.tryParseWith (fun _ -> Some "1") [] c |> Result.defaultWith (fun _ -> failwith "x")
    Assert.False(Matches.flag "d" m)
    Assert.Equal(Some EnvVar, Matches.valueSource "d" m)

[<Fact>]
let ``env count flag uses numeric value`` () =
    let c = Command.create "c" |> Command.arg (Arg.create "v" |> Arg.long "v" |> Arg.count |> Arg.env "V")
    let m = Command.tryParseWith (fun _ -> Some "3") [] c |> Result.defaultWith (fun _ -> failwith "x")
    Assert.Equal(3, Matches.count "v" m)

let private envOf (k: string) (v: string) = fun n -> if n = k then Some v else None

let private envReason (r: Result<Matches, ParseError>) =
    match r with
    | Error(InvalidValue(_, _, reason, _)) -> Some reason
    | _ -> None

[<Fact>]
let ``invalid env value failing validator is an error naming the env var`` () =
    let c = Command.create "c" |> Command.arg (Arg.create "n" |> Arg.long "n" |> Arg.env "MYAPP_N" |> Arg.validate (Value.int >> Result.map ignore))
    let r = Command.tryParseWith (envOf "MYAPP_N" "abc") [] c
    match envReason r with
    | Some reason -> Assert.Contains("MYAPP_N", reason)
    | None -> failwith "expected InvalidValue"

[<Fact>]
let ``invalid env possible value is an error`` () =
    let c = Command.create "c" |> Command.arg (Arg.create "m" |> Arg.long "m" |> Arg.env "MYAPP_M" |> Arg.possibleValues [ "a"; "b" ])
    let r = Command.tryParseWith (envOf "MYAPP_M" "z") [] c
    match envReason r with
    | Some reason -> Assert.Contains("MYAPP_M", reason)
    | None -> failwith "expected InvalidValue"

[<Fact>]
let ``unparseable env boolean for flag is an error`` () =
    let c = Command.create "c" |> Command.arg (Arg.create "f" |> Arg.long "f" |> Arg.flag |> Arg.env "MYAPP_F")
    let r = Command.tryParseWith (envOf "MYAPP_F" "maybe") [] c
    match envReason r with
    | Some reason -> Assert.Contains("MYAPP_F", reason)
    | None -> failwith "expected InvalidValue"

[<Fact>]
let ``non-numeric env for count is an error`` () =
    let c = Command.create "c" |> Command.arg (Arg.create "v" |> Arg.short 'v' |> Arg.count |> Arg.env "MYAPP_V")
    let r = Command.tryParseWith (envOf "MYAPP_V" "lots") [] c
    match envReason r with
    | Some reason -> Assert.Contains("MYAPP_V", reason)
    | None -> failwith "expected InvalidValue"

[<Fact>]
let ``valid env value still ok`` () =
    let c = Command.create "c" |> Command.arg (Arg.create "n" |> Arg.long "n" |> Arg.env "MYAPP_N" |> Arg.validate (Value.int >> Result.map ignore))
    let m = Command.tryParseWith (envOf "MYAPP_N" "5") [] c |> Result.defaultWith (fun _ -> failwith "x")
    Assert.Equal(Ok(Some 5), Matches.tryInt "n" m)

[<Fact>]
let ``command line overrides invalid env without error`` () =
    let c = Command.create "c" |> Command.arg (Arg.create "n" |> Arg.long "n" |> Arg.env "MYAPP_N" |> Arg.validate (Value.int >> Result.map ignore))
    let m = Command.tryParseWith (envOf "MYAPP_N" "abc") [ "--n"; "3" ] c |> Result.defaultWith (fun _ -> failwith "x")
    Assert.Equal(Ok(Some 3), Matches.tryInt "n" m)

let private minPos (req: bool) (lo: int) (hi: int) =
    let a = Arg.create "p" |> Arg.positional 1 |> Arg.numArgs lo hi
    Command.create "c" |> Command.arg (if req then Arg.required a else a)

[<Fact>]
let ``positional numArgs min enforced when too few values`` () =
    match Command.tryParse [ "a" ] (minPos false 2 4) with
    | Error(TooFewValues(_, _)) -> ()
    | r -> failwithf "expected TooFewValues, got %b" r.IsOk

[<Fact>]
let ``positional numArgs min satisfied`` () =
    Assert.True((Command.tryParse [ "a"; "b" ] (minPos false 2 4)).IsOk)

[<Fact>]
let ``optional positional with min and no values is ok`` () =
    Assert.True((Command.tryParse [] (minPos false 2 4)).IsOk)

[<Fact>]
let ``required positional with min and no values is missing required`` () =
    match Command.tryParse [] (minPos true 2 4) with
    | Error(MissingRequired(_, _)) -> ()
    | r -> failwithf "expected MissingRequired, got %b" r.IsOk

[<Fact>]
let ``optional positional numArgs 0 N with no values is ok`` () =
    Assert.True((Command.tryParse [] (minPos false 0 3)).IsOk)

[<Fact>]
let ``positional with default and min 2 given nothing is ok`` () =
    let c =
        Command.create "c"
        |> Command.arg (Arg.create "p" |> Arg.positional 1 |> Arg.numArgs 2 4 |> Arg.defaultValue "x")
    Assert.True((Command.tryParse [] c).IsOk)

// ---- allowHyphenValues / trailingVarArg ----

let private optCmd hyphen =
    let o = Arg.create "opt" |> Arg.long "opt"
    Command.create "c"
    |> Command.arg (if hyphen then Arg.allowHyphenValues o else o)
    |> Command.arg (Arg.create "x" |> Arg.short 'x' |> Arg.flag)

let private parseOk argv c =
    match Command.tryParse argv c with
    | Ok m -> m
    | Error e -> failwithf "unexpected error: %s" (ParseError.render e)

[<Fact>]
let ``allowHyphenValues option takes short-looking value`` () =
    Assert.Equal(Some "-x", Matches.tryString "opt" (parseOk [ "--opt"; "-x" ] (optCmd true)))

[<Fact>]
let ``allowHyphenValues option takes long-looking value`` () =
    Assert.Equal(Some "--not-a-flag", Matches.tryString "opt" (parseOk [ "--opt"; "--not-a-flag" ] (optCmd true)))

[<Fact>]
let ``without allowHyphenValues option still errors`` () =
    match Command.tryParse [ "--opt"; "-x" ] (optCmd false) with
    | Error(MissingValue(_, _)) -> ()
    | _ -> failwith "expected MissingValue"

let private posCmd =
    Command.create "c"
    |> Command.arg (Arg.create "x" |> Arg.short 'x' |> Arg.flag)
    |> Command.arg (Arg.create "p" |> Arg.positional 1 |> Arg.allowHyphenValues)

[<Fact>]
let ``allowHyphenValues positional accepts unknown dash token`` () =
    Assert.Equal(Some "-foo", Matches.tryString "p" (parseOk [ "-foo" ] posCmd))
    Assert.Equal(Some "--bar", Matches.tryString "p" (parseOk [ "--bar" ] posCmd))

[<Fact>]
let ``allowHyphenValues positional still recognises known flags`` () =
    let m = parseOk [ "-x"; "v" ] posCmd
    Assert.True(Matches.flag "x" m)
    Assert.Equal(Some "v", Matches.tryString "p" m)

let private trailCmd =
    Command.create "tool"
    |> Command.version "1.0"
    |> Command.arg (Arg.create "verbose" |> Arg.long "verbose" |> Arg.flag)
    |> Command.arg (Arg.create "cmd" |> Arg.positional 1 |> Arg.manyValues |> Arg.trailingVarArg)

[<Fact>]
let ``trailingVarArg takes everything after first value`` () =
    let m = parseOk [ "--verbose"; "run"; "--flag"; "-x" ] trailCmd
    Assert.True(Matches.flag "verbose" m)
    Assert.Equal<string list>([ "run"; "--flag"; "-x" ], Matches.getMany "cmd" m)
    let m2 = parseOk [ "run"; "--verbose" ] trailCmd
    Assert.False(Matches.flag "verbose" m2)
    Assert.Equal<string list>([ "run"; "--verbose" ], Matches.getMany "cmd" m2)

[<Fact>]
let ``trailingVarArg swallows help and version`` () =
    let m = parseOk [ "run"; "--help"; "-h"; "--version"; "help" ] trailCmd
    Assert.Equal<string list>([ "run"; "--help"; "-h"; "--version"; "help" ], Matches.getMany "cmd" m)

[<Fact>]
let ``trailingVarArg help before first value still shows help`` () =
    match Command.tryParse [ "--help" ] trailCmd with
    | Error(DisplayHelp _) -> ()
    | _ -> failwith "expected help"

[<Fact>]
let ``trailingVarArg double dash before first value is separator`` () =
    let m = parseOk [ "--"; "--verbose"; "a" ] trailCmd
    Assert.False(Matches.flag "verbose" m)
    Assert.Equal<string list>([ "--verbose"; "a" ], Matches.getMany "cmd" m)

[<Fact>]
let ``trailingVarArg double dash after first value is kept`` () =
    let m = parseOk [ "run"; "--"; "x" ] trailCmd
    Assert.Equal<string list>([ "run"; "--"; "x" ], Matches.getMany "cmd" m)

[<Fact>]
let ``trailingVarArg with allowHyphenValues takes a known flag as its first value`` () =
    let c =
        Command.create "tool"
        |> Command.arg (Arg.create "verbose" |> Arg.long "verbose" |> Arg.flag)
        |> Command.arg (Arg.create "cmd" |> Arg.positional 1 |> Arg.manyValues |> Arg.trailingVarArg |> Arg.allowHyphenValues)
    let m = parseOk [ "--verbose"; "x" ] c
    Assert.False(Matches.flag "verbose" m)
    Assert.Equal<string list>([ "--verbose"; "x" ], Matches.getMany "cmd" m)
    // without allowHyphenValues the same token is still parsed as the known flag
    let m2 = parseOk [ "--verbose"; "x" ] trailCmd
    Assert.True(Matches.flag "verbose" m2)
    Assert.Equal<string list>([ "x" ], Matches.getMany "cmd" m2)

[<Fact>]
let ``trailingVarArg rejected on non-last positional`` () =
    let c =
        Command.create "c"
        |> Command.arg (Arg.create "a" |> Arg.positional 1 |> Arg.trailingVarArg)
        |> Command.arg (Arg.create "b" |> Arg.positional 2)
    Assert.NotEmpty(Command.check c)

[<Fact>]
let ``trailingVarArg rejected on single-value positional`` () =
    let a = { (Arg.create "a" |> Arg.positional 1) with TrailingVarArg = true }
    Assert.NotEmpty(Command.check (Command.create "c" |> Command.arg a))

// ---- help customization ----

let private helpText argv cmd =
    match Command.tryParse argv cmd with
    | Error(DisplayHelp t) -> t
    | _ -> failwith "expected help"

let private baseHelpCmd =
    Command.create "h"
    |> Command.about "short about"
    |> Command.arg (Arg.create "plain" |> Arg.long "plain" |> Arg.help "Plain opt")
    |> Command.arg (Arg.create "net" |> Arg.long "net" |> Arg.help "Net opt")

[<Fact>]
let ``longAbout shown only for --help`` () =
    let c = baseHelpCmd |> Command.longAbout "much longer about"
    let l = helpText [ "--help" ] c
    let s = helpText [ "-h" ] c
    Assert.Contains("much longer about", l)
    Assert.DoesNotContain("short about", l)
    Assert.Contains("short about", s)
    Assert.DoesNotContain("much longer about", s)

[<Fact>]
let ``longAbout falls back to about`` () =
    Assert.Contains("short about", helpText [ "--help" ] baseHelpCmd)

[<Fact>]
let ``beforeHelp above and afterHelp below`` () =
    let c = baseHelpCmd |> Command.beforeHelp "BEFORE" |> Command.afterHelp "AFTER"
    let t = helpText [ "-h" ] c
    Assert.StartsWith("BEFORE\n", t)
    Assert.True(t.IndexOf "BEFORE" < t.IndexOf "Usage:")
    Assert.EndsWith("\n\nAFTER\n", t)
    Assert.True(t.IndexOf "Options:" < t.IndexOf "AFTER")

[<Fact>]
let ``helpHeading creates custom section`` () =
    let c =
        baseHelpCmd
        |> Command.arg (Arg.create "host" |> Arg.long "host" |> Arg.help "Host" |> Arg.helpHeading "Network")
        |> Command.arg (Arg.create "port" |> Arg.long "port" |> Arg.helpHeading "Network")
        |> Command.arg (Arg.create "secret" |> Arg.long "secret" |> Arg.helpHeading "Network" |> Arg.hidden)
    let t = helpText [ "-h" ] c
    let net = t.IndexOf "Network:"
    let opts = t.IndexOf "Options:"
    Assert.True(net >= 0 && opts >= 0)
    let netSection, optSection =
        if net < opts then t.Substring(net, opts - net), t.Substring opts
        else t.Substring net, t.Substring(opts, net - opts)
    Assert.Contains("--host", netSection)
    Assert.Contains("--port", netSection)
    Assert.DoesNotContain("--plain", netSection)
    Assert.Contains("--plain", optSection)
    Assert.DoesNotContain("--host", optSection)
    Assert.DoesNotContain("--port", optSection)
    Assert.DoesNotContain("--secret", t)

[<Fact>]
let ``unchanged help without new fields`` () =
    let plain = helpText [ "--help" ] baseHelpCmd
    let withNone = helpText [ "--help" ] (baseHelpCmd |> fun c -> { c with LongAbout = None; BeforeHelp = None; AfterHelp = None })
    Assert.Equal(plain, withNone)
    Assert.Equal(
        "short about\n\nUsage: h [OPTIONS]\n\nOptions:\n      --plain <PLAIN>  Plain opt\n      --net <NET>      Net opt\n  -h, --help           Print help (see a summary with '-h')\n",
        plain)

// ---- subcommandNegatesRequired ---------------------------------------------------------------

let private withSubcommand (negate: bool) =
    Command.create "convert"
    |> Command.arg (Arg.create "input" |> Arg.positional 1 |> Arg.required)
    |> Command.arg (Arg.create "format" |> Arg.long "format" |> Arg.required)
    |> Command.arg (Arg.create "a" |> Arg.long "a" |> Arg.group "pick")
    |> Command.group "pick" [ "a" ] true false
    |> Command.subcommand (
        Command.create "completions"
        |> Command.arg (Arg.create "shell" |> Arg.positional 1 |> Arg.required)
    )
    |> (if negate then Command.subcommandNegatesRequired else id)

[<Fact>]
let ``without the setting a required parent arg blocks the subcommand`` () =
    match Command.tryParse [ "completions"; "bash" ] (withSubcommand false) with
    | Error(MissingRequired _) -> ()
    | other -> failwithf "expected MissingRequired, got %A" (other |> Result.map ignore)

[<Fact>]
let ``with the setting a subcommand does not need the parent's required args or groups`` () =
    match Command.tryParse [ "completions"; "bash" ] (withSubcommand true) with
    | Ok m ->
        let sm = Matches.trySubcommand "completions" m
        Assert.True(sm.IsSome)
        Assert.Equal("bash", Matches.getString "shell" sm.Value)
    | Error e -> failwithf "unexpected error: %s" (ParseError.render e)

[<Fact>]
let ``with the setting the parent still requires its args when no subcommand is given`` () =
    match Command.tryParse [] (withSubcommand true) with
    | Error(MissingRequired _) -> ()
    | _ -> failwith "expected MissingRequired"

[<Fact>]
let ``with the setting the subcommand's own required args are still enforced`` () =
    match Command.tryParse [ "completions" ] (withSubcommand true) with
    | Error(MissingRequired(args, _)) -> Assert.Equal<string list>([ "<SHELL>" ], args)
    | _ -> failwith "expected the subcommand's <SHELL> to be required"
