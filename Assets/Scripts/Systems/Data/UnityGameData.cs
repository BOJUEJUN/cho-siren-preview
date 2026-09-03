using UnityEngine;

namespace ChoSiren.Systems.Data
{
    /// <summary>Reads tables from Resources. Swap for Addressables without touching callers.</summary>
    public sealed class ResourcesGameDataSource : IGameDataSource
    {
        public bool TryReadText(string resourcePath, out string text)
        {
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            text = asset != null ? asset.text : null;
            return asset != null;
        }
    }

    public sealed class UnityJsonReader : IJsonReader
    {
        public T FromJson<T>(string json) where T : class => JsonUtility.FromJson<T>(json);
    }

    /// <summary>
    /// Process-wide lazily loaded repository. Errors are logged once; callers should check
    /// <see cref="GameDataRepository.IsValid"/> and fall back to a safe screen instead of
    /// crashing when a designer commits a broken table.
    /// </summary>
    public static class GameData
    {
        private static GameDataRepository repository;

        public static GameDataRepository Repository
        {
            get
            {
                if (repository != null) return repository;
                repository = new GameDataRepository(new ResourcesGameDataSource(), new UnityJsonReader());
                if (!repository.LoadAll())
                {
                    for (int index = 0; index < repository.Errors.Count; index++)
                        Debug.LogError("CHO_SIREN_DATA_ERROR " + repository.Errors[index]);
                }

                return repository;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReload()
        {
            repository = null;
        }
    }
}
