using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// §19's one Top Checkpoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §19: <i>"There is exactly one Top Checkpoint… Touching the Top Checkpoint starts the
    /// completion flow."</i> No intermediate checkpoints, and no requirement to use checkpoints
    /// during the map.
    /// </para>
    /// <para>
    /// ⛔ <b>IT IS A TRIGGER, NOT A SOLID, AND THAT IS A READING — see Q-044.</b> §19 says
    /// "touching" and never says what the thing is physically. A <b>solid</b> checkpoint would
    /// be a surface Smiley lands on, bounces off and rests upon: that is a fourth kind of
    /// platform, and §14 enumerates exactly three. §16 also fixes the world's contents as
    /// <i>"only Smiley and Platforms"</i> and forbids introducing anything else. A trigger is
    /// the only reading that adds no physics — it changes nothing about how Smiley moves, which
    /// is what lets §19 be built without inventing a mechanic §14 and §16 do not have.
    /// </para>
    /// <para>
    /// ⚠ <b>It reports the touch; it does not decide what happens.</b> The flow belongs to
    /// <see cref="Smiley.Flow.CompletionSequence"/>, which owns §19's ordering and §20's rule
    /// that the Story cannot be skipped. This component's whole job is to notice Smiley.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TopCheckpoint : MonoBehaviour
    {
        /// <summary>Raised the first time Smiley touches it, and every time after.</summary>
        public event System.Action Touched;

        /// <summary>Whether Smiley has ever touched it this run. For tests.</summary>
        public bool HasBeenTouched { get; private set; }

        private void Reset()
        {
            // Only meaningful when a human adds the component; the scene is generated.
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void Awake()
        {
            // ⛔ Enforced rather than trusted. A checkpoint whose collider lost its trigger flag
            // would become a solid ledge at the top of the map — Smiley would land on it, and
            // §19's flow would still fire, so the defect would look like a working checkpoint
            // that Smiley happens to be standing on.
            var collider = GetComponent<Collider2D>();

            if (!collider.isTrigger)
            {
                // ⛔ AN ERROR, NOT A WARNING, AND IT STILL FIXES THE COLLIDER.
                //
                // Repairing it keeps the built game safe; saying so loudly is what stops the
                // repair from HIDING the defect. A warning would let a builder that ships a
                // solid checkpoint stay green forever, because the component would quietly put
                // it right every run — this project has been caught by exactly that shape
                // (D-018), and its rule is that a guard fails rather than warns.
                Debug.LogError(
                    "[CHECKPOINT] The Top Checkpoint's collider was built SOLID. Forcing it to " +
                    "a trigger so the game is playable, but the scene builder is wrong: a solid " +
                    "checkpoint is a fourth kind of platform, and §14 has three.");

                collider.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // ⚠ Only Smiley. Nothing else exists to touch it today (§16), but the rule should
            // not depend on that staying true — the same reasoning as §14.3's contact test.
            if (other.GetComponentInParent<SmileyController>() == null)
            {
                return;
            }

            HasBeenTouched = true;
            Touched?.Invoke();
        }
    }
}
