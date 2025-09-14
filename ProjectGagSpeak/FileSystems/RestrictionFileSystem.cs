using CkCommons.FileSystem;
using CkCommons.HybridSaver;
using GagSpeak.Localization;
using GagSpeak.Services.Configs;
using GagSpeak.Services.Mediator;
using GagSpeak.State.Managers;
using GagSpeak.State.Models;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace GagSpeak.FileSystems;

public sealed class RestrictionFileSystem : CkFileSystem<RestrictionItem>, IMediatorSubscriber, IHybridSavable, IDisposable
{
    private readonly ILogger<RestrictionFileSystem> _logger;
    private readonly RestrictionManager _manager;
    private readonly HybridSaveService _hybridSaver;
    public GagspeakMediator Mediator { get; init; }
    public RestrictionFileSystem(ILogger<RestrictionFileSystem> logger, GagspeakMediator mediator, 
        RestrictionManager manager, HybridSaveService saver)
    {
        _logger = logger;
        Mediator = mediator;
        _manager = manager;
        _hybridSaver = saver;

        Mediator.Subscribe<ConfigRestrictionChanged>(this, (msg) => OnRestrictionChange(msg.Type, msg.Item, msg.OldString));
        Mediator.Subscribe<ReloadFileSystem>(this, (msg) => { if (msg.Module is GagspeakModule.Restriction) Reload(); });
        Changed += OnChange;
        Reload();
    }

    private void Reload()
    {
        if (Load(new FileInfo(_hybridSaver.FileNames.CKFS_Restrictions), _manager.Storage, RestrictionToIdentifier, RestrictionToName))
            _hybridSaver.Save(this);

        _logger.LogDebug("Reloaded restrictions filesystem with " + _manager.Storage.Count + " restriction items.");
    }

    public void Dispose()
    {
        Mediator.Unsubscribe<ConfigRestrictionChanged>(this);
        Changed -= OnChange;
    }

    private void OnChange(FileSystemChangeType type, IPath _1, IPath? _2, IPath? _3)
    {
        if (type != FileSystemChangeType.Reload)
            _hybridSaver.Save(this);
    }

    public bool FindLeaf(RestrictionItem restriction, [NotNullWhen(true)] out Leaf? leaf)
    {
        leaf = Root.GetAllDescendants(ISortMode<RestrictionItem>.Lexicographical)
            .OfType<Leaf>()
            .FirstOrDefault(l => l.Value == restriction);
        return leaf != null;
    }

    private void OnRestrictionChange(StorageChangeType type, RestrictionItem restriction, string? oldString)
    {
        switch (type)
        {
            case StorageChangeType.Created:
                var parent = Root;
                if(oldString != null)
                    try { parent = FindOrCreateAllFolders(oldString); }
                    catch (Bagagwa ex) { _logger.LogWarning(ex, $"Could not move restriction because the folder could not be created."); }

                CreateDuplicateLeaf(parent, restriction.Label, restriction);
                return;
            case StorageChangeType.Deleted:
                if (FindLeaf(restriction, out var leaf1))
                    Delete(leaf1);
                return;

            case StorageChangeType.Modified:
                // need to run checks for type changes and modifications.
                if (!FindLeaf(restriction, out var existingLeaf))
                    return;
                // Detect potential renames.
                if (existingLeaf.Name != restriction.Label)
                    RenameWithDuplicates(existingLeaf, restriction.Label);
                return;

            case StorageChangeType.Renamed when oldString != null:
                if (!FindLeaf(restriction, out var leaf2))
                    return;
                var old = oldString.FixName();
                if (old == leaf2.Name || leaf2.Name.IsDuplicateName(out var baseName, out _) && baseName == old)
                    RenameWithDuplicates(leaf2, restriction.Label);
                return;
        }
    }

    // Used for saving and loading.
    private static string RestrictionToIdentifier(RestrictionItem restriction)
        => restriction.Identifier.ToString();

    private static string RestrictionToName(RestrictionItem restriction)
        => restriction.Label.FixName();

    private static bool RestrictionHasDefaultPath(RestrictionItem restriction, string fullPath)
    {
        var regex = new Regex($@"^{Regex.Escape(RestrictionToName(restriction))}( \(\d+\))?$");
        return regex.IsMatch(fullPath);
    }

    private static (string, bool) SaveRestriction(RestrictionItem restriction, string fullPath)
        // Only save pairs with non-default paths.
        => RestrictionHasDefaultPath(restriction, fullPath)
            ? (string.Empty, false)
            : (RestrictionToIdentifier(restriction), true);

    // HybridSavable
    public int ConfigVersion => 0;
    public HybridSaveType SaveType => HybridSaveType.StreamWrite;
    public DateTime LastWriteTimeUTC { get; private set; } = DateTime.MinValue;
    public string GetFileName(ConfigFileProvider files, out bool isAccountUnique)
        => (isAccountUnique = false, files.CKFS_Restrictions).Item2;

    public string JsonSerialize() 
        => throw new NotImplementedException();

    public void WriteToStream(StreamWriter writer) 
        => SaveToFile(writer, SaveRestriction, true);
}

