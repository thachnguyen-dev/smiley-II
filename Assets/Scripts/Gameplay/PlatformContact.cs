using System.Collections.Generic;
using UnityEngine;

namespace Smiley.Gameplay
{
    /// <summary>
    /// Tracks which Platforms Smiley is currently touching.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Half of §8's Ready condition, and the half that cannot be answered from Smiley's own
    /// state. Kept as a set of the actual platforms rather than a boolean because §14.2 will
    /// need to know <i>which</i> surface is under him the instant he reaches it.
    /// </para>
    /// <para>
    /// ⚠ <b>Nulls are pruned on read, not assumed away.</b> §14.3's Disappearing Platform is
    /// destroyed underneath Smiley by design, and a destroyed collider may never deliver its
    /// exit callback. A stale entry would report contact with a platform that no longer
    /// exists — and would therefore report Ready in mid-air.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlatformContact : MonoBehaviour
    {
        private readonly HashSet<Platform> _touching = new HashSet<Platform>();

        /// <summary>Whether Smiley is in contact with at least one Platform.</summary>
        public bool IsTouchingPlatform
        {
            get
            {
                Prune();
                return _touching.Count > 0;
            }
        }

        /// <summary>How many distinct Platforms are in contact. Useful in a corner (§10).</summary>
        public int ContactCount
        {
            get
            {
                Prune();
                return _touching.Count;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            var platform = collision.collider.GetComponentInParent<Platform>();
            if (platform != null)
            {
                _touching.Add(platform);
            }
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            var platform = collision.collider.GetComponentInParent<Platform>();
            if (platform != null)
            {
                _touching.Remove(platform);
            }
        }

        private void Prune()
        {
            if (_touching.Count == 0)
            {
                return;
            }

            _touching.RemoveWhere(p => p == null);
        }
    }
}
