using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EasyNoteVault;

public static class DataStore
{
    private static readonly string FilePath =
        Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "data.json");

    public static List<VaultItem> Load()
    {
        if (!File.Exists(FilePath))
            return new List<VaultItem>();

        var json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<List<VaultItem>>(json)
               ?? new List<VaultItem>();
    }

    public static void Save(IEnumerable<VaultItem> items)
    {
        var json = JsonSerializer.Serialize(
            items,
            new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(FilePath, json);
    }
}
