namespace Todo.Cli

open Flap
open Todo.Domain

/// Slice: `todo complete <id>`.
module CompleteCli =

    let name = "complete"

    /// Shell completion for `<id>`: the open todos, read from the store when Tab is pressed.
    let private openTodos (store: Store) (partial: string) : Candidate list =
        store.Load()
        |> List.filter (fun t -> not t.Done && (string t.Id).StartsWith partial)
        |> List.map (fun t -> { Value = string t.Id; Help = Some t.Title })

    let command (store: Store) =
        Command.create name
        |> Command.about "Mark a todo as done"
        |> Command.arg (
            Arg.create "id" |> Arg.positional 1 |> Arg.required |> Arg.help "Id of the todo"
            |> Arg.completer (openTodos store))

    let map (m: Matches) : Result<CompleteTodo.Command, string> =
        Matches.getInt "id" m |> Result.map (fun id -> { Id = id })

    let (|CompleteCmd|_|) (m: Matches) : Result<CompleteTodo.Command, string> option =
        Matches.trySubcommand name m |> Option.map map

    let run (deps: Deps) (mapped: Result<CompleteTodo.Command, string>) : int =
        mapped
        |> Output.orFail (fun cmd ->
            CompleteTodo.handle deps.Store cmd
            |> Output.report (fun t -> "completed " + Output.describe t))
