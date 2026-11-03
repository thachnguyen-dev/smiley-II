using Smiley.Config;
using Smiley.Gameplay;
using UnityEngine;

namespace Smiley.View
{
    /// <summary>
    /// §7's trajectory guide: launch direction and a short portion of the flight, drawn as a
    /// run of circular dots that fade out along the arc.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Dots rather than a line, by owner instruction 2026-09-10.</b> They fade along the
    /// predicted path, so the guide reads as <i>"this way, about this hard"</i> and trails off
    /// rather than terminating at a point the player might read as a destination.
    /// </para>
    /// <para>
    /// ⛔ <b>§7 forbids showing the whole trajectory</b>, so the preview is bounded by
    /// <see cref="SmileyTuning.GuideSeconds"/>. That bound is the rule, not a budget: raising it
    /// far enough breaks §7 rather than merely looking different. ⓘ The fade reinforces it —
    /// the last dot is nearly invisible, so the guide has no clear end to aim with.
    /// </para>
    /// <para>
    /// ⚠ <b>The guide is NOT the line from Smiley to the finger</b>, which follows from D-009:
    /// force is finger travel from the touch-down point, so the finger's distance from Smiley
    /// says nothing about how hard the launch will be. The dots start at Smiley and follow the
    /// solved launch.
    /// </para>
    /// <para>
    /// ⓘ The preview integrates gravity only and deliberately does not predict collisions. §7
    /// grants a <i>short portion</i>; stopping the dots at the first platform would answer
    /// "where will I land", which is the question the player is meant to judge.
    /// </para>
    /// </remarks>
    public sealed class TrajectoryGuide : MonoBehaviour
    {
        [SerializeField] private SmileyTuning _tuning;

        /// <summary>The dot sprite. Placeholder art; §32 reserves the real thing to the owner.</summary>
        [SerializeField] private Sprite _dotSprite;

        private SpriteRenderer[] _dots;

        /// <summary>How many dots are currently drawn. For tests.</summary>
        public int VisibleDotCount
        {
            get
            {
                if (_dots == null)
                {
                    return 0;
                }

                int count = 0;
                foreach (SpriteRenderer dot in _dots)
                {
                    if (dot != null && dot.enabled)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void Awake()
        {
            BuildDots();
            Hide();
        }

        private void BuildDots()
        {
            int count = _tuning != null ? Mathf.Max(2, _tuning.GuidePoints) : 12;
            _dots = new SpriteRenderer[count];

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Dot{i:00}");
                go.transform.SetParent(transform, false);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _dotSprite;
                renderer.sortingOrder = 20;
                _dots[i] = renderer;
            }
        }

        /// <summary>Draws the guide for a launch in progress.</summary>
        /// <param name="solution">The launch the current drag describes.</param>
        /// <param name="origin">Smiley's position — where the arc starts.</param>
        /// <param name="mass">Smiley's mass, to turn the impulse into a velocity.</param>
        public void Show(LaunchSolution solution, Vector2 origin, float mass)
        {
            if (_dots == null || _tuning == null)
            {
                return;
            }

            if (!solution.HasAim || solution.Force <= 0f || mass <= 0f)
            {
                Hide();
                return;
            }

            // An impulse is a change in momentum, so the launch velocity is force / mass.
            Vector2 velocity = solution.Direction * (solution.Force / mass);
            Vector2 gravity = Physics2D.gravity;

            // ⚠ SPACED BY DISTANCE ALONG THE ARC, NOT BY TIME.
            //
            // Sampling at equal time steps puts the dots wherever the speed happens to put
            // them: a gentle launch bunches them all inside Smiley's own width, where they
            // overlap into a smear rather than reading as the sequence of dots this is meant
            // to be. Walking the arc and dropping a dot every fixed distance keeps them
            // discrete and evenly spaced at any force — and a weak launch simply gets a
            // shorter trail, which is honest feedback rather than a rendering artefact.
            const int Samples = 96;
            float dt = _tuning.GuideSeconds / Samples;

            float startAt = _tuning.BodyRadius + _tuning.GuideDotSize;
            float spacing = Mathf.Max(0.01f, _tuning.GuideDotSpacing);

            Vector2 previous = origin;
            float travelled = 0f;
            float nextDotAt = startAt;
            int placed = 0;

            for (int s = 1; s <= Samples && placed < _dots.Length; s++)
            {
                float t = dt * s;
                Vector2 p = origin + velocity * t + 0.5f * gravity * (t * t);

                travelled += Vector2.Distance(previous, p);
                previous = p;

                while (travelled >= nextDotAt && placed < _dots.Length)
                {
                    PlaceDot(placed, p);
                    nextDotAt += spacing;
                    placed++;
                }
            }

            // Any dots the arc was too short to reach stay hidden.
            for (int i = placed; i < _dots.Length; i++)
            {
                _dots[i].enabled = false;
            }
        }

        private void PlaceDot(int index, Vector2 position)
        {
            SpriteRenderer dot = _dots[index];
            dot.transform.position = new Vector3(position.x, position.y, 0f);

            // Fading out along the path, and shrinking with it: the tail should suggest a
            // direction rather than mark a destination.
            float along = _dots.Length > 1 ? index / (float)(_dots.Length - 1) : 0f;
            float fade = 1f - along;

            float size = _tuning.GuideDotSize * Mathf.Lerp(1f, _tuning.GuideDotTaper, along);
            dot.transform.localScale = new Vector3(size, size, 1f);

            Color colour = _tuning.GuideDotColour;
            colour.a *= fade * fade;   // squared, so the tail disappears sooner than the head
            dot.color = colour;

            dot.enabled = true;
        }

        /// <summary>
        /// Removes the guide. §7: it disappears <b>immediately</b> when the player releases.
        /// </summary>
        public void Hide()
        {
            if (_dots == null)
            {
                return;
            }

            foreach (SpriteRenderer dot in _dots)
            {
                if (dot != null)
                {
                    dot.enabled = false;
                }
            }
        }
    }
}
