namespace Todo.Cli

open System
open Flap
open Todo.Domain

/// Slice: `todo list [--all] [--priority p]`.
module ListCli =

    let name = "list"

    let command =
        Command.create name
        |> Command.about "List todos"
        |> Command.alias "ls"
        |> Command.arg (Arg.create "all" |> Arg.short 'a' |> Arg.long "all" |> Arg.flag |> Arg.help "Include completed todos")
        |> Command.arg (
            Arg.create "priority"
            |> Arg.short 'p'
            |> Arg.long "priority"
            |> Arg.possibleValues Priorities.names
            |> Arg.help "Only show this priority"
        )

    let map (m: Matches) : Result<ListTodos.Query, string> =
        Matches.tryGetWith Priorities.parse "priority" m
        |> Result.map (fun priority ->
            { IncludeDone = Matches.flag "all" m
              Priority = priority })

    let (|ListCmd|_|) (m: Matches) : Result<ListTodos.Query, string> option =
        Matches.trySubcommand name m |> Option.map map

    let run (deps: Deps) (mapped: Result<ListTodos.Query, string>) : int =
        mapped
        |> Output.orFail (fun query ->
            ListTodos.handle deps.Store query |> List.iter (Output.describe >> Console.WriteLine)
            0)
