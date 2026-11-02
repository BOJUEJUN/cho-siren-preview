namespace ChoSiren
{
    public sealed partial class GameAudio
    {
        public bool IsVoiceLoading(string name) => !string.IsNullOrEmpty(name) && streamedVoiceLoading.Contains(name);
    }
}
