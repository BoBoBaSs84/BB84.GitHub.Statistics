# BB84.GitHub.Statistics

Generates two SVG cards from your GitHub activity — an overview (stars, forks, contributions, lines changed, views, repo count) and a language breakdown. A C# port of [jstrieb/github-stats](https://github.com/jstrieb/github-stats).

Statistics cover **all** repositories you have contributed to, including private ones, which is why it runs against your own token rather than the public API.

## Access token

Create a **classic** personal access token with these scopes:

| Scope        | Needed for                                      |
| ------------ | ----------------------------------------------- |
| `read:user`  | Contribution years, profile name                |
| `user:email` | Commit attribution in the clone fallback        |
| `repo`       | Private repository statistics and traffic views |

Pass it as `--access-token` or `ACCESS_TOKEN`. Drop `repo` if you only want public data, and set `--exclude-private` alongside it.

## Running locally

Download a binary from the [releases](../../releases) page, or build from source:

```bash
dotnet publish src/BB84.GitHub.Statistics -c Release -o publish
```

Then run it:

```bash
export ACCESS_TOKEN=ghp_yourtokenhere
./publish/github-stats
```

This writes `overview.svg` and `languages.svg` to the working directory. Add `--verbose` to see what it is doing — the first run against a large account is slow, since GitHub's statistics endpoint often has to be polled or fallen back on.

### PowerShell on Windows

The published binary is `github-stats.exe`; the release asset is named `github-stats_x86_64-windows.exe`, so rename it or call it by its full name.

```powershell
dotnet publish src/BB84.GitHub.Statistics -c Release -o publish

$env:ACCESS_TOKEN = 'ghp_yourtokenhere'
.\publish\github-stats.exe --verbose
```

`$env:` variables last only for the current session. To keep the token across sessions:

```powershell
[Environment]::SetEnvironmentVariable('ACCESS_TOKEN', 'ghp_yourtokenhere', 'User')
```

Line continuation is a backtick, not a backslash:

```powershell
.\publish\github-stats.exe --json-input-file stats.json `
    --exclude-langs "HTML,CSS" `
    --overview-output-file out\overview.svg
```

Quote any value containing a comma or `*`, otherwise PowerShell splits it into an array and the tool sees only the first element.

One caveat if you stream to stdout: in **Windows PowerShell 5.1**, `> file.svg` writes UTF-16LE and produces an SVG most tools will not read. PowerShell 7+ defaults to UTF-8. Either write the file directly with `--overview-output-file`, or pipe explicitly:

```powershell
.\github-stats.exe --overview-output-file - | Set-Content overview.svg -Encoding utf8NoBOM
```

`utf8NoBOM` needs PowerShell 7+; on 5.1 use `-Encoding utf8`, which adds a byte-order mark. Writing the file directly avoids the question entirely — the tool emits UTF-8 without a BOM itself.

### Iterating without hammering the API

Collect once, then re-render as often as you like:

```bash
./github-stats --json-output-file stats.json          # one API pass
./github-stats --json-input-file stats.json \         # offline, instant
    --exclude-langs "HTML,CSS" \
    --overview-output-file out/overview.svg
```

`-` works as a path for stdin and stdout, so `--overview-output-file -` streams the SVG (all logging goes to stderr).

### Custom templates

```bash
./github-stats --dump-overview-template my-overview.svg   # edit it, then:
./github-stats --overview-template my-overview.svg
```

Templates use `{{ field }}` placeholders. Overview accepts `name`, `stars`, `forks`, `contributions`, `lines_changed`, `views`, `repos`; languages accepts `progress` and `lang_list`. An unknown placeholder fails the run rather than rendering blank.

## Running in a pipeline

The workflow in [`.github/workflows/main.yml`](.github/workflows/main.yml) is the reference setup: it runs daily, generates the SVGs, and commits them to a `generated` branch so they can be embedded in a profile README.

```yaml
- uses: actions/setup-dotnet@v5
  with:
    dotnet-version: "10.0.x"

- name: Build
  run: |
    dotnet publish src/BB84.GitHub.Statistics -c Release \
      -p:PublishAot=false -o "${RUNNER_TEMP}/app"

- name: Generate images
  run: "${RUNNER_TEMP}/app/github-stats"
  env:
    ACCESS_TOKEN: ${{ secrets.ACCESS_TOKEN }}
    EXCLUDE_REPOS: ${{ secrets.EXCLUDE_REPOS }}
    SILENT: "true"
    MAX_RETRIES: 5
```

Notes for CI:

- Store the token as a repository secret. `secrets.GITHUB_TOKEN` will **not** work — it is scoped to the one repository.
- `-p:PublishAot=false` is deliberate for a job that runs the tool once; a framework-dependent build is several minutes faster and needs no native toolchain.
- Keep `MAX_RETRIES` low. GitHub's contributor-statistics endpoint currently returns `202` indefinitely for many repositories ([community discussion 192970](https://github.com/orgs/community/discussions/192970)), and every retry is time spent waiting for an answer that will not come. Once the retries are exhausted the tool clones the repository and counts locally, which is usually the faster path — **this requires `git` on `PATH`**, otherwise those repositories report zero lines changed.
- Committing the output to a separate branch keeps regenerated SVGs out of your main history.

## Options

Every option is settable as a flag or an environment variable: `--access-token` is `ACCESS_TOKEN`, `--exclude-langs` is `EXCLUDE_LANGS`, and so on. Flags win over the environment. Either `--access-token` or `--json-input-file` is required.

| Option                               | Description                                                   |
| ------------------------------------ | ------------------------------------------------------------- |
| `--access-token`                     | GitHub personal access token (classic).                       |
| `--json-input-file`                  | Read statistics from JSON instead of the API (`-` for stdin). |
| `--json-output-file`                 | Dump collected statistics to JSON (`-` for stdout).           |
| `--exclude-repos`                    | Repositories to skip, comma-separated. Supports `*` globs.    |
| `--exclude-langs`                    | Languages to skip, comma-separated. Supports `*` globs.       |
| `--exclude-private`                  | Leave private repositories out of the totals.                 |
| `--overview-output-file`             | Overview SVG destination (default `overview.svg`).            |
| `--languages-output-file`            | Languages SVG destination (default `languages.svg`).          |
| `--overview-template`                | Use this file instead of the built-in overview template.      |
| `--languages-template`               | Use this file instead of the built-in languages template.     |
| `--dump-overview-template`           | Write the built-in overview template here and exit.           |
| `--dump-languages-template`          | Write the built-in languages template here and exit.          |
| `--max-retries`                      | Retries before cloning instead (default 25).                  |
| `--silent` / `--verbose` / `--debug` | Log errors only / informational / everything.                 |
| `--version`, `-h` / `--help`         | Print version or usage and exit.                              |

`--exclude-repos` splits on spaces as well as commas; `--exclude-langs` does not, so language names containing spaces (`Jupyter Notebook`) survive. Both match case-insensitively, and `*` spans `/` — `BoBoBaSs84/*` excludes everything under that owner.

## License

MIT — see [LICENSE](LICENSE).
