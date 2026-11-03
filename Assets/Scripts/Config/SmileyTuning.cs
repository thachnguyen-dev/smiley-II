using UnityEngine;

namespace Smiley.Config
{
    /// <summary>
    /// Every tunable number the drag-and-launch loop needs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>ALMOST EVERY VALUE HERE IS PROVISIONAL AND MINE, NOT AN OWNER DECISION.</b> §34
    /// forbids assuming a value marked TBD; the project's rule is that a value blocks
    /// <i>tuning</i>, not <i>building</i>, so the system is built with the number exposed as
    /// configurable data and obviously unsettled. Each field names the open question it belongs
    /// to. ⚠ <b>A field marked PROVISIONAL may never be cited, quoted or recorded as something
    /// the owner chose.</b>
    /// </para>
    /// <para>
    /// ⭐ <b>The exceptions are marked OWNER-AUTHORED, and they are not to be retuned.</b> As of
    /// 2026-09-10 there is one: <see cref="DisappearCountdownSeconds"/>. ⚠ Authored is not the
    /// same as <i>declared final</i> — only the owner can do that, and they have not.
    /// </para>
    /// <para>
    /// When the owner rules on a value, the ruling gets a numbered decision, the question moves
    /// to CLOSED in <c>design/gdd/open-questions.md</c>, and the tooltip here stops saying
    /// PROVISIONAL. ⓘ <b>A tooltip that still claims to be a guess after the owner has ruled is
    /// how a real decision gets quietly retuned by the next person.</b>
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "SmileyTuning", menuName = "Smiley/Tuning", order = 0)]
    public sealed class SmileyTuning : ScriptableObject
    {
        [Header("Drag & launch — §6")]
        [Tooltip("PROVISIONAL (Q-006). Maximum Drag Distance as a FRACTION OF SCREEN HEIGHT, " +
                 "not pixels: §33 requires that gameplay does not change with the display, and a " +
                 "pixel distance would mean a different drag on every resolution.")]
        [Range(0.05f, 0.9f)]
        public float MaxDragScreenFraction = 0.25f;

        [Tooltip("PROVISIONAL (Q-007). Impulse at full charge. The distance-to-force mapping is " +
                 "linear, which is also provisional — §6 fixes only that distance determines force.")]
        public float MaxLaunchForce = 12f;

        [Header("Ready — §8")]
        [Tooltip("PROVISIONAL (Q-005). Total speed below which Smiley becomes Ready, in units/sec. " +
                 "§8 allows no stabilisation delay, so this threshold is the whole of the rule.")]
        public float StoppingThreshold = 0.25f;

        [Header("Trajectory guide — §7")]
        [Tooltip("PROVISIONAL (Q-033). How many seconds of flight the guide previews. §7 requires a " +
                 "SHORT PORTION and forbids showing the whole trajectory, so this is deliberately " +
                 "small — raising it far would break the rule, not just the feel.")]
        [Range(0.05f, 1.0f)]
        public float GuideSeconds = 0.35f;

        [Tooltip("PROVISIONAL. How many dots the guide draws along the previewed arc. " +
                 "Presentation only.")]
        [Range(2, 64)]
        public int GuidePoints = 14;

        [Tooltip("PROVISIONAL. Diameter of the first guide dot, in world units.")]
        [Range(0.02f, 0.6f)]
        public float GuideDotSize = 0.15f;

        [Tooltip("PROVISIONAL. Gap between guide dots along the arc, in world units. " +
                 "⚠ Spacing is by DISTANCE, not time: equal time steps bunch the dots together " +
                 "at low force until they overlap into a smear instead of reading as dots.")]
        [Range(0.05f, 1f)]
        public float GuideDotSpacing = 0.34f;

        [Tooltip("PROVISIONAL. Size of the LAST dot relative to the first. Below 1 tapers the " +
                 "trail away, which — with the alpha fade — is what keeps §7's 'short portion' " +
                 "from reading as a target marker.")]
        [Range(0.1f, 1f)]
        public float GuideDotTaper = 0.45f;

        [Tooltip("PROVISIONAL. Guide dot colour. ⚠ Presentation, and §32 reserves the visual " +
                 "direction to the owner — this is a placeholder so the dots can be seen.")]
        public Color GuideDotColour = new Color(1f, 1f, 1f, 0.85f);

        [Header("Platform types — §14.2, §14.3")]
        [Tooltip("PROVISIONAL (Q-004). Slippery Platform friction. §14.2 requires only that it is " +
                 "LOWER than Normal, so Smiley slides farther and decelerates more slowly. " +
                 "⛔ It does not modify Launch Force.")]
        [Range(0f, 1f)]
        public float SlipperyFriction = 0.02f;

        [Tooltip("OWNER-AUTHORED 2026-09-10 (Q-008): the platform disappears after 3 seconds of " +
                 "stable contact. ⛔ Not one of my provisional numbers — do not retune it. " +
                 "⚠ Authored is not the same as declared final; only the owner can do that.")]
        [Range(0.5f, 20f)]
        public float DisappearCountdownSeconds = 3f;

        [Tooltip("PROVISIONAL (Q-009). How long a vanished platform stays gone before it " +
                 "respawns. ⓘ It comes back with its FULL countdown (D-011).")]
        [Range(0.5f, 20f)]
        public float RespawnDelaySeconds = 3f;

        [Tooltip("OWNER-AUTHORED 2026-09-13 (D-117): after this long OFF a Disappearing platform, its " +
                 "countdown recovers in full. Within the window, leaving pauses and returning " +
                 "resumes (§14.3). ⛔ Not one of my provisional numbers — do not retune it.")]
        [Range(0f, 20f)]
        public float DisappearRecoverSeconds = 2f;

        [Tooltip("PROVISIONAL (Q-039). How long contact must hold before it counts as STABLE " +
                 "and starts the countdown. §14.3: a brief touch while airborne must not " +
                 "activate it, so distinguishing a graze from landing needs a duration.")]
        [Range(0.01f, 1f)]
        public float StableContactSeconds = 0.15f;

        [Tooltip("PROVISIONAL. How far through the countdown the near-expiry cue begins, 0-1. " +
                 "⛔ §14.3 allows animation and VFX only — never a numerical countdown, never SFX.")]
        [Range(0f, 1f)]
        public float NearExpiryFrom = 0.55f;

        [Header("Rotation — §13")]
        [Tooltip("PROVISIONAL (Q-040). Roughly how long Smiley takes to return to upright after " +
                 "rolling stops. ⛔ §13 says the return must NOT snap, so this may never be zero — " +
                 "an instant return satisfies every other clause of §13 and violates that one.")]
        [Range(0.05f, 3f)]
        public float UprightSettleSeconds = 0.45f;

        [Tooltip("PROVISIONAL (Q-047). Rolling resistance, as angular damping on the body. " +
                 "⭐ Smiley is a soft water balloon (§11) and a soft body loses real energy to " +
                 "deformation while it rolls — a beach ball stops far sooner than a ball " +
                 "bearing. Box2D models no rolling resistance at all, so without this he coasts " +
                 "until something stops him. ⛔ It is a PROPERTY OF THE BODY, like friction and " +
                 "linear damping. It computes no roll distance and reads no landing angle: how " +
                 "far he rolls still emerges from the forces acting on him.")]
        [Range(0f, 30f)]
        public float RollingResistance = 6f;

        [Header("Drag stretch — §11, §12, VISUAL ONLY")]
        [Tooltip("PROVISIONAL (Q-043). How hard a full-strength drag squeezes the artwork. " +
                 "⭐ Owner instruction 2026-09-10: while being dragged Smiley becomes SHORTER " +
                 "along the drag and WIDER across it, like a water balloon being squeezed. " +
                 "⛔ VISUAL ONLY — it never reaches the collider, the material or the body, " +
                 "exactly as §12's idle wobble does not.")]
        [Range(0f, 1f)]
        public float DragStretchAtFullDrag = 0.28f;

        [Tooltip("PROVISIONAL (Q-043). The hard ceiling on the drag squeeze, whatever the drag. " +
                 "⭐ The owner asked for the deformation to be limited to a maximum amount, so " +
                 "this is a stated ceiling rather than a consequence of the mapping above.")]
        [Range(0f, 1f)]
        public float DragStretchMaximum = 0.35f;

        [Tooltip("PROVISIONAL. How quickly the drag squeeze follows the finger, in seconds. " +
                 "Above zero so that starting and ending a drag does not snap the artwork.")]
        [Range(0f, 0.5f)]
        public float DragStretchSeconds = 0.06f;

        [Header("Audio — §27")]
        [Tooltip("PROVISIONAL (Q-048). The contact impulse that plays a collision at full " +
                 "volume. §27: SFX volume depends on impact force — light impact quieter, " +
                 "strong impact louder — and this is where 'strong' is. ⚠ Anything above it is " +
                 "simply loud; the scale does not keep going.")]
        [Range(1f, 60f)]
        public float LoudestImpulse = 14f;

        [Tooltip("PROVISIONAL (Q-048). Below this volume a collision makes no sound at all. " +
                 "⛔ It exists because §11's outline is deliberately lumpy, so Smiley MICRO-" +
                 "BOUNCES on his own bumps for a second or two after landing — each one a real " +
                 "collision, and together a rattle from a character standing still. §27 forbids " +
                 "rolling SFX, and this is that rule arriving by a route it does not name. " +
                 "⚠ Too high and gentle landings go silent, which §27 also forbids.")]
        [Range(0f, 0.5f)]
        public float QuietestAudibleImpact = 0.12f;

        [Tooltip("PROVISIONAL (Q-048). How loud the launch is at no charge, relative to a full " +
                 "one. §27 says only that launch HAS SFX; that a feeble launch should be a " +
                 "quieter sound than a hard one is mine.")]
        [Range(0f, 1f)]
        public float LaunchQuietest = 0.35f;

        [Tooltip("PROVISIONAL (Q-048). The trajectory guide's tone at no drag, as a pitch " +
                 "multiplier. §27: the guide has sound feedback and it CHANGES WITH DRAG FORCE.")]
        [Range(0.2f, 2f)]
        public float GuidePitchLowest = 0.7f;

        [Tooltip("PROVISIONAL (Q-048). The guide's tone at a full drag. ⚠ Above the lowest, so " +
                 "winding up reads as rising — a falling tone would read as letting go.")]
        [Range(0.2f, 3f)]
        public float GuidePitchHighest = 1.6f;

        [Tooltip("PROVISIONAL (Q-048). How loud the guide tone is before §26's SFX setting is " +
                 "applied. ⚠ It sounds for the whole of every aim, so it is deliberately well " +
                 "below the one-shot cues.")]
        [Range(0f, 1f)]
        public float GuideCueVolume = 0.25f;

        [Header("Camera — §17, D-010, D-016")]
        [Tooltip("PROVISIONAL (Q-010). How quickly the camera settles onto Smiley during normal " +
                 "play, in seconds. An exponential approach, so it is frame-rate independent — a " +
                 "per-frame lerp would make the camera faster on a fast device, which §33 rules out.")]
        [Range(0.01f, 1.5f)]
        public float FollowSmoothing = 0.14f;

        [Tooltip("PROVISIONAL (Q-011). How far ABOVE Smiley the view sits, in world units. The " +
                 "game is a climb (§1), so the interesting space is the one he is heading into.")]
        [Range(-3f, 3f)]
        public float CameraFraming = 0.9f;

        [Tooltip("PROVISIONAL (Q-032). Half the width of the band Smiley may move within before " +
                 "the camera pans horizontally. ⚠ §17 makes VERTICAL follow continuous and D-016 " +
                 "makes HORIZONTAL follow a deadzone — the axes differ deliberately, because §9 " +
                 "has him rolling after almost every landing and tracking X continuously would " +
                 "slide the world under a character who is never quite still.")]
        [Range(0.1f, 3f)]
        public float CameraDeadzoneHalfWidth = 0.85f;

        [Header("Shape — §11")]
        [Tooltip("PROVISIONAL. Smiley's radius in world units, before deformation.")]
        public float BodyRadius = 0.5f;

        [Tooltip("PROVISIONAL. Points around the rim of the collider and the blob outline. " +
                 "More is rounder and costlier — every one is rebuilt when the shape deforms.")]
        [Range(6, 32)]
        public int BlobVertices = 16;

        [Tooltip("PROVISIONAL (Q-036). How far the outline strays from a perfect circle, as a " +
                 "fraction of the radius. §11 says Smiley is NOT a mathematically perfect circle " +
                 "and is naturally and slightly asymmetrical, and the owner ruled that this " +
                 "reaches the PHYSICS — so this value changes how he rolls, not just how he looks.")]
        [Range(0f, 0.2f)]
        public float BlobAsymmetry = 0.06f;

        [Tooltip("Fixes the asymmetry. ⛔ Deterministic on purpose: a re-rolled shape would mean " +
                 "two players are rolling physically different characters, and would make a " +
                 "reported feel problem impossible to reproduce.")]
        public int BlobSeed = 20260910;

        [Header("Camera — owner 2026-09-12, D-091, D-099, D-112")]
        [Tooltip("⭐ OWNER-SET (D-112, 2026-09-13): 8.5. How much of the world the camera shows ACROSS, " +
                 "in units, on any phone. The framing is set by width and the height follows the " +
                 "phone's aspect (D-099). Was 7.5 (mine, Q-054), 5.85 after D-091, 4.5 at the " +
                 "reference. The map is 27 wide. ⛔ Changing this does not change the map or the " +
                 "physics: MapRule's 240 px/unit is the authoring conversion, not this camera's.")]
        [Range(3f, 27f)]
        public float CameraVisibleWidth = 8.5f;

        [Tooltip("PROVISIONAL (Q-060). WEB ONLY (D-140): how much of the world the browser camera " +
                 "shows UP AND DOWN, in units. Derived, not chosen: 8.5 across on the 1080 × 2400 " +
                 "reference phone is 18.9 tall, so a browser shows what a phone shows vertically. " +
                 "⛔ Never wider than the map — on a wide window the map's width wins and the view " +
                 "gets shorter instead. ⛔ Never narrower than CameraVisibleWidth. Phones ignore this.")]
        [Range(5f, 40f)]
        public float WebVisibleHeight = 18.9f;

        [Header("Facing — owner 2026-09-12, D-095")]
        [Tooltip("PROVISIONAL (Q-055). Sideways speed, in units per second, beyond which the picture " +
                 "turns to face the way he is moving. Below it he keeps facing the way he was, so " +
                 "settling on a platform does not twitch him left and right.")]
        [Range(0f, 5f)]
        public float FacingSpeedThreshold = 0.3f;

        [Header("Shadow — owner 2026-09-11, D-083")]
        [Tooltip("PROVISIONAL (Q-052). How far above a platform Smiley can be and still cast a " +
                 "shadow on it, in world units. Beyond this the shadow is gone.")]
        [Range(1f, 30f)]
        public float ShadowRange = 6f;

        [Tooltip("PROVISIONAL (Q-052). The shadow's size at the top of the range, as a fraction " +
                 "of its size on the ground.")]
        [Range(0.05f, 1f)]
        public float ShadowMinScale = 0.35f;

        [Tooltip("PROVISIONAL (Q-052). How dark the shadow is when Smiley is on the platform.")]
        [Range(0f, 1f)]
        public float ShadowAlphaNear = 0.45f;

        [Tooltip("PROVISIONAL (Q-052). How dark the shadow is at the top of the range.")]
        [Range(0f, 1f)]
        public float ShadowAlphaFar = 0.08f;

        [Tooltip("PROVISIONAL (Q-052). The shadow's width on the ground, as a fraction of " +
                 "Smiley's diameter.")]
        [Range(0.2f, 2f)]
        public float ShadowWidth = 0.9f;

        [Tooltip("PROVISIONAL (Q-052). The shadow's height on the ground, as a fraction of " +
                 "Smiley's diameter. Flat, because it lies on the platform.")]
        [Range(0.05f, 1f)]
        public float ShadowHeight = 0.28f;

        [Tooltip("⭐ Owner 2026-09-11 (D-085): the shadow sits slightly to the RIGHT of Smiley's " +
                 "centre, as if lit from the upper left. This is how far, as a fraction of his " +
                 "diameter. PROVISIONAL amount (Q-052); the direction is the owner's.")]
        [Range(-1f, 1f)]
        public float ShadowOffsetX = 0.18f;

        [Header("Deformation — §11, §12")]
        [Tooltip("PROVISIONAL. How far the squash is centred toward the CONTACT POINT rather " +
                 "than the body's centre. 0 squashes about the middle, 1 squashes into the " +
                 "contact itself. ⭐ Owner instruction 2026-09-10: deforming about the centre is " +
                 "not physically appropriate — landing from above should push the deformation " +
                 "centre downward toward where Smiley actually touched.")]
        [Range(0f, 1f)]
        public float ContactPivotBias = 0.85f;

        [Tooltip("PROVISIONAL. Squash produced per unit of collision impulse. §11: deformation " +
                 "depends on impact force — strong force, larger deformation. ⭐ Raised sharply on " +
                 "owner instruction 2026-09-10: Smiley should read as a soft water balloon, not " +
                 "a rigid object.")]
        public float ImpactToSquash = 0.16f;

        [Tooltip("PROVISIONAL. How much of Smiley's own resting weight is ignored before a " +
                 "contact counts as an impact. ⛔ Without this, a Smiley standing still is " +
                 "permanently squashed by his own weight — which contradicts §12's 'stopped " +
                 "returns to the natural base shape' and its rule that a stationary Smiley's " +
                 "deformation is visual only. Above 1 to absorb solver noise.")]
        [Range(1f, 4f)]
        public float RestingImpulseTolerance = 1.5f;

        [Tooltip("PROVISIONAL (Q-034). Roughly how long an unforced return to the base shape " +
                 "takes. §11: when the force ends, deformation GRADUALLY returns.")]
        [Range(0.02f, 2f)]
        public float RecoverySeconds = 0.26f;

        [Tooltip("PROVISIONAL (Q-035). Stability clamp. ⚠ §11 says there is NO fixed gameplay " +
                 "maximum deformation and permits a technical limit only to prevent physics " +
                 "instability, visual errors or abnormally large values. This is that limit and " +
                 "nothing more — it is not a design ceiling.")]
        [Range(0.1f, 0.95f)]
        public float TechnicalLimit = 0.75f;

        [Tooltip("PROVISIONAL. Squash produced per unit of rolling speed. §12: higher speed, " +
                 "greater deformation; stopped, back to the base shape.")]
        public float RollingSpeedToSquash = 0.045f;

        [Tooltip("PROVISIONAL. How much Smiley bulges across the squash axis. ⓘ §11 says total " +
                 "size may shrink or grow and that STRICT VOLUME CONSERVATION IS NOT REQUIRED, " +
                 "so this is free rather than derived: below 1 he loses size when squashed, " +
                 "above 1 he gains it.")]
        [Range(0f, 2f)]
        public float StretchRatio = 0.85f;

        [Tooltip("PROVISIONAL (Q-037). Friction multiplier at full squash. The owner's ruling was " +
                 "to make maximum use of physics, so contact properties follow the shape — a " +
                 "flatter body grips more. ⚠ §11 never mentions friction; this mapping is mine.")]
        [Range(1f, 4f)]
        public float SquashFrictionGain = 1.6f;

        [Header("Idle deformation — §12, VISUAL ONLY")]
        [Tooltip("PROVISIONAL. Amplitude of the resting wobble. ⛔ §12: idle deformation is " +
                 "VISUAL ONLY and DOES NOT AFFECT PHYSICS — this value never reaches the collider.")]
        [Range(0f, 0.15f)]
        public float IdleAmplitude = 0.035f;

        [Tooltip("PROVISIONAL. Cycles per second of the resting wobble. Visual only.")]
        [Range(0.1f, 3f)]
        public float IdleFrequency = 0.6f;

        [Header("Body — §9")]
        [Tooltip("PROVISIONAL (Q-002). Mass is constant for the entire game (§9, §11).")]
        public float Mass = 1f;

        [Tooltip("PROVISIONAL. Linear damping on the body. §9 gives no such term explicitly; it is " +
                 "here because a 2D body with only surface friction never quite settles in the air.")]
        public float LinearDamping = 0.05f;

        [Tooltip("PROVISIONAL (Q-013). §9 fixes 'very little bounce' as a principle; the figure is " +
                 "not authored. Impact energy is meant to go into deformation (§11), which this " +
                 "slice does not build.")]
        [Range(0f, 1f)]
        public float Bounciness = 0.05f;

        [Tooltip("PROVISIONAL (Q-003). Normal Platform friction (§14.1). Slippery (§14.2) is a " +
                 "separate platform type and is not built in this slice.")]
        [Range(0f, 1f)]
        public float NormalFriction = 0.4f;

        /// <summary>
        /// §6's Maximum Drag Distance in pixels, for the screen this frame.
        /// </summary>
        /// <remarks>
        /// Derived from screen height rather than stored, so the same drag means the same
        /// force on every device — which is what §33 requires. ⚠ This makes the drag
        /// resolution-independent, <b>not</b> physically identical: a large phone and a small
        /// one both need a quarter-screen drag, which covers different numbers of millimetres.
        /// A physical (DPI-based) reading was offered and not chosen.
        /// </remarks>
        public float MaxDragPixels(float screenHeight) => MaxDragScreenFraction * screenHeight;
    }
}
