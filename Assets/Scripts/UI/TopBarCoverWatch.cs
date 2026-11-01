using ChoSiren.Panels;
using UnityEngine;

namespace ChoSiren.UI
{
    /// <summary>
    /// The shared header sits on top of every screen; full-screen panels (chapter map, battle)
    /// bring their own header, so it steps aside while one of them is open.
    /// </summary>
    public sealed class TopBarCoverWatch : MonoBehaviour
    {
        private void LateUpdate()
        {
            bool covered = false;
            Transform root = transform.parent;
            if (root != null)
                for (int i = 0; i < root.childCount && !covered; i++)
                {
                    Transform child = root.GetChild(i);
                    if (!child.gameObject.activeInHierarchy) continue;
                    covered = child.GetComponent<LevelMapPanel>() != null || child.GetComponent<TacticsBattlePanel>() != null;
                }
            transform.localScale = covered ? Vector3.zero : Vector3.one;
        }
    }
}
