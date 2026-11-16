module Flap.Tests.CompletionTests

open Xunit
open Flap

let compApp =
    Command.create "app"
    |> Command.arg (Arg.create "mode" |> Arg.short 'm' |> Arg.long "mode" |> Arg.possibleValues [ "fast"; "slow" ])
    |> Command.arg (Arg.create "out" |> Arg.long "out" |> Arg.valueHint FilePath)
    |> Command.arg (Arg.create "dir" |> Arg.long "dir" |> Arg.valueHint DirPath)
    |> Command.arg (Arg.create "note" |> Arg.long "note" |> Arg.valueHint Other)
    |> Command.arg (Arg.create "secret" |> Arg.long "secret-opt" |> Arg.hidden |> Arg.possibleValues [ "hiddenval" ])
    |> Command.arg (Arg.create "level" |> Arg.long "level" |> Arg.global' |> Arg.possibleValues [ "low"; "high" ])
    |> Command.arg (Arg.create "quote" |> Arg.long "quote" |> Arg.possibleValues [ "it's"; "a b" ])
    |> Command.arg (Arg.create "target" |> Arg.positional 1 |> Arg.possibleValues [ "alpha"; "beta" ])
    |> Command.arg (Arg.create "src" |> Arg.positional 2 |> Arg.valueHint FilePath)
    |> Command.subcommand (
        Command.create "deploy"
        |> Command.arg (Arg.create "env" |> Arg.long "env" |> Arg.possibleValues [ "prod"; "staging" ])
        |> Command.arg (Arg.create "where" |> Arg.positional 1 |> Arg.valueHint DirPath)
    )

let gen sh = Completions.generate sh compApp

let lines (s: string) = s.Split('\n') |> List.ofArray

let assertLine (pred: string -> bool) (s: string) msg =
    Assert.True(lines s |> List.exists pred, msg + "\n---\n" + s)

let hasAll (frags: string list) (l: string) = frags |> List.forall (fun (f: string) -> l.Contains f)

[<Fact>]
let ``valueHint defaults to None and can be set`` () =
    Assert.Equal(None, (Arg.create "x").ValueHint)
    Assert.Equal(Some DirPath, (Arg.create "x" |> Arg.valueHint DirPath).ValueHint)

[<Fact>]
let ``hidden args stay out of completions for every shell`` () =
    for sh in [ Bash; Zsh; Fish; PowerShell ] do
        let s = gen sh
        Assert.DoesNotContain("secret-opt", s)
        Assert.DoesNotContain("hiddenval", s)

[<Fact>]
let ``bash completes option values, positional values, paths and scopes subcommands`` () =
    let s = gen Bash
    assertLine (hasAll [ "'app,--mode'"; "'app,-m'"; "vals=('fast' 'slow')" ]) s "option values"
    assertLine (hasAll [ "'app,--out'"; "hint=f" ]) s "file hint"
    assertLine (hasAll [ "'app,--dir'"; "hint=d" ]) s "dir hint"
    Assert.Contains("compgen -f -- \"$cur\"", s)
    Assert.Contains("compgen -d -- \"$cur\"", s)
    Assert.Contains("compopt -o filenames", s)
    assertLine (hasAll [ "pos == 0"; "vals=('alpha' 'beta')" ]) s "positional 1 values"
    assertLine (hasAll [ "pos == 1"; "hint=f" ]) s "positional 2 file hint"
    assertLine (hasAll [ "'app__deploy,--env'"; "vals=('prod' 'staging')" ]) s "subcommand option values"
    Assert.DoesNotContain("'app,--env'", s)
    assertLine (hasAll [ "'app__deploy,--level'"; "vals=('low' 'high')" ]) s "inherited global"
    Assert.Contains("vals=('it'\\''s' 'a b')", s)
    Assert.False(lines s |> List.exists (hasAll [ "'app,--note'"; "hint=" ]), "Other hint must not complete paths")

[<Fact>]
let ``zsh completes option values, positional values, paths and scopes subcommands`` () =
    let s = gen Zsh
    assertLine (hasAll [ "'app,--mode'"; "'app,-m'"; "vals=('fast' 'slow')" ]) s "option values"
    assertLine (hasAll [ "'app,--out'"; "hint=f" ]) s "file hint"
    assertLine (hasAll [ "'app,--dir'"; "hint=d" ]) s "dir hint"
    Assert.Contains("f) _files ;;", s)
    Assert.Contains("d) _files -/ ;;", s)
    Assert.Contains("compset -P '*='", s)
    assertLine (hasAll [ "pos == 0"; "vals=('alpha' 'beta')" ]) s "positional values"
    assertLine (hasAll [ "'app__deploy,--env'"; "vals=('prod' 'staging')" ]) s "subcommand option values"
    Assert.DoesNotContain("'app,--env'", s)
    assertLine (hasAll [ "'app__deploy,--level'"; "vals=('low' 'high')" ]) s "inherited global"
    Assert.Contains("vals=('it'\\''s' 'a b')", s)

[<Fact>]
let ``fish completes option values, positional values, paths and scopes subcommands`` () =
    let s = gen Fish
    assertLine (hasAll [ "'__app_flap_ctx app'"; "-s m -l mode"; "-r -f -a '(printf \"%s\\\\n\" \"fast\" \"slow\")'" ]) s "option values"
    assertLine (hasAll [ "-l out"; "-r -F" ]) s "file hint"
    assertLine (hasAll [ "-l dir"; "__fish_complete_directories" ]) s "dir hint"
    assertLine (fun l -> l.Contains "-l note" && not (l.Contains "-F") && not (l.Contains "__fish_complete_directories")) s "other hint"
    assertLine (hasAll [ "'__app_flap_ctx app 0 0'"; "\"alpha\" \"beta\"" ]) s "positional values"
    assertLine (hasAll [ "'__app_flap_ctx app 1 1'"; "-F" ]) s "positional file"
    assertLine (hasAll [ "'__app_flap_ctx app__deploy 0 0'"; "__fish_complete_directories" ]) s "sub positional dir"
    assertLine (hasAll [ "'__app_flap_ctx app__deploy'"; "-l env"; "\"prod\" \"staging\"" ]) s "subcommand option"
    assertLine (hasAll [ "'__app_flap_ctx app__deploy'"; "-l level" ]) s "inherited global"
    Assert.False(lines s |> List.exists (hasAll [ "'__app_flap_ctx app'"; "-l env" ]))
    Assert.Contains("\"it\\'s\" \"a b\"", s)

[<Fact>]
let ``powershell completes option values, positional values, paths and scopes subcommands`` () =
    let s = gen PowerShell
    assertLine (hasAll [ "'app,--mode'"; "$vals = @('fast', 'slow')" ]) s "option values"
    assertLine (hasAll [ "'app,-m'"; "$vals = @('fast', 'slow')" ]) s "short option values"
    assertLine (hasAll [ "'app,--out'"; "$hint = 'f'" ]) s "file hint"
    assertLine (hasAll [ "'app,--dir'"; "$hint = 'd'" ]) s "dir hint"
    Assert.Contains("Get-ChildItem", s)
    Assert.Contains("-Directory", s)
    assertLine (hasAll [ "$pos -eq 0"; "$vals = @('alpha', 'beta')" ]) s "positional values"
    assertLine (hasAll [ "'app__deploy,--env'"; "$vals = @('prod', 'staging')" ]) s "subcommand option values"
    Assert.DoesNotContain("'app,--env'", s)
    assertLine (hasAll [ "'app__deploy,--level'"; "$vals = @('low', 'high')" ]) s "inherited global"
    Assert.Contains("@('it''s', 'a b')", s)
