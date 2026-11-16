# How to wrap another command (`tool run cmd --flag`)

Use `Arg.trailingVarArg` on the last positional so everything after its first value is passed through untouched.

```fsharp
let run =
    Command.create "run"
    |> Command.arg (Arg.create "cmd" |> Arg.positional 1 |> Arg.required |> Arg.trailingVarArg)

match Command.tryParse [ "run"; "git"; "log"; "--oneline"; "-n"; "3" ] cmd with
| Ok m ->
    // cmd here is the root command with `run` as a subcommand
    let sub = Matches.trySubcommand "run" m
    sub |> Option.map (Matches.getMany "cmd")   // Some [ "git"; "log"; "--oneline"; "-n"; "3" ]
| Error e -> eprintfn "%s" (ParseError.render e)
```

After the first value, no flag, subcommand, help or version handling happens: `--oneline` and `-n` are values.

## Let an option or positional take a value starting with `-`

```fsharp
Arg.create "offset" |> Arg.long "offset" |> Arg.allowHyphenValues
```

`--offset -5` then yields `-5`. For a positional, a dash-leading token is a value unless it is a known flag.

## Accept unknown subcommands

For `git`-style external subcommands, use `Command.allowExternalSubcommands` and read the tokens with `Matches.externalArgs`.

## Constraints

`Command.check` rejects `trailingVarArg` on any argument that is not the last positional.
