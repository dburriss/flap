# How to ship shell completions

Flap generates completion scripts for Bash, Zsh, Fish, PowerShell and Nushell from your command definition.

## Expose a way to print the script

Generate the script with `Completions.generate` and print it, for example from a `completions` subcommand:

```fsharp
let cmd =
    Command.create "myapp"
    |> Command.subcommand (
        Command.create "completions"
        |> Command.arg (
            Arg.create "shell" |> Arg.positional 1 |> Arg.required
            |> Arg.possibleValues [ "bash"; "zsh"; "fish"; "powershell"; "nushell" ]))

match Matches.trySubcommand "completions" m with
| Some sm ->
    let shell =
        match Matches.getString "shell" sm with
        | "bash" -> Bash
        | "zsh" -> Zsh
        | "fish" -> Fish
        | "powershell" -> PowerShell
        | _ -> Nushell
    printfn "%s" (Completions.generate shell cmd)
| None -> ()
```

## Complete values

- Fixed choices: `Arg.possibleValues [ "fast"; "slow" ]` completes after `--mode <TAB>`, `--mode=<TAB>` and at a positional's slot.
- Paths: `Arg.valueHint FilePath` (or `DirPath`, `AnyPath`) completes file system paths.

## Test a generated script

`mise.toml` pins nushell, powershell and fish so you can source the output in each shell.

See [Completions reference](../reference/completions.md) for what each shell receives.
