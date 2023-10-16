namespace CreateReactApp.Models;

public class PackageJsonModel
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string? License { get; set; }
    public IDictionary<string, string>? Scripts { get; set; }
    public IDictionary<string, string>? Dependencies { get; set; }
    public IDictionary<string, string>? DevDependencies { get; set; }
}
