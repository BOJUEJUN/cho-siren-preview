using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChoSiren
{
    [Serializable]
    public sealed class MemberCatalogEntry
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Role = string.Empty;
        public string Rarity = string.Empty;
        public string PortraitResourcePath = string.Empty;
        public string ThumbnailResourcePath = string.Empty;
        public int BasePower;
        public int StartingLevel = 1;
        public bool InitiallyUnlocked;
    }

    [Serializable]
    public sealed class MemberCatalogManifest
    {
        public int SchemaVersion = MemberCatalog.CurrentSchemaVersion;
        public List<MemberCatalogEntry> Members = new List<MemberCatalogEntry>();
    }

    public sealed class MemberCatalogRecord
    {
        internal MemberCatalogRecord(MemberCatalogEntry source, string portraitPath, string thumbnailPath)
        {
            Id = source.Id.Trim();
            Name = source.Name.Trim();
            Role = source.Role.Trim();
            Rarity = source.Rarity.Trim();
            PortraitResourcePath = portraitPath;
            ThumbnailResourcePath = string.IsNullOrEmpty(thumbnailPath) ? portraitPath : thumbnailPath;
            BasePower = source.BasePower;
            StartingLevel = source.StartingLevel;
            InitiallyUnlocked = source.InitiallyUnlocked;
        }

        public string Id { get; }
        public string Name { get; }
        public string Role { get; }
        public string Rarity { get; }
        public string PortraitResourcePath { get; }
        public string ThumbnailResourcePath { get; }
        public int BasePower { get; }
        public int StartingLevel { get; }
        public bool InitiallyUnlocked { get; }
    }

    public interface IMemberCatalog
    {
        int Count { get; }
        MemberCatalogRecord this[int index] { get; }
        bool TryGetIndex(string memberId, out int index);
        bool TryGet(string memberId, out MemberCatalogRecord member);
    }

    /// <summary>
    /// Validated, immutable-at-runtime view of the member manifest. Member ids are the durable
    /// identity; array positions are presentation order only and must never be persisted.
    /// </summary>
    public sealed class MemberCatalog : IMemberCatalog
    {
        public const int CurrentSchemaVersion = 1;
        public const string DefaultManifestResourcePath = "Data/member-catalog";

        private static readonly HashSet<string> Roles = new HashSet<string>(StringComparer.Ordinal)
        {
            "主唱", "舞者", "支援"
        };

        private static readonly HashSet<string> Rarities = new HashSet<string>(StringComparer.Ordinal)
        {
            "SSR", "SR", "R"
        };

        private readonly MemberCatalogRecord[] entries;
        private readonly Dictionary<string, int> indexById;

        private MemberCatalog(MemberCatalogRecord[] entries)
        {
            this.entries = entries;
            indexById = new Dictionary<string, int>(entries.Length, StringComparer.Ordinal);
            for (int index = 0; index < entries.Length; index++) indexById.Add(entries[index].Id, index);
        }

        public IReadOnlyList<MemberCatalogRecord> Entries => entries;
        public int Count => entries.Length;

        public MemberCatalogRecord this[int index] => entries[index];

        public bool TryGetIndex(string memberId, out int index) =>
            indexById.TryGetValue(memberId ?? string.Empty, out index);

        public bool TryGet(string memberId, out MemberCatalogRecord member)
        {
            if (TryGetIndex(memberId, out int index))
            {
                member = entries[index];
                return true;
            }

            member = null;
            return false;
        }

        public MemberDefinition[] ToLegacyDefinitions()
        {
            return entries.Select(entry => new MemberDefinition(
                entry.Id,
                entry.Name,
                entry.Role,
                entry.Rarity,
                entry.PortraitResourcePath,
                entry.BasePower,
                entry.ThumbnailResourcePath)).ToArray();
        }

        public static bool TryLoad(string manifestResourcePath, out MemberCatalog catalog, out string error)
        {
            TextAsset asset = Resources.Load<TextAsset>(manifestResourcePath);
            if (asset == null)
            {
                catalog = null;
                error = $"未找到成员清单：Resources/{manifestResourcePath}.json";
                return false;
            }

            try
            {
                MemberCatalogManifest manifest = JsonUtility.FromJson<MemberCatalogManifest>(asset.text);
                return TryCreate(manifest, out catalog, out error);
            }
            catch (Exception exception)
            {
                catalog = null;
                error = $"成员清单无法解析：{exception.Message}";
                return false;
            }
        }

        public static bool TryCreate(MemberCatalogManifest manifest, out MemberCatalog catalog, out string error,
            int minimumMemberCount = 1, Func<string, bool> resourceExists = null)
        {
            catalog = null;
            if (manifest == null)
            {
                error = "成员清单为空";
                return false;
            }

            if (manifest.SchemaVersion != CurrentSchemaVersion)
            {
                error = $"不支持的成员清单版本：{manifest.SchemaVersion}";
                return false;
            }

            if (manifest.Members == null || manifest.Members.Count < minimumMemberCount)
            {
                error = $"成员数量不足：至少需要 {minimumMemberCount} 名";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var portraitPaths = new HashSet<string>(StringComparer.Ordinal);
            MemberCatalogRecord[] normalized = new MemberCatalogRecord[manifest.Members.Count];
            for (int index = 0; index < manifest.Members.Count; index++)
            {
                MemberCatalogEntry source = manifest.Members[index];
                if (source == null)
                {
                    error = $"第 {index + 1} 项成员为空";
                    return false;
                }

                string id = (source.Id ?? string.Empty).Trim();
                string name = (source.Name ?? string.Empty).Trim();
                string role = (source.Role ?? string.Empty).Trim();
                string rarity = (source.Rarity ?? string.Empty).Trim();
                string portraitPath = NormalizeResourcePath(source.PortraitResourcePath);
                string thumbnailPath = NormalizeResourcePath(source.ThumbnailResourcePath);

                if (!IsStableId(id))
                {
                    error = $"成员 ID 无效：{id}；仅允许 3-40 位小写字母、数字和连字符";
                    return false;
                }

                if (!ids.Add(id))
                {
                    error = $"成员 ID 重复：{id}";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(name) || ContainsAsciiLetter(name))
                {
                    error = $"成员 {id} 缺少中文名称或名称含英文字母";
                    return false;
                }

                if (!Roles.Contains(role))
                {
                    error = $"成员 {id} 的定位无效：{role}";
                    return false;
                }

                if (!Rarities.Contains(rarity))
                {
                    error = $"成员 {id} 的稀有度无效：{rarity}";
                    return false;
                }

                if (!IsMemberResourcePath(portraitPath))
                {
                    error = $"成员 {id} 的立绘路径无效：{source.PortraitResourcePath}";
                    return false;
                }

                if (!portraitPaths.Add(portraitPath))
                {
                    error = $"立绘路径重复：{portraitPath}";
                    return false;
                }

                if (!string.IsNullOrEmpty(thumbnailPath) && !IsMemberResourcePath(thumbnailPath))
                {
                    error = $"成员 {id} 的缩略图路径无效：{source.ThumbnailResourcePath}";
                    return false;
                }

                if (!MemberCatalogRules.IsBasePowerInBand(rarity, source.BasePower))
                {
                    error = $"成员 {id} 的基础战力 {source.BasePower} 不在 {rarity} 区间";
                    return false;
                }

                if (source.StartingLevel < 1 || source.StartingLevel > GameModel.MaxMemberLevel)
                {
                    error = $"成员 {id} 的初始等级无效：{source.StartingLevel}";
                    return false;
                }

                if (resourceExists != null)
                {
                    if (!resourceExists(portraitPath))
                    {
                        error = $"成员 {id} 的立绘资源不存在：{portraitPath}";
                        return false;
                    }

                    if (!string.IsNullOrEmpty(thumbnailPath) && !resourceExists(thumbnailPath))
                    {
                        error = $"成员 {id} 的缩略图资源不存在：{thumbnailPath}";
                        return false;
                    }
                }

                source.Id = id;
                source.Name = name;
                source.Role = role;
                source.Rarity = rarity;
                normalized[index] = new MemberCatalogRecord(source, portraitPath, thumbnailPath);
            }

            catalog = new MemberCatalog(normalized);
            error = string.Empty;
            return true;
        }

        private static string NormalizeResourcePath(string path)
        {
            return (path ?? string.Empty).Trim().Replace('\\', '/').Trim('/');
        }

        private static bool IsMemberResourcePath(string path)
        {
            return path.StartsWith("Art/Members/", StringComparison.Ordinal) &&
                   !path.StartsWith("Resources/", StringComparison.Ordinal) &&
                   !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                   !path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
                   !path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) &&
                   !path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStableId(string id)
        {
            if (id.Length < 3 || id.Length > 40 || id[0] == '-' || id[id.Length - 1] == '-') return false;
            for (int index = 0; index < id.Length; index++)
            {
                char value = id[index];
                if ((value < 'a' || value > 'z') && (value < '0' || value > '9') && value != '-') return false;
            }

            return true;
        }

        private static bool ContainsAsciiLetter(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if ((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z'))
                    return true;
            }

            return false;
        }
    }

    public static class MemberCatalogRules
    {
        public static bool TryValidateLaunchDistribution(IReadOnlyList<MemberCatalogEntry> entries, out string error)
        {
            if (entries == null || entries.Count != 54)
            {
                error = $"首发成员必须为 54 名，当前为 {entries?.Count ?? 0} 名";
                return false;
            }

            var expectedByRole = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "主唱", 18 },
                { "舞者", 18 },
                { "支援", 18 }
            };
            var expectedByRarity = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "SSR", 12 },
                { "SR", 18 },
                { "R", 24 }
            };

            foreach (KeyValuePair<string, int> expected in expectedByRole)
            {
                int actual = entries.Count(entry => entry != null && entry.Role == expected.Key);
                if (actual != expected.Value)
                {
                    error = $"定位 {expected.Key} 应为 {expected.Value} 名，当前为 {actual} 名";
                    return false;
                }
            }

            foreach (KeyValuePair<string, int> expected in expectedByRarity)
            {
                int actual = entries.Count(entry => entry != null && entry.Rarity == expected.Key);
                if (actual != expected.Value)
                {
                    error = $"稀有度 {expected.Key} 应为 {expected.Value} 名，当前为 {actual} 名";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public static bool IsBasePowerInBand(string rarity, int value)
        {
            GetPowerBand(rarity, out int minimum, out int maximum);
            return minimum > 0 && value >= minimum && value <= maximum;
        }

        public static int DeterministicBasePower(string memberId, string rarity)
        {
            GetPowerBand(rarity, out int minimum, out int maximum);
            if (minimum <= 0) throw new ArgumentException("未知稀有度", nameof(rarity));

            uint hash = 2166136261;
            string key = $"{memberId}|{rarity}";
            for (int index = 0; index < key.Length; index++)
            {
                hash ^= key[index];
                hash *= 16777619;
            }

            return minimum + (int)(hash % (uint)(maximum - minimum + 1));
        }

        public static void GetPowerBand(string rarity, out int minimum, out int maximum)
        {
            switch (rarity)
            {
                case "SSR":
                    minimum = 8200;
                    maximum = 9600;
                    break;
                case "SR":
                    minimum = 7000;
                    maximum = 8199;
                    break;
                case "R":
                    minimum = 5800;
                    maximum = 6999;
                    break;
                default:
                    minimum = 0;
                    maximum = -1;
                    break;
            }
        }
    }

    [Serializable]
    public sealed class MemberProgressV2
    {
        public string MemberId = string.Empty;
        public int Level = 1;
        public bool Unlocked;
    }

    [Serializable]
    public sealed class MemberRosterSaveV2
    {
        public int SchemaVersion = 2;
        public List<MemberProgressV2> Members = new List<MemberProgressV2>();
        public List<string> TeamMemberIds = new List<string>();
    }

    /// <summary>
    /// Pure migration helpers. They do not read or write PlayerPrefs, allowing the caller to
    /// validate a converted save before committing it under a new key.
    /// </summary>
    public static class MemberSaveMigration
    {
        public static MemberRosterSaveV2 FromLegacy(GameSave legacy, IReadOnlyList<MemberDefinition> legacyOrder,
            MemberCatalog catalog)
        {
            if (legacy == null) throw new ArgumentNullException(nameof(legacy));
            if (legacyOrder == null) throw new ArgumentNullException(nameof(legacyOrder));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var legacyIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < legacyOrder.Count; index++)
            {
                string id = legacyOrder[index]?.Id;
                if (!string.IsNullOrEmpty(id) && !legacyIndexById.ContainsKey(id)) legacyIndexById.Add(id, index);
            }

            var unlockedLegacy = new HashSet<int>(legacy.UnlockedMembers ?? new List<int>());
            var result = new MemberRosterSaveV2();
            var unlockedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MemberCatalogRecord entry in catalog.Entries)
            {
                int level = entry.StartingLevel;
                bool unlocked = entry.InitiallyUnlocked;
                if (legacyIndexById.TryGetValue(entry.Id, out int oldIndex))
                {
                    if (legacy.MemberLevels != null && oldIndex >= 0 && oldIndex < legacy.MemberLevels.Count)
                        level = Mathf.Clamp(legacy.MemberLevels[oldIndex], 1, GameModel.MaxMemberLevel);
                    unlocked = unlockedLegacy.Contains(oldIndex);
                }

                result.Members.Add(new MemberProgressV2
                {
                    MemberId = entry.Id,
                    Level = level,
                    Unlocked = unlocked
                });
                if (unlocked) unlockedIds.Add(entry.Id);
            }

            if (unlockedIds.Count == 0 && result.Members.Count > 0)
            {
                result.Members[0].Unlocked = true;
                unlockedIds.Add(result.Members[0].MemberId);
            }

            var teamIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (int oldIndex in legacy.Team ?? new List<int>())
            {
                if (oldIndex < 0 || oldIndex >= legacyOrder.Count) continue;
                string id = legacyOrder[oldIndex]?.Id;
                if (string.IsNullOrEmpty(id) || !catalog.TryGetIndex(id, out _) || !unlockedIds.Contains(id)) continue;
                if (teamIds.Add(id)) result.TeamMemberIds.Add(id);
                if (result.TeamMemberIds.Count == 4) break;
            }

            if (result.TeamMemberIds.Count == 0 && unlockedIds.Count > 0)
                result.TeamMemberIds.Add(result.Members.First(member => member.Unlocked).MemberId);

            return result;
        }

        public static void ToIndexLists(MemberRosterSaveV2 source, MemberCatalog catalog,
            out List<int> unlockedMembers, out List<int> memberLevels, out List<int> team)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var progressById = (source.Members ?? new List<MemberProgressV2>())
                .Where(progress => progress != null && !string.IsNullOrEmpty(progress.MemberId))
                .GroupBy(progress => progress.MemberId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            unlockedMembers = new List<int>();
            memberLevels = new List<int>(catalog.Count);
            for (int index = 0; index < catalog.Count; index++)
            {
                MemberCatalogRecord entry = catalog[index];
                if (progressById.TryGetValue(entry.Id, out MemberProgressV2 progress))
                {
                    memberLevels.Add(Mathf.Clamp(progress.Level, 1, GameModel.MaxMemberLevel));
                    if (progress.Unlocked) unlockedMembers.Add(index);
                }
                else
                {
                    memberLevels.Add(entry.StartingLevel);
                    if (entry.InitiallyUnlocked) unlockedMembers.Add(index);
                }
            }

            if (unlockedMembers.Count == 0 && catalog.Count > 0) unlockedMembers.Add(0);

            var unlockedSet = new HashSet<int>(unlockedMembers);
            team = new List<int>();
            foreach (string id in source.TeamMemberIds ?? new List<string>())
            {
                if (!catalog.TryGetIndex(id, out int index) || !unlockedSet.Contains(index) || team.Contains(index)) continue;
                team.Add(index);
                if (team.Count == 4) break;
            }

            if (team.Count == 0 && unlockedMembers.Count > 0) team.Add(unlockedMembers[0]);
        }
    }
}
