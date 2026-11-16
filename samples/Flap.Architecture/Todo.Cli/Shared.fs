namespace Todo.Cli

open System
open Todo.Domain

/// What every slice's `run` receives. Built once in Program.fs.
type Deps =
    { Store: Store
      /// The root command, needed by the `completions` slice to generate scripts.
      Root: Flap.Command }

/// How a priority is spelled on the command line (shared by `add` and `list`).
module Priorities =

    let cases = [ "low", Low; "normal", Normal; "high", High ]

    /// Names offered to Flap for validation and shell completion.
    let names = cases |> List.map fst

    /// Parser for `Matches.getWith` / `Matches.tryGetWith`.
    let parse = Flap.Value.oneOf cases

    let nameOf (p: Priority) =
        cases |> List.find (fun (_, v) -> v = p) |> fst

/// Console output shared by the slices. Exit codes: 0 ok, 2 bad input or a domain error.
module Output =

    let describe (t: Todo) =
        let mark = if t.Done then "x" else " "
        "[" + mark + "] " + string t.Id + " " + t.Title + " (" + Priorities.nameOf t.Priority + ")"

    let explain (e: DomainError) =
        match e with
        | EmptyTitle -> "the title must not be empty"
        | NotFound id -> "no todo with id " + string id
        | AlreadyDone id -> "todo " + string id + " is already done"

    let fail (message: string) : int =
        Console.Error.WriteLine("error: " + message)
        2

    /// Run `f` on a successfully mapped command; a mapping failure becomes an error message.
    let orFail (f: 'a -> int) (mapped: Result<'a, string>) : int =
        match mapped with
        | Ok a -> f a
        | Error message -> fail message

    /// Print a use case result, or its error.
    let report (render: 'a -> string) (result: Result<'a, DomainError>) : int =
        match result with
        | Ok a ->
            Console.WriteLine(render a)
            0
        | Error e -> fail (explain e)
