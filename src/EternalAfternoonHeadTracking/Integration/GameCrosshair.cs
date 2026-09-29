using UnityEngine;

namespace EternalAfternoonHeadTracking
{
    /// <summary>
    /// Finds the game's middleOfScreenCircle crosshair and repositions it
    /// to match the aim offset from head tracking.
    ///
    /// The crosshair is a UI GameObject on PlayerScript anchored at screen center.
    /// The game only calls SetActive() on it (never moves it), so we can safely
    /// set its RectTransform.anchoredPosition each frame.
    /// </summary>
    internal sealed class GameCrosshair
    {
        private readonly GamePlayer _player;
        private RectTransform _crosshairRect;
        private RectTransform _canvasRect;
        private bool _found;
        private Vector2 _originalPosition;

        // Canvas-to-screen scale is effectively constant until resolution changes.
        // Recompute only when Screen.width/height shift to avoid per-frame
        // RectTransform.rect access (potentially triggers layout).
        private float _cachedScaleX = 1f;
        private float _cachedScaleY = 1f;
        private int _cachedScreenWidth;
        private int _cachedScreenHeight;

        internal GameCrosshair(GamePlayer player)
        {
            _player = player;
        }

        /// <summary>
        /// Moves the game crosshair by the given screen-pixel offset from center.
        /// Converts screen pixels to canvas coordinates automatically.
        /// </summary>
        internal void SetOffset(Vector2 screenPixelOffset)
        {
            if (!EnsureFound()) return;

            if (_canvasRect != null)
            {
                int screenW = Screen.width;
                int screenH = Screen.height;
                if (screenW != _cachedScreenWidth || screenH != _cachedScreenHeight)
                {
                    Rect r = _canvasRect.rect;
                    _cachedScaleX = screenW > 0 ? r.width / screenW : 1f;
                    _cachedScaleY = screenH > 0 ? r.height / screenH : 1f;
                    _cachedScreenWidth = screenW;
                    _cachedScreenHeight = screenH;
                }
            }

            _crosshairRect.anchoredPosition = _originalPosition + new Vector2(
                screenPixelOffset.x * _cachedScaleX,
                screenPixelOffset.y * _cachedScaleY);
        }

        /// <summary>
        /// Resets the crosshair to its original position (screen center).
        /// </summary>
        internal void ResetPosition()
        {
            if (_found && _crosshairRect != null)
            {
                _crosshairRect.anchoredPosition = _originalPosition;
            }
        }

        private bool EnsureFound()
        {
            if (_found)
            {
                // Check if Unity object was destroyed (scene change etc.)
                if (_crosshairRect != null) return true;
                _found = false;
            }

            var go = FindCrosshairGameObject();
            if (go == null) return false;

            _crosshairRect = go.GetComponent<RectTransform>();
            if (_crosshairRect == null) return false;

            var canvas = go.GetComponentInParent<Canvas>();
            if (canvas != null)
                _canvasRect = (RectTransform)canvas.transform;

            _originalPosition = _crosshairRect.anchoredPosition;

            // Force scale recompute on first SetOffset after (re)discovery —
            // the new canvas may differ from whatever we had cached.
            _cachedScreenWidth = 0;
            _cachedScreenHeight = 0;

            _found = true;
            return true;
        }

        private GameObject FindCrosshairGameObject()
        {
            var crosshairField = GameTypeResolver.MiddleOfScreenCircleField;
            if (NullHelper.IsNull(crosshairField)) return null;

            Component player = _player.Current;
            if (player == null) return null;

            return crosshairField.GetValue(player) as GameObject;
        }
    }
}
