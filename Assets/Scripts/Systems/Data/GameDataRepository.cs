using System;
using System.Collections.Generic;
using ChoSiren.Systems.Economy;
using ChoSiren.Systems.Gacha;
using ChoSiren.Systems.Story;
using ChoSiren.Systems.Tactics;

namespace ChoSiren.Systems.Data
{
    /// <summary>Where table text comes from (Resources today, Addressables/remote later).</summary>
    public interface IGameDataSource
    {
        bool TryReadText(string resourcePath, out string text);
    }

    /// <summary>JSON deserializer boundary so the pure loader is testable without JsonUtility.</summary>
    public interface IJsonReader
    {
        T FromJson<T>(string json) where T : class;
    }

    public static class GameDataPaths
    {
        public const string Economy = "Data/economy";
        public const string Gacha = "Data/gacha";
        public const string Tactics = "Data/tactics";
        public const string StoryFolder = "Data/Story/";
        public const string MemberCatalog = MemberCatalogResourcePath;
        private const string MemberCatalogResourcePath = "Data/member-catalog";

        public static string Story(string scriptId) => StoryFolder + scriptId;
    }

    /// <summary>
    /// Single entry point for every designer-editable table. Loads all manifests, validates
    /// each, and collects every error instead of stopping at the first so a designer sees the
    /// whole list after one import.
    /// </summary>
    public sealed class GameDataRepository
    {
        private readonly IGameDataSource source;
        private readonly IJsonReader json;
        private readonly Dictionary<string, StoryScript> stories = new Dictionary<string, StoryScript>(StringComparer.Ordinal);

        public GameDataRepository(IGameDataSource source, IJsonReader json)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.json = json ?? throw new ArgumentNullException(nameof(json));
        }

        public EconomyConfig Economy { get; private set; }
        public GachaManifest Gacha { get; private set; }
        public TacticsManifest Tactics { get; private set; }
        public List<string> Errors { get; } = new List<string>();
        public bool IsValid => Errors.Count == 0 && Economy != null && Gacha != null && Tactics != null;

        public bool LoadAll()
        {
            Errors.Clear();
            Economy = Load<EconomyConfig>(GameDataPaths.Economy, config => config.TryValidate(out string e) ? null : e);
            Gacha = Load<GachaManifest>(GameDataPaths.Gacha, manifest => manifest.TryValidate(out string e) ? null : e);
            Tactics = Load<TacticsManifest>(GameDataPaths.Tactics, manifest => manifest.TryValidate(out string e) ? null : e);
            CrossValidate();
            return IsValid;
        }

        public bool TryGetStory(string scriptId, out StoryScript script, out string error)
        {
            if (stories.TryGetValue(scriptId, out script))
            {
                error = string.Empty;
                return true;
            }

            script = Load<StoryScript>(GameDataPaths.Story(scriptId), value => value.TryValidate(out string e) ? null : e,
                out error);
            if (script == null) return false;
            if (script.Id != scriptId)
            {
                error = $"剧情脚本 {scriptId} 的文件名与 Id 字段不一致：{script.Id}";
                script = null;
                return false;
            }

            stories[scriptId] = script;
            return true;
        }

        private T Load<T>(string path, Func<T, string> validate) where T : class
        {
            T value = Load(path, validate, out string error);
            if (value == null) Errors.Add(error);
            return value;
        }

        private T Load<T>(string path, Func<T, string> validate, out string error) where T : class
        {
            if (!source.TryReadText(path, out string text) || string.IsNullOrWhiteSpace(text))
            {
                error = $"未找到数据表：Resources/{path}.json";
                return null;
            }

            T value;
            try
            {
                value = json.FromJson<T>(text);
            }
            catch (Exception exception)
            {
                error = $"数据表 {path} 无法解析：{exception.Message}";
                return null;
            }

            if (value == null)
            {
                error = $"数据表 {path} 为空";
                return null;
            }

            string validationError = validate(value);
            if (validationError != null)
            {
                error = $"数据表 {path} 校验失败：{validationError}";
                return null;
            }

            error = string.Empty;
            return value;
        }

        private void CrossValidate()
        {
            if (Gacha == null || Tactics == null) return;

            // Character banners must only hand out units the battle system knows about; otherwise
            // a player could pull something that can never be fielded.
            for (int bannerIndex = 0; bannerIndex < Gacha.Banners.Count; bannerIndex++)
            {
                GachaBannerDefinition banner = Gacha.Banners[bannerIndex];
                if (banner.Kind != GachaBannerKind.Character) continue;
                foreach (List<string> pool in new[]
                         {
                             banner.FeaturedItemIds, banner.StandardSsrItemIds, banner.SrItemIds, banner.RItemIds
                         })
                {
                    for (int index = 0; index < pool.Count; index++)
                    {
                        if (Tactics.FindUnit(pool[index]) == null)
                            Errors.Add($"卡池 {banner.Id} 的角色 {pool[index]} 在 tactics.json 中没有单位定义");
                    }
                }
            }
        }
    }
}
