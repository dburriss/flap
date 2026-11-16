namespace Todo.Cli

open Flap
open Todo.Domain

/// Slice: `todo complete <id>`.
module CompleteCli =

    let name = "complete"

    let command =
        Command.create name
        |> Command.about "Mark a todo as done"
        |> Command.arg (Arg.create "id" |> Arg.positional 1 |> Arg.required |> Arg.help "Id of the todo")

    let map (m: Matches) : Result<CompleteTodo.Command, string> =
        Matches.getInt "id" m |> Result.map (fun id -> { Id = id })

    let (|CompleteCmd|_|) (m: Matches) : Result<CompleteTodo.Command, string> option =
        Matches.trySubcommand name m |> Option.map map

    let run (deps: Deps) (mapped: Result<CompleteTodo.Command, string>) : int =
        mapped
        |> Output.orFail (fun cmd ->
            CompleteTodo.handle deps.Store cmd
            |> Output.report (fun t -> "completed " + Output.describe t))
