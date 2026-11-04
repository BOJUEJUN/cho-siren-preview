using System;
using System.Collections.Generic;

namespace ChoSiren
{
    /// <summary>One member's 创意工坊 settings (2026-11-04). Indices refer to the option lists in ChoSirenApp.Workshop1104.</summary>
    [Serializable]
    public sealed class WorkshopLook1104
    {
        public string MemberId = string.Empty;
        public int Filter;
        public int Brightness = 100;
        public int Pitch;
        public int Volume = 100;
        public int Background;
        public int Accent;
        public int Bob = 50;
        public int Scale = 100;
        public bool Mirror;
        /// <summary>Voice samples recorded for the custom voice (0-3), saved under persistentDataPath/voice-samples.</summary>
        public int SamplesRecorded;
        public bool VoiceConsent;
    }

    public sealed partial class GameSave
    {
        public List<WorkshopLook1104> WorkshopLooks1104 = new List<WorkshopLook1104>();
    }

    public sealed partial class GameModel
    {
        /// <summary>The member's look (a default one, not yet stored, when she has none).</summary>
        public WorkshopLook1104 WorkshopLook1104(string memberId)
        {
            Save.WorkshopLooks1104 ??= new List<WorkshopLook1104>();
            foreach (WorkshopLook1104 look in Save.WorkshopLooks1104)
                if (look.MemberId == memberId) return look;
            return new WorkshopLook1104 { MemberId = memberId };
        }

        /// <summary>Stores the edited look.</summary>
        public void SaveWorkshopLook1104(WorkshopLook1104 look)
        {
            if (look == null || string.IsNullOrEmpty(look.MemberId)) return;
            Save.WorkshopLooks1104 ??= new List<WorkshopLook1104>();
            int at = Save.WorkshopLooks1104.FindIndex(l => l.MemberId == look.MemberId);
            look.Brightness = Math.Max(60, Math.Min(130, look.Brightness));
            look.Pitch = Math.Max(-30, Math.Min(30, look.Pitch));
            look.Volume = Math.Max(30, Math.Min(100, look.Volume));
            look.Bob = Math.Max(0, Math.Min(100, look.Bob));
            look.Scale = Math.Max(85, Math.Min(115, look.Scale));
            look.SamplesRecorded = Math.Max(0, Math.Min(3, look.SamplesRecorded));
            if (at >= 0) Save.WorkshopLooks1104[at] = look; else Save.WorkshopLooks1104.Add(look);
            SaveState();
        }

        /// <summary>Voice pitch (AudioSource.pitch) and volume share a member's lines play with everywhere.</summary>
        public void VoiceTuning1104(string memberId, out float pitch, out float volume)
        {
            pitch = 1f; volume = 1f;
            if (string.IsNullOrEmpty(memberId) || Save.WorkshopLooks1104 == null) return;
            foreach (WorkshopLook1104 look in Save.WorkshopLooks1104)
                if (look.MemberId == memberId)
                {
                    pitch = 1f + look.Pitch / 200f;
                    volume = look.Volume / 100f;
                    return;
                }
        }
    }
}
