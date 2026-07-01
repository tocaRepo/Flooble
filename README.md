# Flooble

Flooble is a small .NET deterministic workflow runner for YAML-defined automation steps.
It supports file discovery tools and agent-powered steps, with simple placeholder
resolution between inputs and previous step outputs.

## What It Does

- Loads a workflow from YAML.
- Resolves `${input.*}` and `${steps.<stepId>.output}` references.
- Executes steps in order.
- Provides built-in `read_file`, `write_file`, `list_files`, `search_text`, `file_info`, and `agent` handlers.
- Uses a configurable OpenAI-compatible endpoint for agent steps.

## Solution Layout

- `Flooble.Core` contains the workflow model, loader, interpreter, and agent wrapper.
- `Flooble.Tools` contains the file read/write tools used by the CLI.
- `Flooble.Cli` is the sample console app that loads and runs the example workflow.
- `Flooble.GUI` is a basic WPF chat app for talking to the configured agent.
- `Flooble.Web` is the recommended cross-platform browser GUI for chat and workflows.

`Flooble.Web` is browser-based and runs cross-platform. `Flooble.GUI` remains
the Windows-only WPF app.

## Requirements

- .NET 10 SDK
- An OpenAI-compatible API key for the configured agent endpoint

## Getting Started

1. Restore and build the solution.

   ```powershell
   dotnet restore
   dotnet build
   ```

2. Configure the CLI in `Flooble.Cli/configuration.json`.

   The sample configuration expects an `Agent` section with:

   - `API_ENDPOINT` for the OpenAI-compatible server
   - `API_KEY` or the `GROQ_API_KEY` environment variable
   - read/write permissions for local files

3. Run the sample CLI.

   ```powershell
   dotnet run --project Flooble.Cli
   ```

   To run a different workflow file, pass its name or path as the first
   argument. For example:

   ```powershell
   dotnet run --project Flooble.Cli -- review.workflow.yaml
   ```

4. Run the GUI.

   ```powershell
   dotnet run --project Flooble.GUI
   ```

   The GUI uses the same `configuration.json` conventions as the CLI. Make sure
   `Flooble.Cli/configuration.json` contains a valid `API_ENDPOINT` and either
   `API_KEY` or the `GROQ_API_KEY` environment variable is available.

5. Run the web app.

   ```powershell
   dotnet run --project Flooble.Web
   ```

`Flooble.Web` is the recommended cross-platform GUI. It runs in the browser
and uses standard ASP.NET Core configuration sources:

   - `appsettings.json`
- `appsettings.Development.json`
- environment variables

The `/chat` page keeps a session-scoped in-memory transcript so follow-up
questions can use prior turns in the same browser circuit. The transcript is
cleared when you click **Clear chat** and is trimmed to keep only the most
recent messages.

   For local development, you can copy or mirror the settings from
   `Flooble.Cli/configuration.json` into `Flooble.Web/appsettings.Development.json`
   or let the web app reuse that file directly.

## Workflow Format

Workflows are YAML documents with a name, optional inputs, and an ordered list
of steps.

Example:

```yaml
name: summarize-file
input:
  path: test.txt

steps:
  - id: read
    tool: read_file
    with:
      path: "${input.path}"

  - id: summarize
    agent: summarize the text in your own words
    with:
      text: "${steps.read.output}"
```

### Step Types

- `tool: read_file` reads a text file using the configured permissions.
- `tool: write_file` writes text to a file using the configured permissions.
- `tool: list_files` returns a compact tree for a directory, with ignore patterns and maximum depth.
- `tool: search_text` searches text files by substring, regex, or glob and returns file/line snippets.
- `tool: file_info` returns size, modified time, extension, and SHA-256 hash for one or more files.
- `agent: ...` runs an agent step using the provided instructions.

### Placeholder Syntax

- `${input.name}` resolves from workflow input.
- `${steps.stepId.output}` resolves from a previous step output.

Only these two placeholder forms are currently supported.

## Configuration

`Flooble.Cli/configuration.json` controls the agent endpoint and file access
rules.

Example:

```json
{
  "Agent": {
    "API_ENDPOINT": "https://api.groq.com/openai/v1",
    "ReadPermissions": {
      "type": "local",
      "allowedPaths": ["."]
    },
    "WritePermissions": {
      "type": "local",
      "allowedPaths": ["."]
    }
  }
}
```

Permission types:

- `local` limits access to the configured `allowedPaths`.
- `global` allows access anywhere on disk.

### Flooble.Web Configuration

`Flooble.Web` accepts the same `Agent` configuration shape through standard
ASP.NET Core sources:

- `Agent:API_ENDPOINT`
- `Agent:API_KEY`
- `GROQ_API_KEY` if you want to keep using the existing environment variable
- `Agent:ReadPermissions`
- `Agent:WritePermissions`

No API key should be committed to source control.

## Extending Flooble

`Flooble.Core.WorkflowHandlers.CreateDefault` is the main place to add new
handlers. The interpreter resolves arguments first, then dispatches each step
to the matching handler name.

If you add a new tool, register it in `WorkflowHandlers` and document the new
step shape in this README.

## License

Flooble is licensed under the MIT License. See [LICENSE](LICENSE).
