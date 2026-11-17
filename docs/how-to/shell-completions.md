# How to ship shell completions

Flap generates completion scripts for Bash, Zsh, Fish, PowerShell and Nushell from your command definition. This page uses a small tool called `convert`:

```sh
convert photo.png --format webp --out ./images
```

There are two separate jobs:

1. **In your command definition:** say what can be completed (part 1).
2. **In the shell:** print the script and load it (parts 2 and 3).

## Part 1: choose what gets completed

Subcommands, flags and options are completed automatically. For the values after them, add the following to the `Arg` definitions:

```fsharp
let cmd =
    Command.create "convert"
    |> Command.arg (Arg.create "input" |> Arg.positional 1 |> Arg.valueHint FilePath)
    |> Command.arg (Arg.create "format" |> Arg.long "format" |> Arg.possibleValues [ "png"; "jpg"; "webp" ])
    |> Command.arg (Arg.create "out" |> Arg.long "out" |> Arg.valueHint DirPath)
```

With that definition:

| The user types | The shell offers |
|---|---|
| `convert --format <TAB>` | `png`, `jpg`, `webp` |
| `convert --format=<TAB>` | `png`, `jpg`, `webp` |
| `convert --out <TAB>` | directories |
| `convert <TAB>` | files (the `input` positional) |

- **Fixed choices:** `Arg.possibleValues` completes after `--format <TAB>`, `--format=<TAB>` and at a positional's slot.
- **Paths:** `Arg.valueHint` takes `FilePath`, `DirPath` or `AnyPath`.
- **Values computed at runtime:** see [Offer values that are only known at runtime](dynamic-completions.md).

## Part 2: print the script

Generate the script with `Completions.generate` and print it, for example from a `completions` subcommand. The `shell` argument here chooses which shell to generate for; it has nothing to do with the values in part 1.

```fsharp
let shells =
    [ "bash", Bash; "zsh", Zsh; "fish", Fish; "powershell", PowerShell; "nushell", Nushell ]

let cmd =
    Command.create "convert"
    // ... the arguments from part 1 ...
    |> Command.subcommand (
        Command.create "completions"
        |> Command.arg (
            Arg.create "shell" |> Arg.positional 1 |> Arg.required
            |> Arg.possibleValues (List.map fst shells))) // completes the shell names

match Matches.trySubcommand "completions" m with
| Some sm ->
    match Matches.getWith (Value.oneOf shells) "shell" sm with
    | Ok shell -> printfn "%s" (Completions.generate shell cmd)
    | Error e -> eprintfn "%s" e
| None -> ()
```

## Part 3: load the script in the shell

Run the matching line in your shell, and add it to your shell's startup file to keep it.

| Shell | Command |
|---|---|
| Bash | `eval "$(convert completions bash)"` |
| Zsh | `source <(convert completions zsh)` (after `compinit`) |
| Fish | `convert completions fish \| source` |
| PowerShell | `convert completions powershell \| Out-String \| Invoke-Expression` |
| Nushell | `convert completions nushell \| save -f convert-completions.nu`, then `source convert-completions.nu` |

In Bash, use `eval` rather than `source <(...)`: the system Bash 3.2 on macOS does not load the script with `source <(...)`.

Nushell's `source` needs a file that exists before the script runs, which is why it takes two steps.

## Test a generated script

`mise.toml` pins nushell, powershell and fish so you can load the output in each shell.

See [Completions reference](../reference/completions.md) for what each shell receives.
