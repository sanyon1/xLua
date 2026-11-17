# Unity verification

Batch-mode checks that the package works in real Unity projects: code generation, hotfix
injection, generated code spread over several assemblies, and IL2CPP players.

Nothing here opens the repository's own Unity project. Each suite copies what it needs into a
scratch directory and runs Unity on the copy.

## Running

From Git Bash:

```
WORK=/c/some/empty/dir ./run.sh              # all suites
WORK=/c/some/empty/dir ./run.sh hotfix unit  # only some
```

`UNITY` (default: 2021.2.1f1) is the editor used by `hotfix` and `unit`, `UNITY6` (default:
6000.2.6f2) the one used by `consumer`. Both need the Windows IL2CPP module. `hotfix` also needs
the .NET SDK to build the injector. A full run takes roughly half an hour.

## Suites

| Suite | What it does | Passes when |
|---|---|---|
| `hotfix` | Copies the repository project, adds `hotfix/` and a second assembly `HfAsm`, defines `HOTFIX_ENABLE`, generates, injects, then runs in editor play mode and as an IL2CPP player. | `HOTFIXTEST SUMMARY` and `MULTITEST SUMMARY` both report `fail=0`, in the editor and in the player. |
| `unit` | Copies the repository project, moves the xLua unit tests to where they expect to be, generates and runs them in editor play mode. | `[  PASSED  ] 273 tests.` appears twice and `ltest_case_failed.txt` has 0 lines. |
| `consumer` | Creates a new project that installs the package by path, with types in three assemblies (`Game`, `Game2`, Assembly-CSharp), generates and builds an IL2CPP player. | `smoke.txt` ends with `result=OK`. |

## What the tests cover

- `hotfix/HotfixSmoke.cs`: replacing instance and static methods, struct arguments, properties,
  `out`/`ref`, generic classes, removing a patch, IntKey, and a delegate type that was not
  generated but shares a signature with one that was.
- `hotfix/MultiAsmSmoke.cs` with `hotfix/HfAsm`: generated code in two assemblies, a value type
  and an enum of one assembly used by wraps in the other, hotfix and IntKey in both assemblies
  without the ids colliding.
- `consumer/`: registration before the first `LuaEnv`, high managed stripping, and value types
  crossing three assemblies.

The xLua unit tests only compile in the editor, so `hotfix` leaves them out of its project.

## Injector

`injector/inj.csproj` builds `XLuaHotfixInject.exe` from the current sources. Unity looks for it
in `./Tools` of the project being injected; `run.sh` puts it there for the `hotfix` suite.
