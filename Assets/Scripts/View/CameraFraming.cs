using Smiley.Config;
using Smiley.Gameplay;
using UnityEngine;

namespace Smiley.View
{
    /// <summary>
    /// Sizes the camera so it shows a fixed WIDTH of world, whatever the phone's shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Owner, 2026-09-12 (D-099): <i>"increase the visible width of the game view … adjust it
    /// so that the camera shows more horizontal area."</i> An orthographic camera is sized by
    /// half-HEIGHT, and the width follows from the aspect ratio — so to show more across you
    /// raise the size, and how much you get depends on the phone. This turns that round: the
    /// tuning names the width in units, and the size is derived from it here, per device, once.
    /// A wider phone then shows less height, not more width — the framing the map was drawn for.
    /// </para>
    /// <para>
    /// ⭐ <b>D-140 (owner, 2026-10-19): the browser is framed by HEIGHT instead.</b> A landscape
    /// window held to 8.5 units across showed under 5 units tall — four times closer than a phone.
    /// On the web the camera shows a phone's height (<see cref="SmileyTuning.WebVisibleHeight"/>),
    /// capped so it never shows past the map's sides, and never narrower than a phone's 8.5.
    /// It is re-applied when the window changes shape, because a browser can be resized or
    /// made fullscreen mid-climb. ⛔ Phones are untouched: they take the width rule, once.
    /// </para>
    /// <para>
    /// ⛔ Visual only. Nothing in physics or the map reads the camera; §33 and D-025 hold.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFraming : MonoBehaviour
    {
        [SerializeField] private SmileyTuning _tuning;

        private Camera _camera;
        private float _appliedAspect = -1f;

        /// <summary>The orthographic half-height that shows <paramref name="visibleWidth"/> units across at <paramref name="aspect"/>.</summary>
        public static float HalfHeightFor(float visibleWidth, float aspect) =>
            aspect > 0f ? visibleWidth / (2f * aspect) : visibleWidth * 0.5f;

        /// <summary>
        /// D-140: the web's half-height — a phone's height, no wider than the map, no narrower
        /// than a phone's width.
        /// </summary>
        /// <param name="minimumWidth">The phone's width, which the view never drops below.</param>
        /// <param name="visibleHeight">The height to aim for.</param>
        /// <param name="mapWidth">The map's width, which the view never exceeds. Zero or less: no cap.</param>
        /// <param name="aspect">The window's width over its height.</param>
        public static float WebHalfHeightFor(float minimumWidth, float visibleHeight, float mapWidth, float aspect)
        {
            float half = visibleHeight * 0.5f;

            if (mapWidth > 0f)
            {
                half = Mathf.Min(half, HalfHeightFor(mapWidth, aspect));
            }

            return Mathf.Max(half, HalfHeightFor(minimumWidth, aspect));
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            Apply();
        }

        private void Update()
        {
            // ⓘ Only the web re-frames: a phone's shape does not change mid-climb.
            if (Smiley.Web.WebPlatform.Active && !Mathf.Approximately(_camera.aspect, _appliedAspect))
            {
                Apply();
            }
        }

        /// <summary>Applies the framing from tuning to this camera. Public for tests.</summary>
        public void Apply()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            float width = _tuning != null ? _tuning.CameraVisibleWidth : 7.5f;
            _appliedAspect = _camera.aspect;

            if (!Smiley.Web.WebPlatform.Active)
            {
                _camera.orthographicSize = HalfHeightFor(width, _camera.aspect);
                return;
            }

            float height = _tuning != null ? _tuning.WebVisibleHeight : 18.9f;
            var bounds = FindFirstObjectByType<LevelBounds>();
            float mapWidth = bounds != null ? bounds.World.width : 0f;

            _camera.orthographicSize = WebHalfHeightFor(width, height, mapWidth, _camera.aspect);
        }
    }
}
