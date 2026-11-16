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

- `Command.tryParse` / `Command.tryParseWith` are pure and return `Result<Matches, ParseError>` (use these under Fable).
- `Command.parseWith` takes stdout/stderr/exit callbacks; `Command.parse` uses the console (not under Fable).
- Typed values use explicit parsers (`Value.int`, `Value.oneOf`, ...) via `Matches.tryGetWith`.
- `Matches.tryInt` / `tryBool` / `tryFloat` return `Result<'T option, string>` (`Ok None` if absent, `Error` if the value
  does not parse); `getInt` / `getBool` / `getFloat` are the required variants returning `Result<'T, string>`:
  `match Matches.tryInt "times" m with Ok(Some n) -> ... | Ok None -> ... | Error e -> eprintfn "%s" e`.
- Supports flags, counts, options, append/multi-value, delimiters, positionals, subcommands, global args, env vars,
  defaults, possible values, validators, conflicts/requires, groups, external subcommands, help/version,
  did-you-mean suggestions and completions (`Completions.generate Bash|Zsh|Fish|PowerShell|Nushell cmd`).
- Nushell completions (`Completions.generate Nushell cmd`) emit `export extern` definitions per command level (and alias), with
  `def "nu-complete ..."` completers for possible values and `path`/`directory` typing for value hints.
- `Arg.allowHyphenValues` lets an option/positional take a value starting with `-`; `Arg.trailingVarArg` (last, multi-value
  positional) takes every token after its first value verbatim, for exec-style wrappers like `tool run cmd --flag`.
- Help customization: `Command.longAbout` (shown for `--help` only), `Command.beforeHelp` / `Command.afterHelp` (text above/below the
  help sections, e.g. examples) and `Arg.helpHeading` (groups options under a custom section instead of `Options:`).
- Completions are value-aware: `Arg.possibleValues` complete after an option (`--mode <TAB>`, `--mode=<TAB>`) or at a
  positional's slot, and `Arg.valueHint FilePath|DirPath|AnyPath` completes paths with each shell's native mechanism
  (`Other`/unset: no path completion). Values are scoped to the subcommand (and inherited global args) they belong to.
- AOT: reference with `--reflectionfree` in your app (see `samples/Flap.Sample`) to avoid F# `%A` reflection in generated `ToString`.

## Development

```
dotnet test tests/Flap.Tests
dotnet publish samples/Flap.Sample -c Release -r osx-arm64     # Native AOT
cd samples/Flap.Fable && dotnet fable --lang js                 # Fable
```
