> [!NOTE]
> This "Note" section is here for information about the template, and should be deleted in the target repository.
>
> The `Component` name is used as generic name for component repository being created.
>
> In this template, projects are inlined in `src` directory; no need to have sub-folders since the project names are explicit.
> However, if the list becomes to long (say more than 15 projects), we might want to group them. It's important however to **keep all projects on the same level of the hierarchy** for simplicity. Specially avoid projects under `src` when there are groups.
> For example, do something like:
>
> ```txt
> src
> ├───Group1
> │   ├───Component.Group1.SubComponent1
> │   ├───Component.Group1.SubComponent1.Tests
> │   └───Component.Group1.SubComponent2
> └───Group2
>     ├───Component.Group2.SubComponent3
>     ├───Component.Group2.SubComponent4
>     └───Component.Group2.Tests
> ```
>
> Some sections of this README may not apply on the specific repository being created, feel free to remove them. But, some basic info must be provided: description, links to software factory, steps to build (if specific).

# \<Component\>

[![Build Status]()]()

`<Component>` is (...).

This repository contains (...).

## Documentation

For introduction, architecture overview, in-depth view, etc., see: [`/docs`](./docs)

## Getting Started

### Prerequisites

- .NET SDK

### Steps to build

1. Build

    `dotnet build`

1. Run tests (Optional)

    `dotnet test --no-build`

More information: [.NET CLI overview](https://learn.microsoft.com/en-us/dotnet/core/tools/).

### Local execution

(...)
