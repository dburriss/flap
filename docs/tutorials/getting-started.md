# Getting started: build a greeter CLI

In this tutorial you build `greet`, a small command-line program, and run it.
It takes about ten minutes. You need the .NET 10 SDK and the Flap package referenced in an F# console project.

## 1. Define the command

Replace the contents of `Program.fs` with:

```fsharp
open Flap

[<EntryPoint>]
let main argv =
    let cmd =
        Command.create "greet"
        |> Command.version "0.1.0"
        |> Command.about "Greets people"
        |> Command.arg (Arg.create "name" |> Arg.short 'n' |> Arg.long "name" |> Arg.help "Who to greet" |> Arg.defaultValue "world")
        |> Command.arg (Arg.create "loud" |> Arg.short 'l' |> Arg.long "loud" |> Arg.flag |> Arg.help "Shout")

    let m = cmd |> Command.parse (List.ofArray argv)
    let msg = "Hello, " + Matches.getString "name" m + "!"
    System.Console.WriteLine(if Matches.flag "loud" m then msg.ToUpperInvariant() else msg)
    0
```

`Command.create` starts a command, `Arg.create` starts an argument, and each `|>` step refines it.
`Command.parse` reads the real command line, and prints help or an error and exits when needed.

## 2. Run it

```
dotnet run -- --name Ada
```

You should see:

```
Hello, Ada!
```

## 3. Try the flag

```
dotnet run -- -n Ada --loud
```

You should see:

```
HELLO, ADA!
```

## 4. Ask for help

```
dotnet run -- --help
```

Flap prints usage, the `about` text, and every option with its help text. `--version` prints `0.1.0`.

## 5. Make a mistake

```
dotnet run -- --nmae Ada
```

Flap reports an unexpected argument, suggests the similar `--name`, and exits with code 2.

## What you built

A command with a defaulted option, a flag, generated help and version output, and did-you-mean errors.

Next:
- Add subcommands, env vars, validators and more: [API reference](../reference/api.md).
- Ship tab completion: [Shell completions](../how-to/shell-completions.md).
- Publish it small and fast: [AOT and Fable](../how-to/aot-and-fable.md).
