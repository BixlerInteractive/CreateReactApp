using CreateReactApp.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Concurrent;
using System.Diagnostics;

var outputDirectory = GetInput("Enter the output directory", Directory.Exists, "Directory does not exist.");
var appName = GetInput("Enter the application's name");
var appDescription = GetInput("Enter the application's description");
var authorName = GetInput("Enter the author's name");

try
{
    var start = DateTime.Now;

    WriteLog("Initializing YARN...", MessageType.Info);
    await RunCommand("yarn init -y");

    WriteLog("Updating Package.json...", MessageType.Info);
    await UpdatePackageJson();

    WriteLog("Installing Dependencies...", MessageType.Info);
    await RunCommand("yarn");

    WriteLog("Creating File Structure...", MessageType.Info);
    CreateFileStructure();

    WriteLog("Creating Files...", MessageType.Info);
    await CreateFiles();

    WriteLog("Initializing Git...", MessageType.Info);
    await InitializeGit();

    WriteLog("Done!", MessageType.Success);
    WriteLog($"{(DateTime.Now - start).TotalSeconds} seconds", MessageType.Success);
}
catch (Exception e)
{
    WriteLog(e.Message, MessageType.Error);
}

WriteLog("Press Enter to exit");
_ = Console.ReadLine();

return;

// ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- ----- -----

async Task InitializeGit()
{
    await RunCommand("git init");
    await RunCommand("git add .");
    await RunCommand("git commit -m \"Initial commit\"");
}

async Task CreateFiles()
{
    var files = new Dictionary<string, string>
    {
        {
            "tsconfig.json", """
                             {
                               "compilerOptions": {
                                 "target": "es2016",
                                 "allowJs": false,
                                 "skipLibCheck": true,
                                 "esModuleInterop": true,
                                 "strict": true,
                                 "noImplicitAny": true,
                                 "forceConsistentCasingInFileNames": true,
                                 "noFallthroughCasesInSwitch": true,
                                 "module": "commonjs",
                                 "noEmit": true,
                                 "jsx": "preserve"
                               }
                             }
                             """
        },
        {
            "src/index.html", $"""
                               <!DOCTYPE html>
                               <html lang="en">
                               <head>
                                   <meta charset="UTF-8">
                                   <meta content="width=device-width, initial-scale=1.0" name="viewport">
                                   <title>{appName}</title>
                               </head>
                               <body>
                               <div id="root"></div>
                               <script src="App.tsx" type="module"></script>
                               </body>
                               </html>
                               """
        },
        {
            "src/index.d.ts", """
                              declare module "*.webp"
                              """
        },
        {
            "src/App.tsx", """
                           import ReactDOM from "react-dom/client";
                           import { Main } from "./components/main";

                           const root = ReactDOM.createRoot(
                              document.getElementById("root") as HTMLElement,
                           );

                           root.render(
                              <>
                                  <Main />
                              </>,
                           );
                           """

        },
        {
            "src/components/main.tsx", """
                                       import { memo } from "react";
                                       import "../stylesheets/main.scss";

                                       export const Main = memo(() =>
                                       {
                                       	return <>
                                       	    App is working
                                       	</>;
                                       });
                                       """
        },
        {
            "src/stylesheets/main.scss", ""
        },
        {
            ".gitignore", """
                          # See https://help.github.com/articles/ignoring-files/ for more about ignoring files.

                          # dependencies
                          /node_modules
                          /.pnp

                          # production
                          /build

                          # misc
                          .DS_Store
                          .env.local
                          .env.development.local
                          .env.test.local
                          .env.production.local

                          # parcel
                          /dist
                          /.parcel-cache

                          npm-debug.log*
                          yarn-debug.log*
                          yarn-error.log*
                          """
        },
    };

    foreach (var file in files)
    {
        WriteLog($"/{file.Key}", MessageType.Verbose);
        var path = Path.Combine(outputDirectory, file.Key);
        await File.WriteAllTextAsync(path, file.Value);
    }
}

void CreateFileStructure()
{
    var pathStrings = new[]
    {
        "src", "src/components", "src/constants", "src/data", "src/helpers", "src/stylesheets", "static", "static/images",
    };

    foreach (var pathString in pathStrings)
    {
        WriteLog($"./{pathString}", MessageType.Verbose);
        var path = Path.Combine(outputDirectory, pathString);
        Directory.CreateDirectory(path);
    }
}

