namespace Todo.Cli

open System
open Flap

/// Slice: `todo completions <shell>`.
/// A CLI-only concern with no domain behind it: it maps to a `Flap.Shell`, not a domain command.
module CompletionsCli =

    let name = "completions"

    let command =
        Command.create name
        |> Command.about "Print a shell completion script"
        |> Command.arg (
            Arg.create "shell"
            |> Arg.positional 1
            |> Arg.required
            |> Arg.possibleValues [ "bash"; "zsh"; "fish"; "powershell"; "nushell" ]
        )

    let map (m: Matches) : Result<Shell, string> =
        Matches.getWith
            (Value.oneOf [ "bash", Bash; "zsh", Zsh; "fish", Fish; "powershell", PowerShell; "nushell", Nushell ])
            "shell"
            m

    let (|CompletionsCmd|_|) (m: Matches) : Result<Shell, string> option =
        Matches.trySubcommand name m |> Option.map map

    let run (deps: Deps) (mapped: Result<Shell, string>) : int =
        mapped
        |> Output.orFail (fun shell ->
            Console.WriteLine(Completions.generate shell deps.Root)
            0)
