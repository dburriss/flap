namespace Todo.Domain

/// Slice: list todos. A read-only use case, so its input is a query rather than a command.
module ListTodos =

    type Query =
        { IncludeDone: bool
          Priority: Priority option }

    let handle (store: Store) (query: Query) : Todo list =
        store.Load()
        |> List.filter (fun t -> query.IncludeDone || not t.Done)
        |> List.filter (fun t ->
            match query.Priority with
            | Some p -> t.Priority = p
            | None -> true)
