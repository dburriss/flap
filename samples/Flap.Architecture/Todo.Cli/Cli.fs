/// The root command: just composes each slice's `command`.
module Todo.Cli.Cli

open Flap

let root (store: Todo.Domain.Store) =
    Command.create "todo"
    |> Command.version "0.1.0"
    |> Command.about "A tiny todo list, showing a recommended Flap architecture"
    |> Command.subcommandRequired
    |> Command.subcommand AddCli.command
    |> Command.subcommand (CompleteCli.command store)
    |> Command.subcommand ListCli.command
    |> Command.subcommand CompletionsCli.command
