using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>One outfit in a member's wardrobe: the picture's file name (in persistentDataPath/workshop-looks/&lt;member&gt;/) and its label.</summary>
    [Serializable]
    public sealed class WardrobeOutfit1112
    {
        public string MemberId = string.Empty;
        public string File = string.Empty;
        public string Name = string.Empty;
        public string Part = string.Empty;
    }

    [Serializable]
    public sealed class WornOutfit1112
    {
        public string MemberId = string.Empty;
        public string File = string.Empty;
    }

    public sealed partial class GameSave
    {
        public List<WardrobeOutfit1112> WardrobeNotes1112 = new List<WardrobeOutfit1112>();
        public List<WornOutfit1112> WornOutfits1112 = new List<WornOutfit1112>();
    }

    /// <summary>
    /// 2026-11-12 衣橱 (少爷: "创意工坊…换完装在哪里看也不知道…功能要完整，不是 demo"): every AI outfit lands in the member's
    /// wardrobe as soon as it is made (paid results are never lost), carries a label (style · part), and one of them can be worn.
    /// A worn outfit replaces her full portrait wherever the game shows it (成员详情, 大招立绘, 剧情/抽卡展示…) through
    /// <see cref="ChoSiren.Panels.PanelKit.MemberSpriteOrNull"/>; the authored homepage idols stay as drawn (AGENTS.md).
    /// </summary>
    public sealed partial class GameModel
    {
        public const int WardrobeCapacity1112 = 30;

        public IReadOnlyList<WardrobeOutfit1112> Wardrobe1112(string memberId)
        {
            Save.WardrobeNotes1112 ??= new List<WardrobeOutfit1112>();
            var result = new List<WardrobeOutfit1112>();
            var known = new HashSet<string>();
            foreach (string path in WorkshopAi1104.Wardrobe(memberId))
            {
                string file = Path.GetFileName(path);
                known.Add(file);
                WardrobeOutfit1112 note = Save.WardrobeNotes1112.Find(n => n.MemberId == memberId && n.File == file);
                result.Add(note ?? new WardrobeOutfit1112 { MemberId = memberId, File = file, Name = "我的造型" });
            }
            return result;
        }

        public void NoteOutfit1112(string memberId, string path, string name, string part)
        {
            if (string.IsNullOrEmpty(path)) return;
            Save.WardrobeNotes1112 ??= new List<WardrobeOutfit1112>();
            string file = Path.GetFileName(path);
            Save.WardrobeNotes1112.RemoveAll(n => n.MemberId == memberId && n.File == file);
            Save.WardrobeNotes1112.Add(new WardrobeOutfit1112 { MemberId = memberId, File = file, Name = name ?? string.Empty, Part = part ?? string.Empty });
            SaveState();
        }

        /// <summary>The worn outfit's file name, or null for her original look.</summary>
        public string WornOutfitFile1112(string memberId)
        {
            Save.WornOutfits1112 ??= new List<WornOutfit1112>();
            WornOutfit1112 worn = Save.WornOutfits1112.Find(w => w.MemberId == memberId);
            if (worn == null || string.IsNullOrEmpty(worn.File)) return null;
            return File.Exists(WorkshopAi1104.OutfitPath1112(memberId, worn.File)) ? worn.File : null;
        }

        /// <summary>Wears an outfit from her wardrobe (null or empty = back to her original look).</summary>
        public void WearOutfit1112(string memberId, string file)
        {
            Save.WornOutfits1112 ??= new List<WornOutfit1112>();
            Save.WornOutfits1112.RemoveAll(w => w.MemberId == memberId);
            if (!string.IsNullOrEmpty(file)) Save.WornOutfits1112.Add(new WornOutfit1112 { MemberId = memberId, File = file });
            WorkshopAi1104.ForgetWornSprite1112(memberId);
            SaveState();
        }

        public bool DeleteOutfit1112(string memberId, string file)
        {
            if (string.IsNullOrEmpty(file)) return false;
            if (WornOutfitFile1112(memberId) == file) WearOutfit1112(memberId, null);
            Save.WardrobeNotes1112?.RemoveAll(n => n.MemberId == memberId && n.File == file);
            bool removed = WorkshopAi1104.DeleteOutfit1112(memberId, file);
            SaveState();
            return removed;
        }

        public bool WardrobeFull1112(string memberId) => WorkshopAi1104.Wardrobe(memberId).Length >= WardrobeCapacity1112;
    }
}
