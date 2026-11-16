namespace Todo.Cli

open Flap
open Todo.Domain

/// Slice: `todo add <title> [--priority p]`.
module AddCli =

    let name = "add"

    // 1. The Flap setup.
    let command =
        Command.create name
        |> Command.about "Add a todo"
        |> Command.arg (Arg.create "title" |> Arg.positional 1 |> Arg.required |> Arg.help "What needs doing")
        |> Command.arg (
            Arg.create "priority"
            |> Arg.short 'p'
            |> Arg.long "priority"
            |> Arg.possibleValues Priorities.names
            |> Arg.defaultValue "normal"
            |> Arg.help "How important it is"
        )

    // 2. The mapper: Matches -> domain command. Pure; fails with a message on bad values.
    let map (m: Matches) : Result<AddTodo.Command, string> =
        Matches.getWith Priorities.parse "priority" m
        |> Result.map (fun priority ->
            { Title = Matches.getString "title" m
              Priority = priority })

    // 3. The partial active pattern: only matches when `add` was the subcommand used.
    let (|AddCmd|_|) (m: Matches) : Result<AddTodo.Command, string> option =
        Matches.trySubcommand name m |> Option.map map

    // 4. Run the domain use case and render the result.
    let run (deps: Deps) (mapped: Result<AddTodo.Command, string>) : int =
        mapped
        |> Output.orFail (fun cmd ->
            AddTodo.handle deps.Store cmd
            |> Output.report (fun t -> "added " + Output.describe t))
