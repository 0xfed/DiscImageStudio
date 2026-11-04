using System.Text.Json;
using DiscImageStudio.BluRay;
using DiscImageStudio.Cd;
using DiscImageStudio.Core;
using DiscImageStudio.Dvd;
using DiscImageStudio.Localization;

internal static class DescriptorTranslationTests
{
    internal static void Run(string[] languages)
    {
        string original = L.PreferredLanguage;
        try
        {
            L.Initialize(L.English);
            IOpticalDiscModule[] modules = [new CdDiscModule(), new DvdDiscModule(), new BluRayDiscModule()];
            DiscModuleDescriptor[] descriptors = modules.Select(module => module.Descriptor).ToArray();
            Dictionary<object, (string Name, string? Description)> source = [];
            foreach (DiscModuleDescriptor descriptor in descriptors)
            {
                source.Add(descriptor, (descriptor.DisplayName, descriptor.Description));
                foreach (DiscCommandDescriptor command in descriptor.Commands)
                {
                    source.Add(command, (command.DisplayName, command.Description));
                    foreach (DiscOptionDefinition option in command.Options)
                        source.TryAdd(option, (option.DisplayName, null));
                }
            }
            foreach ((string name, string? description) in source.Values)
            {
                Check(L.TryGetText(name, out _), "unregistered metadata label: " + name);
                Check(description is null || L.TryGetText(description, out _), "unregistered metadata description: " + description);
            }
            string technical = TechnicalValues(descriptors);
            foreach (string language in languages)
            {
                L.SetLanguage(language);
                for (int index = 0; index < modules.Length; index++)
                {
                    DiscModuleDescriptor descriptor = modules[index].Descriptor;
                    Check(ReferenceEquals(descriptor, descriptors[index]), "language switch replaced cached descriptors");
                    Check(descriptor.DisplayName == L.T(source[descriptor].Name)
                          && descriptor.Description == L.T(source[descriptor].Description!), "module metadata in " + language);
                    foreach (DiscCommandDescriptor command in descriptor.Commands)
                    {
                        Check(command.DisplayName == L.T(source[command].Name)
                              && command.Description == L.T(source[command].Description!), "command metadata in " + language);
                        foreach (DiscOptionDefinition option in command.Options)
                        {
                            Check(option.DisplayName == L.T(source[option].Name), "option metadata in " + language);
                            Check(option.DisplayUnit == (option.Unit is null ? null : L.T(option.Unit)), "display unit in " + language);
                            Check(!JsonSerializer.Serialize(option).Contains("DisplayUnit", StringComparison.Ordinal),
                                "presentation-only units changed the serialized metadata schema");
                        }
                    }
                }
                Check(TechnicalValues(descriptors) == technical, "routing, capabilities, defaults, units, or choices changed in " + language);
            }
            DiscOptionDefinition changed = descriptors[0].Commands[0].Options[0] with { DisplayName = "Outer radius" };
            L.SetLanguage(L.Japanese);
            Check(changed.DisplayName == L.T("Outer radius"), "record init did not retain its new key");
            L.SetLanguage(L.English);
            Check(changed.DisplayName == "Outer radius", "record copy captured translated text");
            Console.WriteLine("descriptor translations: passed (cached labels follow five languages; routing, choices, defaults, units and schema preserved)");
        }
        finally { L.Initialize(original); }
    }

    private static string TechnicalValues(IEnumerable<DiscModuleDescriptor> descriptors) => JsonSerializer.Serialize(
        descriptors.Select(d => new
        {
            d.Id, d.Family, d.SupportsCancellation,
            Commands = d.Commands.Select(c => new
            {
                c.Name, c.Capabilities,
                Options = c.Options.Select(o => new { o.Name, o.ValueType, o.Required, o.DefaultValue, o.Unit, o.Choices }),
            }),
        }));

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
