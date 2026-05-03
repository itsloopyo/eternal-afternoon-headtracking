using UnityEngine;

namespace EternalAfternoonHeadTracking
{
    /// <summary>
    /// Helper MonoBehaviour that checks for mod recreation needs every frame.
    /// Also handles periodic log flushing for performance.
    /// </summary>
    internal class ModRecreator : MonoBehaviour
    {
        private int _frameCount;
        private const int LogFlushInterval = 60;

        private void Update()
        {
            ModLoader.CheckRecreate();

            _frameCount++;
            if (_frameCount >= LogFlushInterval)
            {
                _frameCount = 0;
                ModLoader.FlushLog();
            }
        }

        private void OnDestroy()
        {
            ModLoader.FlushLog();
            ModLoader.ResetRecreator();
        }
    }
}
