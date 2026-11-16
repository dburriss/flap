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

let nuApp =
    Command.create "myapp"
    |> Command.about "Does things"
    |> Command.version "1.0"
    |> Command.arg (Arg.create "mode" |> Arg.short 'm' |> Arg.long "mode" |> Arg.help "Run mode" |> Arg.possibleValues [ "fast"; "slow" ])
    |> Command.arg (Arg.create "verbose" |> Arg.short 'v' |> Arg.long "verbose" |> Arg.action Count)
    |> Command.arg (Arg.create "force" |> Arg.long "force" |> Arg.action SetTrue)
    |> Command.arg (Arg.create "x" |> Arg.short 'x')
    |> Command.arg (Arg.create "out" |> Arg.long "out" |> Arg.valueHint FilePath)
    |> Command.arg (Arg.create "dir" |> Arg.long "dir" |> Arg.valueHint DirPath)
    |> Command.arg (Arg.create "any" |> Arg.long "any" |> Arg.valueHint AnyPath)
    |> Command.arg (Arg.create "note" |> Arg.long "note" |> Arg.valueHint Other)
    |> Command.arg (Arg.create "quote" |> Arg.long "quote" |> Arg.help "say \"hi\"\nsecond" |> Arg.possibleValues [ "it's"; "a \"b\""; "back\\slash" ])
    |> Command.arg (Arg.create "secret" |> Arg.long "secret-opt" |> Arg.hidden |> Arg.possibleValues [ "hiddenval" ])
    |> Command.arg (Arg.create "level" |> Arg.long "level" |> Arg.global' |> Arg.possibleValues [ "low"; "high" ])
    |> Command.arg (Arg.create "name" |> Arg.positional 1 |> Arg.required)
    |> Command.arg (Arg.create "target" |> Arg.positional 2 |> Arg.possibleValues [ "alpha"; "beta" ])
    |> Command.arg (Arg.create "files" |> Arg.positional 3 |> Arg.valueHint FilePath |> Arg.numArgs 1 5)
    |> Command.subcommand (
        Command.create "build"
        |> Command.about "Build it"
        |> Command.alias "b"
        |> Command.arg (Arg.create "env" |> Arg.long "env" |> Arg.possibleValues [ "prod"; "staging" ])
        |> Command.arg (Arg.create "where" |> Arg.positional 1 |> Arg.valueHint DirPath)
    )

let nu = Completions.generate Nushell nuApp

[<Fact>]
let ``nushell option possible values get a completer def and annotated param`` () =
    assertLine (hasAll [ "def \"nu-complete myapp mode\" []"; "\"fast\""; "\"slow\"" ]) nu "completer def"
    assertLine (hasAll [ "--mode (-m): string@\"nu-complete myapp mode\""; "# Run mode" ]) nu "annotated option with help"

[<Fact>]
let ``nushell path and directory hints and plain options`` () =
    assertLine (fun l -> l.Trim().StartsWith "--out: path") nu "file hint"
    assertLine (fun l -> l.Trim().StartsWith "--dir: directory") nu "dir hint"
    assertLine (fun l -> l.Trim().StartsWith "--any: path") nu "any path hint"
    assertLine (fun l -> l.Trim().StartsWith "--note: string") nu "other hint"
    assertLine (fun l -> l.Trim().StartsWith "-x: string") nu "short only"

[<Fact>]
let ``nushell boolean flags are untyped`` () =
    assertLine (fun l -> l.Trim().StartsWith "--verbose (-v)" && not (l.Contains ":")) nu "count flag"
    assertLine (fun l -> l.Trim().StartsWith "--force" && not (l.Contains ":")) nu "set-true flag"
    assertLine (fun l -> l.Trim().StartsWith "--help (-h)" && not (l.Contains ":")) nu "builtin help"
    assertLine (fun l -> l.Trim().StartsWith "--version (-V)" && not (l.Contains ":")) nu "builtin version"

[<Fact>]
let ``nushell positional required optional and rest forms`` () =
    assertLine (fun l -> l.Trim().StartsWith "name: string") nu "required"
    assertLine (fun l -> l.Trim().StartsWith "target?: string@\"nu-complete myapp target\"") nu "optional with completer"
    assertLine (fun l -> l.Trim().StartsWith "...files: path") nu "rest"
    assertLine (fun l -> l.Trim().StartsWith "where?: directory") nu "sub positional dir"

[<Fact>]
let ``nushell subcommand extern path alias and inherited global`` () =
    assertLine (fun l -> l.StartsWith "export extern \"myapp\" [") nu "root extern"
    assertLine (fun l -> l.StartsWith "export extern \"myapp build\" [") nu "sub extern"
    assertLine (fun l -> l.StartsWith "export extern \"myapp b\" [") nu "alias extern"
    assertLine (hasAll [ "def \"nu-complete myapp build env\""; "\"prod\"" ]) nu "sub completer"
    assertLine (hasAll [ "def \"nu-complete myapp build level\""; "\"low\"" ]) nu "inherited global completer"
    assertLine (fun l -> l = "# Build it") nu "command description"
    assertLine (fun l -> l = "# Does things") nu "root description"
    let sub = nu.Substring(nu.IndexOf "export extern \"myapp build\"")
    let subBlock = sub.Substring(0, sub.IndexOf "]")
    Assert.Contains("--level: string@\"nu-complete myapp build level\"", subBlock)
    Assert.DoesNotContain("--mode", subBlock)

[<Fact>]
let ``nushell escapes quotes and backslashes and keeps help on one line`` () =
    Assert.Contains("\"it's\"", nu)
    Assert.Contains("\"a \\\"b\\\"\"", nu)
    Assert.Contains("\"back\\\\slash\"", nu)
    assertLine (hasAll [ "--quote"; "# say \"hi\" second" ]) nu "single-line help"

[<Fact>]
let ``nushell hides hidden args`` () =
    Assert.DoesNotContain("secret-opt", nu)
    Assert.DoesNotContain("hiddenval", nu)

let private multilineApp =
    Command.create "app"
    |> Command.arg (Arg.create "mode" |> Arg.long "mode" |> Arg.help "say \"hi\"\nsecond line\r\nthird" |> Arg.possibleValues [ "fast"; "slow" ])
    |> Command.arg (Arg.create "plain" |> Arg.long "plain" |> Arg.help "one\ntwo")
    |> Command.subcommand (Command.create "deploy" |> Command.about "Deploy it\nsecond line")

[<Fact>]
let ``fish keeps multi-line help on one line`` () =
    let s = Completions.generate Fish multilineApp
    // fish treats every output line as a candidate, so a raw newline inside a description leaks
    // its continuation line as a bogus completion
    let structural (l: string) =
        l = "" || l.StartsWith "complete " || l.StartsWith "function " || l.StartsWith "end" || l.StartsWith " "
    for l in lines s do
        Assert.True(structural l, "continuation line leaked: '" + l + "'\n---\n" + s)
    assertLine (hasAll [ "-l mode"; "-d 'say \"hi\" second line third'" ]) s "option help flattened"
    assertLine (hasAll [ "-l plain"; "-d 'one two'" ]) s "plain option help flattened"
    assertLine (hasAll [ "'deploy'"; "-d 'Deploy it second line'" ]) s "subcommand about flattened"
