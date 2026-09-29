using System;
using System.Reflection;

namespace EternalAfternoonHeadTracking
{
    /// <summary>
    /// Centralized, search-once-cache-forever resolver for game types accessed via reflection.
    ///
    /// Eternal Afternoon uses:
    /// - KlexberGameTemplate.Core.UserInput: Singleton with CinemachineInputProvider for gameplay detection
    /// - KlexberGameTemplate.Core.PlayerScript: Has middleOfScreenCircle (crosshair GameObject) and defaultFOV
    /// - KlexberGameTemplate.UI.CameraOptions: costumFOVValue, the player's field of view offset
    /// </summary>
    internal static class GameTypeResolver
    {
        private static bool _searched;

        // UserInput — for gameplay detection via CinemachineInputProvider.enabled
        private static Type _userInputType;
        private static FieldInfo _userInputInstanceField;
        private static PropertyInfo _cinemachineInputProviderProperty;

        // PlayerScript — for hiding middleOfScreenCircle
        private static Type _playerScriptType;
        private static FieldInfo _middleOfScreenCircleField;
        private static PropertyInfo _defaultFovProperty;

        // CameraOptions — the field of view offset the player set
        private static PropertyInfo _fovOffsetProperty;

        internal static Type UserInputType { get { EnsureSearched(); return _userInputType; } }
        internal static FieldInfo UserInputInstanceField { get { EnsureSearched(); return _userInputInstanceField; } }
        internal static PropertyInfo CinemachineInputProviderProperty { get { EnsureSearched(); return _cinemachineInputProviderProperty; } }

        internal static Type PlayerScriptType { get { EnsureSearched(); return _playerScriptType; } }
        internal static FieldInfo MiddleOfScreenCircleField { get { EnsureSearched(); return _middleOfScreenCircleField; } }
        internal static PropertyInfo DefaultFovProperty { get { EnsureSearched(); return _defaultFovProperty; } }
        internal static PropertyInfo FovOffsetProperty { get { EnsureSearched(); return _fovOffsetProperty; } }

        private static void EnsureSearched()
        {
            if (_searched) return;
            _searched = true;

            bool foundUserInput = false;
            bool foundPlayerScript = false;
            bool foundCameraOptions = false;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!foundUserInput)
                    {
                        var t = asm.GetType("KlexberGameTemplate.Core.UserInput");
                        if (NullHelper.NotNull(t))
                        {
                            _userInputType = t;
                            foundUserInput = true;

                            _userInputInstanceField = t.GetField("Instance",
                                BindingFlags.Public | BindingFlags.Static);

                            _cinemachineInputProviderProperty = t.GetProperty("cinemachineInputProvider",
                                BindingFlags.Public | BindingFlags.Instance);
                        }
                    }

                    if (!foundPlayerScript)
                    {
                        var t = asm.GetType("KlexberGameTemplate.Core.PlayerScript");
                        if (NullHelper.NotNull(t))
                        {
                            _playerScriptType = t;
                            foundPlayerScript = true;

                            _middleOfScreenCircleField = t.GetField("middleOfScreenCircle",
                                BindingFlags.Public | BindingFlags.Instance);

                            _defaultFovProperty = t.GetProperty("defaultFOV",
                                BindingFlags.Public | BindingFlags.Instance);
                        }
                    }

                    if (!foundCameraOptions)
                    {
                        var t = asm.GetType("KlexberGameTemplate.UI.CameraOptions");
                        if (NullHelper.NotNull(t))
                        {
                            foundCameraOptions = true;

                            _fovOffsetProperty = t.GetProperty("costumFOVValue",
                                BindingFlags.Public | BindingFlags.Static);
                        }
                    }

                    if (foundUserInput && foundPlayerScript && foundCameraOptions) break;
                }
                catch (Exception ex)
                {
                    // Some loaded assemblies (esp. dynamic / partially-loaded ones) can
                    // throw ReflectionTypeLoadException etc. Skip them, but log so this
                    // is diagnosable instead of disappearing silently.
                    string asmName;
                    try { asmName = asm.GetName().Name; }
                    catch { asmName = "<unknown>"; }
                    ModLoader.Log($"[GameTypeResolver] Skipping assembly '{asmName}': {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (!foundUserInput) ModLoader.Log("[GameTypeResolver] UserInput type NOT found");
            if (!foundPlayerScript) ModLoader.Log("[GameTypeResolver] PlayerScript type NOT found");
            if (!foundCameraOptions) ModLoader.Log("[GameTypeResolver] CameraOptions type NOT found");
        }
    }
}
