// Unity overloads == for destroyed/missing objects, but C# `??` ignores it: in the Editor,
// GetComponent<T>() on a missing component returns a "fake null" so `GetComponent() ?? AddComponent()`
// never adds. Wrap the left side with UnityNull.Real(...) to turn fake nulls into real ones.
public static class UnityNull
{
    public static T Real<T>(T o) where T : UnityEngine.Object => o != null ? o : null;
}
