# How to offer values that are only known at runtime

Use a completer when the values to suggest depend on data your program reads while it runs. This page uses a tool called `hop`, where `hop connect <host>` takes a host name from a list the user keeps in a file.

For a fixed list of values, use `Arg.possibleValues` instead (see [Shell completions](shell-completions.md)). This page assumes you already [print a completion script](shell-completions.md) from your app.

In short: when the user presses Tab on `hop connect <TAB>`, the shell runs your program, your program prints the hosts, and the shell shows them. You write the program side in part 1 and set up the shell side in part 2.

## Part 1: in your program

### 1. Attach a completer to the argument

A completer is a function from the text typed so far to the values to suggest. Here it reads the host list and keeps the hosts that start with what was typed:

```fsharp
let knownHosts (typed: string) : string list =
    readHostsFile ()
    |> List.filter (fun h -> h.StartsWith typed)

let connect =
    Command.create "connect"
    |> Command.arg (Arg.create "host" |> Arg.positional 1 |> Arg.completerValues knownHosts)
```

Your function decides what to return, so it has to do the filtering. `Arg.completerValues` also works on an option, for example `Arg.create "profile" |> Arg.long "profile" |> Arg.completerValues knownProfiles`.

### 2. Optionally add a description to each value

`Arg.completer` returns `Candidate` values, which can carry a description. Shells that support descriptions (Zsh, Fish, PowerShell and Nushell) show it next to the value.

```fsharp
let knownHostsWithNotes (typed: string) : Candidate list =
    readHostsFile ()
    |> List.filter (fun h -> h.Name.StartsWith typed)
    |> List.map (fun h -> { Value = h.Name; Help = Some h.Note })

Arg.create "host" |> Arg.positional 1 |> Arg.completer knownHostsWithNotes
```

If the completer needs something such as a store or a client, write a function that takes it and returns the `Command`.

### 3. Let your program answer the shell

The shell asks for values by running your program with an environment variable set. If you use `Command.parse`, it answers for you and there is nothing to add.

If you call `Command.tryParse` yourself, check for the request first with `Complete.respond`:

```fsharp
let env name = Option.ofObj (System.Environment.GetEnvironmentVariable name)

match Complete.respond env (List.ofArray argv) cmd with
| Some values ->
    System.Console.Write values
    0
| None ->
    // parse and run as usual
```

## Part 2: in your shell

### 4. Generate and install the script again

Run `Completions.generate` as before and install the result in your shell. Only arguments that have a completer make the shell run your program. Fixed values, flags, subcommands and paths are still completed by the script itself.

### 5. Check the program's side by hand

You can test part 1 without a shell. The variable is `<NAME>_COMPLETE`, where `<NAME>` is the command name in upper case with every character that is not a letter or digit replaced by `_`. For `hop`:

```sh
HOP_COMPLETE=bash hop connect pr
```

This prints the hosts that start with `pr`, one per line. With `zsh`, `fish`, `powershell` or `nushell` in place of `bash`, each line is `value<TAB>description`.

`Complete.envVarName cmd` gives you the name from code.

## Notes

- **Speed:** your program starts on every Tab press for a dynamic argument. That is quick under Native AOT. For an app run as `dotnet app.dll` it can be noticeable, so keep the completer cheap.
- **Nushell:** its script splits the command line on single spaces, so a quoted argument containing a space can confuse the completer.

See the [Completions reference](../reference/completions.md) for the exact protocol and output formats.
