module Todo.Cli.Program

open System
open Flap
open Todo.Cli.AddCli
open Todo.Cli.CompleteCli
open Todo.Cli.ListCli
open Todo.Cli.CompletionsCli

/// One line per command: a slice's partial active pattern matches and maps, its `run` executes.
let private dispatch (deps: Deps) (m: Matches) : int =
    match m with
    | AddCmd cmd         -> AddCli.run deps cmd
    | CompleteCmd cmd    -> CompleteCli.run deps cmd
    | ListCmd cmd        -> ListCli.run deps cmd
    | CompletionsCmd cmd -> CompletionsCli.run deps cmd
    | _ ->
        Output.fail "unknown command"

[<EntryPoint>]
let main argv =
    let deps = { Store = FileStore.store; Root = Cli.root }

    try
        match Command.tryParse (List.ofArray argv) Cli.root with
        | Ok m -> dispatch deps m
        | Error e ->
            let text = ParseError.render e
            let exitCode = ParseError.exitCode e
            if exitCode = 0 then Console.WriteLine text else Console.Error.WriteLine text
            exitCode
    with ex ->
        let isDebug = argv |> Array.contains "--debug"
        Console.Error.WriteLine("todo: unexpected error: " + ex.Message)
        if isDebug then Console.Error.WriteLine(ex.ToString())
        1
