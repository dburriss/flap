namespace Todo.Cli

open System
open System.IO
open Todo.Domain

/// Infrastructure: a `Store` backed by a tab-separated text file (`$TODO_FILE`, default `./todo.txt`).
module FileStore =

    let private path () =
        match Environment.GetEnvironmentVariable "TODO_FILE" with
        | null
        | "" -> "todo.txt"
        | p -> p

    let private priorityOf (s: string) =
        Priorities.cases |> List.tryFind (fun (k, _) -> k = s) |> Option.map snd |> Option.defaultValue Normal

    let private nameOf (p: Priority) =
        Priorities.nameOf p

    let private parseLine (line: string) : Todo option =
        match line.Split('\t', 4) with
        | [| id; isDone; priority; title |] ->
            match Int32.TryParse id with
            | true, n ->
                Some
                    { Id = n
                      Title = title
                      Priority = priorityOf priority
                      Done = (isDone = "1") }
            | _ -> None
        | _ -> None

    let private formatLine (t: Todo) =
        String.Join("\t", string t.Id, (if t.Done then "1" else "0"), nameOf t.Priority, t.Title.Replace('\t', ' '))

    let store : Store =
        { Load =
            fun () ->
                if File.Exists(path ()) then
                    File.ReadAllLines(path ()) |> Array.toList |> List.choose parseLine
                else
                    []
          Save = fun todos -> File.WriteAllLines(path (), todos |> List.map formatLine) }
