module Flap.Tests.CompleteTests

open Xunit
open Flap

let private projects (w: string) =
    [ "alpha"; "alpine"; "beta" ]
    |> List.filter (fun p -> p.StartsWith(w, System.StringComparison.Ordinal))
    |> List.map (fun p -> { Value = p; Help = Some("project " + p) })

let app =
    Command.create "my-tool"
    |> Command.arg (Arg.create "mode" |> Arg.short 'm' |> Arg.long "mode" |> Arg.possibleValues [ "fast"; "slow" ])
    |> Command.arg (Arg.create "verbose" |> Arg.short 'v' |> Arg.long "verbose" |> Arg.action SetTrue)
    |> Command.arg (Arg.create "level" |> Arg.long "level" |> Arg.global' |> Arg.possibleValues [ "low"; "high" ])
    |> Command.arg (Arg.create "secret" |> Arg.long "secret-opt" |> Arg.hidden |> Arg.possibleValues [ "x" ])
    |> Command.subcommand (
        Command.create "deploy"
        |> Command.alias "ship"
        |> Command.arg (Arg.create "project" |> Arg.long "project" |> Arg.completer projects)
        |> Command.arg (Arg.create "target" |> Arg.positional 1 |> Arg.completerValues (fun _ -> [ "prod"; "stage" ]))
    )
    |> Command.subcommand (Command.create "status")
    |> Command.subcommand (Command.create "internal" |> Command.hidden)

let private values (argv: string list) =
    Complete.candidates argv app |> List.map (fun c -> c.Value)

[<Fact>]
let ``envVarName is per app, upper-cased, non-alphanumerics become underscores`` () =
    Assert.Equal("MY_TOOL_COMPLETE", Complete.envVarName app)
    Assert.Equal("X_COMPLETE", Complete.envVarName (Command.create "x"))

[<Fact>]
let ``empty argv completes visible subcommands, not hidden ones`` () =
    let v = values [ "" ]
    Assert.Contains("deploy", v)
    Assert.Contains("status", v)
    Assert.DoesNotContain("internal", v)

[<Fact>]
let ``subcommand names and aliases are filtered by prefix`` () =
    Assert.Equal<string list>([ "status" ], values [ "st" ])
    Assert.Equal<string list>([ "ship" ], values [ "sh" ])

[<Fact>]
let ``dash completes flags, never hidden ones`` () =
    let v = values [ "-" ]
    Assert.Contains("--mode", v)
    Assert.Contains("-m", v)
    Assert.Contains("--verbose", v)
    Assert.Contains("--level", v)
    Assert.DoesNotContain("--secret-opt", v)

[<Fact>]
let ``long flag prefix filters`` () =
    Assert.Equal<string list>([ "--mode" ], values [ "--mo" ])

[<Fact>]
let ``option value completes static values`` () =
    Assert.Equal<string list>([ "fast"; "slow" ], values [ "--mode"; "" ])
    Assert.Equal<string list>([ "slow" ], values [ "-m"; "s" ])

[<Fact>]
let ``option=value completes the value part`` () =
    Assert.Equal<string list>([ "fast" ], values [ "--mode=f" ])

[<Fact>]
let ``completer receives the partial word and keeps its help text`` () =
    let cs = Complete.candidates [ "deploy"; "--project"; "al" ] app
    Assert.Equal<Candidate list>(
        [ { Value = "alpha"; Help = Some "project alpha" }; { Value = "alpine"; Help = Some "project alpine" } ],
        cs
    )

[<Fact>]
let ``subcommand alias resolves to the same context`` () =
    Assert.Equal<string list>([ "alpha"; "alpine"; "beta" ], values [ "ship"; "--project=" ])

[<Fact>]
let ``positional completer is used for its slot`` () =
    Assert.Equal<string list>([ "prod"; "stage" ], values [ "deploy"; "" ])

[<Fact>]
let ``positional slot advances after a value is given`` () =
    Assert.Equal<string list>([], values [ "deploy"; "prod"; "" ])

[<Fact>]
let ``a consumed option value does not count as a positional`` () =
    Assert.Equal<string list>([ "prod"; "stage" ], values [ "deploy"; "--project"; "alpha"; "" ])

[<Fact>]
let ``global option is available inside a subcommand`` () =
    Assert.Equal<string list>([ "low"; "high" ], values [ "deploy"; "--level"; "" ])
    Assert.Contains("--level", values [ "deploy"; "-" ])

[<Fact>]
let ``after double dash everything is positional`` () =
    Assert.Equal<string list>([], values [ "--"; "-" ])
    Assert.Equal<string list>([], values [ "deploy"; "--"; "prod"; "" ])

[<Fact>]
let ``unknown input never throws and yields nothing`` () =
    Assert.Equal<string list>([], values [ "nope"; "--bogus="; ])
    Assert.Equal<string list>([], values [ "--bogus"; "x"; "y" ])

[<Fact>]
let ``empty argv is treated as completing an empty word`` () =
    Assert.Equal<string list>(values [ "" ], values [])

[<Fact>]
let ``render bash is one value per line`` () =
    let cs = [ { Value = "a"; Help = Some "ignored" }; { Value = "b"; Help = None } ]
    Assert.Equal("a\nb\n", Complete.render Bash cs)

[<Fact>]
let ``render other shells is value TAB help with the column always present`` () =
    let cs = [ { Value = "a"; Help = Some "first" }; { Value = "b"; Help = None } ]
    for sh in [ Zsh; Fish; PowerShell; Nushell ] do
        Assert.Equal("a\tfirst\nb\t\n", Complete.render sh cs)

[<Fact>]
let ``render collapses tabs and newlines so each candidate stays on one line`` () =
    let cs = [ { Value = "a\tb"; Help = Some "x\ny\r\nz" } ]
    Assert.Equal("a b\tx y  z\n", Complete.render Zsh cs)
