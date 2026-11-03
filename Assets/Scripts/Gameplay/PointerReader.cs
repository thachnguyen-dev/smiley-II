using UnityEngine;
using UnityEngine.InputSystem;

namespace Smiley.Gameplay
{
    /// <summary>One frame's pointer reading, independent of where it came from.</summary>
    public readonly struct PointerSample
    {
        /// <summary>Whether a finger (or the stand-in mouse button) is down.</summary>
        public readonly bool IsDown;

        /// <summary>Screen-space position, in pixels.</summary>
        public readonly Vector2 Position;

        /// <summary>Identifies the finger, so a second one can be ignored.</summary>
        public readonly int Id;

        public PointerSample(bool isDown, Vector2 position, int id)
        {
            IsDown = isDown;
            Position = position;
            Id = id;
        }

        /// <summary>Nothing is down.</summary>
        public static PointerSample None => new PointerSample(false, Vector2.zero, -1);
    }

    /// <summary>
    /// Reads the touchscreen, falling back to the mouse when there is not one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The first finger down owns the interaction.</b> §6 specifies single-finger input,
    /// so a second touch is ignored until the first lifts. Without an ownership rule a stray
    /// thumb resting on the screen would fight the one the player is aiming with.
    /// </para>
    /// <para>
    /// ⓘ The mouse path exists so the game can be played in the editor with no device
    /// attached. It is not a supported input method — §33 and the platform notes say touch
    /// is the only one.
    /// </para>
    /// </remarks>
    public static class PointerReader
    {
        private static PointerSample? _override;

        /// <summary>
        /// Makes <see cref="Read"/> return this sample instead of the device's.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ⛔ <b>The seam that makes §6 testable at all.</b> A batch-mode run has no touchscreen
        /// and no mouse, so <see cref="Read"/> reports "no finger" forever — and §6 treats a
        /// pointer that is no longer down as a <b>release</b>. Every test of a drag therefore
        /// launched Smiley on the frame after it began one, and the drag itself was never
        /// exercised by anything.
        /// </para>
        /// <para>
        /// ⚠ <b>Deliberately here rather than in the controller.</b> Overriding the reader means
        /// the real <c>Update</c>, the real Begin → Aim → Release ordering and the real
        /// components all run — a test that instead poked the controller would be exercising a
        /// path the finger never takes, and would keep passing while the input was disconnected.
        /// </para>
        /// </remarks>
        public static void SetOverride(PointerSample sample) => _override = sample;

        /// <summary>Hands the input back to the device.</summary>
        public static void ClearOverride() => _override = null;

        /// <summary>Whether something is standing in for the device. For tests.</summary>
        public static bool IsOverridden => _override.HasValue;

        /// <summary>Reads whichever pointer is currently down.</summary>
        public static PointerSample Read()
        {
            if (_override.HasValue)
            {
                return _override.Value;
            }

            Touchscreen screen = Touchscreen.current;

            if (screen != null)
            {
                var touches = screen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var touch = touches[i];
                    if (touch.press.isPressed)
                    {
                        return new PointerSample(
                            true, touch.position.ReadValue(), touch.touchId.ReadValue());
                    }
                }

                // ⭐ D-138: in a browser a touchscreen is reported on many laptops that are being
                // played with a mouse, and returning here would make the mouse do nothing. So
                // on the web an idle touchscreen falls through to the mouse. ⛔ Phones unchanged.
                if (!Smiley.Web.WebPlatform.Active)
                {
                    return PointerSample.None;
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                return new PointerSample(true, mouse.position.ReadValue(), 0);
            }

            return PointerSample.None;
        }
    }
}
