using ChoSiren.Panels;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// Training-entry hook for the lightweight practice feedback. The level-up itself still
    /// runs through GameModel.Train (gold-only, existing formula); this partial only routes
    /// the profile button into the practice panel and restores the dossier on close.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        private void OpenTrainingPractice(int memberIndex, int teamSlot)
        {
            if (DeferUntilReferenceArtReady(() => OpenTrainingPractice(memberIndex, teamSlot))) return;
            if (!model.IsUnlocked(memberIndex))
            {
                Toast("请先签约该成员");
                return;
            }

            CloseModal();
            SuspendLobbyMedia();
            MemberPracticePanel practice = null;
            practice = MemberPracticePanel.Open(safeRoot, model, memberIndex,
                () =>
                {
                    // 2026-11-01: a stage tap can switch the trainee; return to whoever was trained last.
                    int shown = ReferenceEquals(practice, null) ? memberIndex : practice.MemberIndex;
                    ShowScreen(currentScreen);
                    OpenTeamMember(shown, shown == memberIndex ? teamSlot : -1);
                },
                Toast);
        }
    }
}
