# Contributing workflow

## Branches

- Do not implement or publish changes directly on `main`.
- Create a session branch from the current `main` before committing work.
- Name session branches `Session-DD-MM-YY`, matching the convention already used in this repository (for example, `Session-19-07-26`).
- Keep the session branch after merging so the repository history retains the same structure as earlier work.

## Commits

- Split work into multiple small, logical commits instead of one large commit.
- Each commit should represent one coherent concern and be understandable on its own.
- Use short English commit subjects written in lowercase, following the existing history (for example, `add video source selection`).
- Do not mix unrelated cleanup or formatting with a functional change.
- Build the project and check the diff before publishing the session branch.

## Publishing to main

- Publishing locally means committing changes on the session branch and merging that branch into local `main`.
- Merge session branches with `--no-ff`; do not squash or fast-forward them into `main`.
- Name merge commits using the existing pattern: `MERGE N: Description`, where `N` is the next merge number in the repository history.
- Verify that the working tree is clean and that `main` contains the merge commit after publishing.
- Do not push to `origin` unless the user explicitly requests a push.

## Verification

- Run `dotnet build VideoHarvester.sln` before merging into `main`.
- The build must complete with no errors. Address warnings introduced by the change before publishing.
- Run `git diff --check` to catch whitespace errors before committing or merging.

## Code Style

- **Avoid unnecessary comments** - code should be self-explanatory through clear naming and structure.
- Use comments only when absolutely necessary to explain complex logic or non-obvious decisions.
- Remove commented-out code instead of leaving it in the codebase.

## Git Safety Rules

- **NEVER use `git reset --hard`** - it permanently deletes uncommitted changes from the workspace.
- Use `git reset --soft` if you need to uncommit while keeping changes staged.
- Use `git reset --mixed` (default) to uncommit and unstage while keeping changes in workspace.
- Interactive rebase (`git rebase -i`) cannot be executed through API - ask user to do it manually if needed.
