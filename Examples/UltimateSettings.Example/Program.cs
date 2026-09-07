using System.Runtime.CompilerServices;
using UltimateSettings;
using UltimateSettings.Attributes;
using UltimateSettings.Sources;

CopyTemplateIfMissing("user.json");
CopyTemplateIfMissing("machine.json");
CopyTemplateIfMissing("domain.xml");

using var manager = new SettingsManagerBuilder<ExampleSettings>()
    .AddSource("user",    new JsonFileSource("user",    "user.json",    canWrite: true,  watchForChanges: true))
    .AddSource("machine", new JsonFileSource("machine", "machine.json", canWrite: true,  watchForChanges: true))
    .AddSource("domain",  new XmlFileSource("domain",   "domain.xml",   canWrite: false, watchForChanges: true))
    .WithValidator((setting, out error)=>{
        if (string.IsNullOrWhiteSpace(setting.valueA))
        {
            error = "valueA cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(setting.ObjectA.valueA))
        {
            error = "ObjectA.valueA cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(setting.ObjectA.valueB))
        {
            error = "ObjectA.valueB cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(setting.Subcategory.valueA))
        {
            error = "Subcategory.valueA cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(setting.Subcategory.valueB))
        {
            error = "Subcategory.valueB cannot be empty.";
            return false;
        }

        error = null;
        return true;
    })
    .Build();
manager.Load();
Console.WriteLine("Settings sources loaded.");
//log full config
Console.Write(
    System.Text.Json.JsonSerializer.Serialize(
        manager.Current, 
        new System.Text.Json.JsonSerializerOptions() { WriteIndented = true }
    ) + Environment.NewLine
);

manager.SettingsChanged += (sender, args) =>
{
    Console.WriteLine("Settings changed.");
    Console.Write(
        System.Text.Json.JsonSerializer.Serialize(
            manager.Current, 
            new System.Text.Json.JsonSerializerOptions() { WriteIndented = true }
        )+Environment.NewLine
    );
};

manager.SettingsReloadRejected += (sender, args) =>
{
    Console.WriteLine("Settings reload rejected. Error: " + args.Error + Environment.NewLine);
};

// Current is read-only; persisting changes requires Edit(), which writes to one source and reloads.
manager.Edit("user", edit => edit.Set(s => s.valueA, "NewValueA"));

while (true)
{
    Console.WriteLine("Press 'q' to quit.");
    char keyChar;
    if (Console.IsInputRedirected)
    {
        // ReadKey() throws when console input is redirected (e.g. no console attached).
        var line = Console.ReadLine();
        keyChar = string.IsNullOrEmpty(line) ? '\0' : line[0];
    }
    else
    {
        keyChar = Console.ReadKey().KeyChar;
    }

    if (keyChar == 'q')
    {
        break;
    }
}


static void CopyTemplateIfMissing(string configurationFileName)
{
    var destinationPath = Path.Combine(Directory.GetCurrentDirectory(), configurationFileName);
    if (File.Exists(destinationPath))
    {
        return;
    }

    var templatePath = Path.Combine(AppContext.BaseDirectory, "templates", $"{configurationFileName}.template");
    if (!File.Exists(templatePath))
    {
        throw new FileNotFoundException($"Configuration template was not found: '{templatePath}'.", templatePath);
    }

    File.Copy(templatePath, destinationPath);
}

[SourceOrder("user", "machine", "domain")]
public sealed class ExampleSettings : SettingsBase
{
    public string valueA { get; init; } = "Default";
    public MyAtomicSettingsObject ObjectA { get; init; } = new();
    public MySubcattegorySettingsObject Subcategory { get; init; } = new();
}

public sealed class MyAtomicSettingsObject
{
    public string valueA { get; init; } = "Default";
    public string valueB { get; init; } = "Default";
}


public sealed class MySubcattegorySettingsObject : SettingsBase
{
    public string valueA { get; init; } = "Default";

    [SourceOrder("machine", "domain")]
    public string valueB { get; init; } = "Default";
}

