# How to complete values at runtime

Use a completer when the valid values are only known while the program runs, such as ids from a database or file. For a fixed list, use `Arg.possibleValues` instead (see [Shell completions](shell-completions.md)).

This page assumes you already [print a completion script](shell-completions.md) from your app.

## Attach a completer to an arg

`Arg.completerValues` takes the partial word and returns plain strings:

```fsharp
let projects (partial: string) : string list =
    loadProjectNames () |> List.filter (fun p -> p.StartsWith partial)

Arg.create "project" |> Arg.long "project" |> Arg.completerValues projects
```

Your function receives the word being completed and decides what to return, so it should filter by `partial` itself.

It works the same on a positional:

```fsharp
Arg.create "id" |> Arg.positional 1 |> Arg.completerValues projects
```

## Add descriptions

`Arg.completer` returns `Candidate` values, which can carry a description. Shells that support descriptions (Zsh, Fish, PowerShell, Nushell) show it next to the value.

```fsharp
let openTodos (partial: string) : Candidate list =
    store.Load()
    |> List.filter (fun t -> not t.Done && (string t.Id).StartsWith partial)
    |> List.map (fun t -> { Value = string t.Id; Help = Some t.Title })

Arg.create "id" |> Arg.positional 1 |> Arg.completer openTodos
```

A completer that needs services (a store, a client) is easiest to write as a function that takes them and returns the `Command`, like `CompleteCli.command store` in `samples/Flap.Architecture`.

## Answer completion requests

Generated scripts call your program with an environment variable set. Handle it before normal parsing.

If you use `Command.parse`, there is nothing to do: it answers the request and exits.

If you call `Command.tryParse` yourself, use `Complete.respond`:

```fsharp
let env name = Option.ofObj (System.Environment.GetEnvironmentVariable name)

match Complete.respond env (List.ofArray argv) cmd with
| Some candidates ->
    System.Console.Write candidates
    0
| None ->
    // parse and run as usual
```

## Regenerate and install the script

Generate the script again with `Completions.generate` and install it as before. Scripts only gain call-back code when at least one arg has a completer, and only those args launch your program.

## Check it by hand

The variable is `<NAME>_COMPLETE`, where `<NAME>` is the command name upper-cased with every non-alphanumeric character replaced by `_`. For a command named `my-tool`:

```sh
MY_TOOL_COMPLETE=bash my-tool deploy --project al
```

This prints one candidate per line. Use `zsh`, `fish`, `powershell` or `nushell` to see the tab-separated `value<TAB>description` form.

You can also read the name from code with `Complete.envVarName cmd`.

## Keep completers fast

Your program starts on every Tab press for a dynamic arg. Under Native AOT this is quick. For an app run as `dotnet app.dll`, startup can be noticeable, so keep the completer cheap and avoid work in module initialisers.

## Nushell

The Nushell script splits the command line on single spaces, so a quoted argument containing a space can confuse the completer. Plain values work as expected.

See the [Completions reference](../reference/completions.md) for the exact protocol and output formats.