async Task UpdatePackageJson()
{
    var path = Path.Combine(outputDirectory, "package.json");
    var fileRead = await File.ReadAllTextAsync(path);
    var package = JsonConvert.DeserializeObject<PackageJsonModel>(fileRead);
    if (package == null)
    {
        throw new Exception("Failed to read package.json");
    }

    const string indexPath = "src/index.html";

    package.Author = authorName;
    package.Name = appName;
    package.Description = appDescription;
    package.Version = "0.0.1";
    package.License = "ISC";

    if (package.Scripts == null)
    {
        package.Scripts = new Dictionary<string, string>();
    }
    else
    {
        package.Scripts.Clear();
    }
    package.Scripts.Add("start", $"parcel {indexPath} -p 3000");
    package.Scripts.Add("build", $"npm run check && parcel build {indexPath} --dist-dir build");
    package.Scripts.Add("serve", "serve -s build");
    package.Scripts.Add("check", "tsc --noEmit");

    package.Dependencies = new ConcurrentDictionary<string, string>();
    var dependencies = new[]
    {
        "parcel", "react", "react-dom",
    };
    var dependencyTask = Parallel.ForEachAsync(dependencies,
        async (dependency, _) =>
        {
            var version = await GetLatestPackageVersion(dependency);
            package.Dependencies.Add(dependency, version);
        }
    );

    package.DevDependencies = new ConcurrentDictionary<string, string>();
    var devDependencies = new[]
    {
        "typescript", "@types/react", "@types/react-dom", "parcel-plugin-static-files-copy",
    };
    var devDependencyTask = Parallel.ForEachAsync(devDependencies,
        async (devDependency, _) =>
        {
            var version = await GetLatestPackageVersion(devDependency);
            package.DevDependencies.Add(devDependency, version);
        }
    );

    Task.WaitAll(dependencyTask, devDependencyTask);

    var fileWrite = JsonConvert.SerializeObject(package,
        new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy(),
            },
            Formatting = Formatting.Indented,
        });

    await File.WriteAllTextAsync(path, fileWrite);
}

async Task<string> GetLatestPackageVersion(string packageName)
{
    return (await RunCommand($"npm view {packageName} version")).Replace("\n", "");
}

async Task<string> RunCommand(string command)
{
    var process = new Process();
    var startInfo = new ProcessStartInfo
    {
        WindowStyle = ProcessWindowStyle.Hidden,
        FileName = "cmd.exe",
        Arguments = $"/C {command}",
        WorkingDirectory = outputDirectory,
        UseShellExecute = false,
        RedirectStandardOutput = true,
    };
    process.StartInfo = startInfo;
    process.Start();
    var output = await process.StandardOutput.ReadToEndAsync();
    await process.WaitForExitAsync();
    return output;
}

string GetInput(string message, Func<string, bool>? validator = null, string? errorMessage = null)
{
    string? input;
    var success = false;
    do
    {
        WriteLog(message, MessageType.Info);
        input = @"" + Console.ReadLine();

        if (string.IsNullOrEmpty(input))
        {
            WriteLog("Input cannot be empty.", MessageType.Error);
            continue;
        }

        if (validator != null && !validator(input))
        {
            WriteLog(errorMessage ?? "Validation failed", MessageType.Error);
            continue;
        }

        success = true;
    } while (!success);

    return input;
}

void WriteLog(string message, MessageType messageType = MessageType.General)
{
    var color = messageType switch
    {
        MessageType.General => ConsoleColor.White,
        MessageType.Info => ConsoleColor.Cyan,
        MessageType.Verbose => ConsoleColor.Gray,
        MessageType.Success => ConsoleColor.Green,
        MessageType.Warning => ConsoleColor.Yellow,
        MessageType.Error => ConsoleColor.Red,
        _ => throw new ArgumentOutOfRangeException(nameof(messageType), messageType, null),
    };
    Console.ForegroundColor = color;
    Console.WriteLine(message);
    Console.ResetColor();
}

internal enum MessageType
{
    General,
    Info,
    Verbose,
    Success,
    Warning,
    Error,
}
