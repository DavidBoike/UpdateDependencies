using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

public class ProjectFile
{
    string path;
    string packageElementName;
    XmlWriterSettings saveProjectSettings;
    
    XDocument xdoc;
    Encoding encoding;
    XElement primaryItemGroup;
    XElement transitiveItemGroup;
    XElement[] allPkgRefs;
    Dictionary<string, XElement> packagesByName;
	
	
    public ProjectFile(string path)
    {
        this.path = path;
        var isCentralPackageManagement = Path.GetFileName(path).Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase);
		
        packageElementName = isCentralPackageManagement ? "PackageVersion" : "PackageReference";

        encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        saveProjectSettings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Encoding = encoding
        };
        
        xdoc = LoadXDocument(path);
		
        allPkgRefs = xdoc.XPathSelectElements($"/Project/ItemGroup/{packageElementName}").ToArray();
        packagesByName = allPkgRefs.ToDictionary(e => e.Attribute("Include").Value, StringComparer.OrdinalIgnoreCase);

        var itemGroups = allPkgRefs.Select(e => e.Parent)
            .Distinct()
            .ToArray();
		
        transitiveItemGroup = itemGroups.FirstOrDefault(IsTransitiveDependencyItemGroup);
        primaryItemGroup = itemGroups.FirstOrDefault(e => !IsTransitiveDependencyItemGroup(e));
    }

    public bool TryUpdate(string package, string version)
    {
        if (packagesByName.TryGetValue(package, out var pkgRef))
        {
            pkgRef.SetAttributeValue("Version", version);
            return true;
        }

        return false;
    }
	
    public void AddTransitiveReference(string package, string version)
    {
        if (transitiveItemGroup is null)
        {
            transitiveItemGroup = new XElement("ItemGroup", new XAttribute("Label", "Pinned transitive dependencies"));

            primaryItemGroup.AddAfterSelf(
                new XText($"{Environment.NewLine}{Environment.NewLine}  "),
                transitiveItemGroup);
        }
        transitiveItemGroup.Add(new XElement(packageElementName, new XAttribute("Include", package), new XAttribute("Version", version)));
        SortPackageRefs(transitiveItemGroup);
    }

    XDocument LoadXDocument(string path)
    {
        using var reader = new StreamReader(path, Encoding.Default);
        using var xreader = XmlReader.Create(reader, preserveWhiteSpaceReaderSettings);
        var doc = XDocument.Load(xreader);
        
        saveProjectSettings.Encoding = reader.CurrentEncoding;

        return doc;
    }
    
    void SortPackageRefs(XElement itemGroup)
    {
        var orderedItems = itemGroup.DescendantNodes()
            .OfType<XElement>()
            .OrderBy(e => e.Attribute("Include").Value);
			
        List<XNode> newContent = new();
		
        foreach(var item in orderedItems)
        {
            newContent.Add(new XText($"{Environment.NewLine}    "));
            newContent.Add(item);
        }
        newContent.Add(new XText($"{Environment.NewLine}  "));

        itemGroup.ReplaceNodes(newContent.ToArray());
    }
	
    public void Save()
    {
        SortPackageRefs(primaryItemGroup);
        if (transitiveItemGroup is not null)
        {
            SortPackageRefs(transitiveItemGroup);
        }
        using var writer = XmlWriter.Create(path, saveProjectSettings);
        xdoc.Save(writer);
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
        IgnoreWhitespace = false,
    };
}