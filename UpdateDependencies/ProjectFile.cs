using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

public class ProjectFile
{
    string path;
	
    public XDocument XDoc;
    public XElement PrimaryItemGroup;
    public XElement TransitiveItemGroup;
    public XElement[] AllPackageReferences;
    public Dictionary<string, XElement> PackagesByName;
	
    string packageElementName;
	
    public ProjectFile(string path)
    {
        this.path = path;
        var isCentralPkgMgmt = Path.GetFileName(path).Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase);
		
        packageElementName = isCentralPkgMgmt ? "PackageVersion" : "PackageReference";
		
        XDoc = LoadXDocument(path);
		
        AllPackageReferences = XDoc.XPathSelectElements($"/Project/ItemGroup/{packageElementName}").ToArray();
        PackagesByName = AllPackageReferences.ToDictionary(e => e.Attribute("Include").Value, StringComparer.OrdinalIgnoreCase);

        var itemGroups = AllPackageReferences.Select(e => e.Parent)
            .Distinct()
            .ToArray();
		
        TransitiveItemGroup = itemGroups.FirstOrDefault(IsTransitiveDependencyItemGroup);
        PrimaryItemGroup = itemGroups.FirstOrDefault(e => !IsTransitiveDependencyItemGroup(e));
    }
	
    public void AddTransitiveReference(string package, string version)
    {
        if (TransitiveItemGroup is null)
        {
            TransitiveItemGroup = new XElement("ItemGroup", new XAttribute("Label", "Pinned transitive dependencies"));

            PrimaryItemGroup.AddAfterSelf(
                new XText($"{Environment.NewLine}{Environment.NewLine}  "),
                TransitiveItemGroup);
        }
        TransitiveItemGroup.Add(new XElement(packageElementName, new XAttribute("Include", package), new XAttribute("Version", version)));
    }

    static XDocument LoadXDocument(string path)
    {
        using var reader = XmlReader.Create(path, preserveWhiteSpaceReaderSettings);
        return XDocument.Load(reader, LoadOptions.None);
    }
	
    public void Save()
    {
        using var writer = XmlWriter.Create(path, saveProjectSettings);
        XDoc.Save(writer);
    }
    
    static bool IsTransitiveDependencyItemGroup(XElement itemGroup)
    {
        var label = itemGroup.Attribute("Label")?.Value;
        if (label is null)
        {
            return false;
        }
		
        if (label.Contains("transitive", StringComparison.OrdinalIgnoreCase) || label.Contains("pinned", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
		
        return false;
    }
    
    static readonly XmlReaderSettings preserveWhiteSpaceReaderSettings = new XmlReaderSettings
    {
        CloseInput = true,
        IgnoreComments = false,
        IgnoreProcessingInstructions = false,
        IgnoreWhitespace = false
    };

    static readonly XmlWriterSettings saveProjectSettings = new XmlWriterSettings
    {
        OmitXmlDeclaration = true,
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
    };
}