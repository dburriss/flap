namespace Todo.Domain

/// Slice: add a todo.
module AddTodo =

    type Command = { Title: string; Priority: Priority }

    let handle (store: Store) (cmd: Command) : Result<Todo, DomainError> =
        let title = cmd.Title.Trim()

        if title = "" then
            Error EmptyTitle
        else
            let todos = store.Load()
            let nextId = (todos |> List.fold (fun acc t -> max acc t.Id) 0) + 1

            let todo =
                { Id = nextId
                  Title = title
                  Priority = cmd.Priority
                  Done = false }

            store.Save(todos @ [ todo ])
            Ok todo
