# Flap

A [clap](https://github.com/clap-rs/clap)-style builder API for F#: modules and piping, no attributes, no reflection.
Native AOT and Fable compatible.

```fsharp
open Flap

let cmd =
    Command.create "myapp"
    |> Command.version "1.0"
    |> Command.about "Does things"
    |> Command.arg (Arg.create "name" |> Arg.short 'n' |> Arg.long "name" |> Arg.required)
    |> Command.arg (Arg.create "verbose" |> Arg.short 'v' |> Arg.count)
    |> Command.arg (Arg.create "mode" |> Arg.long "mode" |> Arg.possibleValues [ "fast"; "slow" ] |> Arg.defaultValue "fast")
    |> Command.subcommand (Command.create "build" |> Command.arg (Arg.create "release" |> Arg.long "release" |> Arg.flag))

let m = cmd |> Command.parse (List.ofArray argv)   // prints help/errors and exits like clap
let name = m |> Matches.getString "name"
let verbosity = m |> Matches.count "verbose"
```

Flags, counts, options, multi-value, positionals, subcommands, global args, env vars, defaults, possible values,
validators, conflicts/requires, groups, external subcommands, help/version, did-you-mean suggestions and shell
completions (Bash, Zsh, Fish, PowerShell, Nushell).

## Documentation

- **Learn:** [Getting started](docs/tutorials/getting-started.md)
- **Do:** [AOT and Fable](docs/how-to/aot-and-fable.md), [Shell completions](docs/how-to/shell-completions.md), [Customize help](docs/how-to/customize-help.md), [Exec-style wrappers](docs/how-to/exec-style-wrappers.md)
- **Look up:** [API reference](docs/reference/api.md), [Completions reference](docs/reference/completions.md)
- **Understand:** [Design](docs/explanation/design.md)

See the [docs index](docs/index.md) for everything in one place. Contributing: see [AGENTS.md](AGENTS.md).
