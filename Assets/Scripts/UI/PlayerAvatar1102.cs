using UnityEngine;

namespace ChoSiren.UI
{
    /// <summary>The manager wears the same saved face in the lobby, profile, contract and dialogue.</summary>
    public static class PlayerAvatar1102
    {
        public static Sprite Resolve(GameModel model)
        {
            if (model == null) return null;
            return Member(model.PlayerAvatarMemberId) ?? Preset(model.PlayerAvatar);
        }

        public static Sprite Member(string memberId)
        {
            if (string.IsNullOrEmpty(memberId)) return null;
            int index = GameModel.MemberIndexFor1101(memberId);
            if (index < 0) return null;
            Sprite art = Resources.Load<Sprite>(GameModel.Members[index].ResourcePath);
            return PortraitFace1101.CropSprite(memberId, art, 1f, PortraitFace1101.Framing.Head);
        }

        public static Sprite Preset(int index)
        {
            index = Mathf.Clamp(index, 0, GameModel.PlayerAvatarResources.Length - 1);
            return StoryArt1019.Load(GameModel.PlayerAvatarResources[index])
                ?? StoryArt1019.Load(GameModel.PlayerAvatarResources[0])
                ?? StoryArt1019.Load("Art/ProfileAvatar");
        }
    }
}
