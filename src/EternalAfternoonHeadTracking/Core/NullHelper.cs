namespace EternalAfternoonHeadTracking
{
    /// <summary>
    /// Null check helper for old Mono compatibility.
    /// Unity's old Mono runtime lacks certain null operators, so we use ReferenceEquals.
    ///
    /// <para><b>When to use each pattern:</b></para>
    /// <list type="bullet">
    ///   <item><c>NullHelper.IsNull(x)</c> — for plain .NET objects (Type, FieldInfo, etc.)
    ///   where Unity's overloaded == is not involved.</item>
    ///   <item><c>x == null</c> — for Unity objects (Component, GameObject, etc.)
    ///   where you need destroyed-object detection.</item>
    /// </list>
    /// </summary>
    internal static class NullHelper
    {
        internal static bool IsNull(object obj) => ReferenceEquals(obj, null);
        internal static bool NotNull(object obj) => !ReferenceEquals(obj, null);
    }
}
