# Reports.PluginPackager.exe

Reports.PluginPackager.exe is tool that can be used during a build process to create a `*.reports` file, which can be easily deployed.

- By default, the packaging tool will bundle up everything in a folder using reasonable defaults.

## Invoking the packaging step as a post-build event in Visual Studio

This post-build event will create a plugin bundle with the name of the current target. This is usually all you need.

```
$(SolutionDir)Reports.PluginPackager\bin\$(Configuration)\Reports.PluginPackager.exe $(TargetPath) /OutputPath=$(ProjectDir)deploy\$(Configuration)\$(TargetName).report
```

## How does my build know if the packaging step has failed?

The `PluginPackager.exe` tool follows standard exit code conventions. Zero means success, and any positive exit codes means an error occurred.

Visual Studio post-build events will detect any non-zero exit codes and indicate a failed packaging step.

## Packaging by assembly path

If you set the `/AssemblyPath=path` option or just specify a path to an assembly on the command line, the tool will:
- Ensure that the assembly contains a matching JSON file.
- Automatically set the `AssemblyName` from the discovered assembly.

## Other defaults

- The `/DeployedFolder=value` will default to the simplified plugin name unless explicitly set.
