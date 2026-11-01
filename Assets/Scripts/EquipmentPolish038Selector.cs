namespace ChoSiren
{
    public sealed partial class ChoSirenApp
    {
        /// <summary>
        /// 饰品管理角色选择。2026-11-01 (visual-b): the old 284×150 crystalline cells are gone — this is the
        /// roster-ui large-card member picker (ChoSirenAppMemberPicker1101, purpose "equipment"), with its
        /// explanation behind 「?」. Selection semantics are unchanged: picking an unlocked member only
        /// retargets the accessory page, it never edits the team or moves gear.
        /// <paramref name="page"/> and <paramref name="stateFilter"/> are kept for existing callers.
        /// </summary>
        private void OpenEquipmentMemberSelector(int page = 0, bool ownedOnly = true, string query = "", int stateFilter = 0)
            => OpenEquipmentMemberSelector1101(ownedOnly, query);
    }
}
