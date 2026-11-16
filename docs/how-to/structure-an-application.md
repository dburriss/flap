# How to structure an application

This is a recommended layout for a Flap-based tool that does real work: a domain project that knows nothing about the command line, and a CLI project that translates arguments into domain commands. Both are organised by slice (one folder per command), not by layer.

The complete, runnable version is in `samples/Flap.Architecture`.

## Layout

```
Todo.Domain/                 no reference to Flap
  Shared.fs                  only what slices share (types, the Store port)
  Add/AddTodo.fs             command type + use case (`handle`)
  Complete/CompleteTodo.fs
  List/ListTodos.fs

Todo.Cli/                    references Todo.Domain and Flap
  Shared.fs                  Deps, shared priority parsing, console output helpers
  Add/AddCli.fs              Flap definition + mapper + partial active pattern + run
  Complete/CompleteCli.fs
  List/ListCli.fs
  Completions/CompletionsCli.fs
  Cli.fs                     root command, composes each slice's `command`
  Program.fs                 parse, then one dispatch line per command
```

Adding a feature means adding a folder to each project, one line in `Cli.fs` and one line in `Program.fs`.

## Write a slice

Each CLI slice is one module with four parts.

```fsharp
module AddCli =

    let name = "add"

    // 1. The Flap setup
    let command =
        Command.create name
        |> Command.arg (Arg.create "title" |> Arg.positional 1 |> Arg.required)
        |> Command.arg (Arg.create "priority" |> Arg.long "priority"
                        |> Arg.possibleValues Priorities.names |> Arg.defaultValue "normal")

    // 2. The mapper: Matches -> domain command. Pure; fails with a message on bad values.
    let map (m: Matches) : Result<AddTodo.Command, string> =
        Matches.getWith Priorities.parse "priority" m
        |> Result.map (fun p -> { Title = Matches.getString "title" m; Priority = p })

    // 3. A partial active pattern: matches only when `add` was the subcommand used.
    let (|AddCmd|_|) (m: Matches) =
        Matches.trySubcommand name m |> Option.map map

    // 4. Run the domain use case and render the result.
    let run (deps: Deps) (mapped: Result<AddTodo.Command, string>) : int =
        mapped
        |> Output.orFail (fun cmd ->
            AddTodo.handle deps.Store cmd
            |> Output.report (fun t -> "added " + Output.describe t))
```

## Dispatch with the patterns

`Program.fs` parses with the pure `Command.tryParse`, then has one line per command:

```fsharp
let private dispatch (deps: Deps) (m: Matches) : int =
    match m with
    | AddCmd cmd         -> AddCli.run deps cmd
    | CompleteCmd cmd    -> CompleteCli.run deps cmd
    | ListCmd cmd        -> ListCli.run deps cmd
    | CompletionsCmd cmd -> CompletionsCli.run deps cmd
    | _                  -> Output.fail "unknown command"
```

`deps` (the store, the root command) is built once in `main` and handed to every slice. Parsing and mapping stay pure and testable; the console is only touched in `run` and `main`.

## Commands that don't reach the domain

`completions <shell>` is a CLI concern, so its pattern maps to a `Flap.Shell` rather than a domain command, and its `run` uses `deps.Root` to generate the script. Not every slice needs a domain counterpart.

## Try it

```
dotnet run --project samples/Flap.Architecture/Todo.Cli -- add "buy milk" --priority high
dotnet run --project samples/Flap.Architecture/Todo.Cli -- list
dotnet publish samples/Flap.Architecture/Todo.Cli -c Release -r osx-arm64   # Native AOT
```

The sample stores todos in `./todo.txt` (override with `TODO_FILE`).

See [Shell completions](shell-completions.md) for the completions slice and [AOT and Fable](aot-and-fable.md) for the project settings.
