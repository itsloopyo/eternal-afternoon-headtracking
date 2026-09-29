using UnityEngine;

namespace EternalAfternoonHeadTracking
{
    /// <summary>
    /// The scene's PlayerScript, found once and held until the scene that owns it unloads.
    /// The crosshair, the field of view reader and the lean trace all read from it.
    /// </summary>
    internal sealed class GamePlayer
    {
        // FindObjectsOfType walks every object in the scene, so a missing player (the menus)
        // is looked for again at most every two seconds at 60fps.
        private const int RetryIntervalFrames = 120;

        private Component _player;
        private int _nextSearchFrame;

        /// <summary>The live PlayerScript, or null when the scene has none.</summary>
        internal Component Current
        {
            get
            {
                if (_player != null) return _player;

                int frame = Time.frameCount;
                if (frame < _nextSearchFrame) return null;
                _nextSearchFrame = frame + RetryIntervalFrames;

                var playerScriptType = GameTypeResolver.PlayerScriptType;
                if (NullHelper.IsNull(playerScriptType)) return null;

                var playerScripts = Object.FindObjectsOfType(playerScriptType);
                if (playerScripts.Length == 0) return null;

                _player = (Component)playerScripts[0];
                return _player;
            }
        }
    }
}
