<p align="center">
  <img src="docs/images/flap-logo.svg" alt="flap" width="280">
</p>

<h3 align="center">A clap-style, pipe-friendly CLI parser for F#.</h3>

<p align="center">
  Flap is a <a href="https://github.com/clap-rs/clap">clap</a>-style builder API for F#: modules and piping, no attributes, no reflection.
  Describe your commands with <code>|&gt;</code>, parse, and read typed values from the matches.
  Native AOT and Fable compatible.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/Flap"><img src="https://img.shields.io/nuget/vpre/Flap?logo=nuget&label=Flap" alt="NuGet Flap"></a>
  <a href="https://www.nuget.org/packages/Flap"><img src="https://img.shields.io/nuget/dt/Flap?logo=nuget" alt="NuGet downloads"></a>
  <a href="https://github.com/dburriss/flap/blob/main/LICENSE"><img src="https://img.shields.io/github/license/dburriss/flap" alt="License"></a>
</p>

## What it does

- **Flags, counts, options and positionals.** Short and long forms, repeated flags as counts, multi-value args. See the [API reference](docs/reference/api.md).
- **Subcommands.** Nested commands, global args, aliases and external subcommands. See [Exec-style wrappers](docs/how-to/exec-style-wrappers.md).
- **Validation.** Defaults, env vars, possible values, validators, conflicts, requires and groups.
- **Help and version.** Generated `--help`/`--version`, did-you-mean suggestions and room to customize. See [Customize help](docs/how-to/customize-help.md).
- **Shell completions.** Bash, Zsh, Fish, PowerShell and Nushell, value-aware. See [Shell completions](docs/how-to/shell-completions.md).
- **Pure core.** `Command.tryParse*` returns a `Result` with no console I/O; only `Command.parse` prints and exits.
- **Native AOT and Fable.** Reflection-free, so it publishes as Native AOT and runs under Fable. See [AOT and Fable](docs/how-to/aot-and-fable.md).

## Get started

Add the [`Flap`](https://www.nuget.org/packages/Flap) package to an F# console project (beta, hence `--prerelease`; .NET 10 SDK required, and the library also targets `netstandard2.1`):

```bash
dotnet add package Flap --prerelease
```

Then define a command and parse:

```fsharp
open Flap

[<EntryPoint>]
let main argv =
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
    0
```

The [getting started tutorial](docs/tutorials/getting-started.md) walks through building a complete CLI.

## Documentation

Organised by [Diataxis](https://diataxis.fr/), starting at the [docs index](docs/index.md):

| I want to... | Go to |
| --- | --- |
| Learn Flap by doing | [Getting started](docs/tutorials/getting-started.md) |
| Get a specific job done | [AOT and Fable](docs/how-to/aot-and-fable.md), [Shell completions](docs/how-to/shell-completions.md), [Customize help](docs/how-to/customize-help.md), [Exec-style wrappers](docs/how-to/exec-style-wrappers.md) |
| Look up a function or type | [API reference](docs/reference/api.md), [Completions reference](docs/reference/completions.md) |
| Understand why it works this way | [Design](docs/explanation/design.md) |

More: [changelog](CHANGELOG.md) · [contributing](AGENTS.md)

## License

[MIT](LICENSE)
