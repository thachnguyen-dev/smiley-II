using System.IO;
using NUnit.Framework;

namespace Smiley.Tests
{
    /// <summary>
    /// The Google Play Billing prerequisite: the APK/AAB must carry
    /// <c>com.android.vending.BILLING</c>, or no purchase can ever be made (D-124).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⓘ <b>Nothing declares that permission by hand, and nothing should.</b> It arrives in the
    /// merged manifest from the Play Billing AAR, which Unity injects because
    /// <c>com.unity.purchasing</c> is installed — verified on 2026-10-01 by reading the built
    /// APK's own manifest, where it appears beside <c>ProxyBillingActivity</c> and
    /// <c>com.google.android.play.billingclient.version</c>. ⛔ A hand-written
    /// <c>Assets/Plugins/Android/AndroidManifest.xml</c> would be worse than useless here: a
    /// custom main manifest REPLACES Unity's generated one and must then carry everything else
    /// too, so adding one to gain a line costs the rest of the file.
    /// </para>
    /// <para>
    /// ⚠ <b>So the permission rests on two things that a person can quietly delete</b>, and this
    /// fixture is both of them. The project uses a CUSTOM <c>mainTemplate.gradle</c>; the
    /// <c>**DEPS**</c> token in it is where Unity writes every injected dependency — billing and
    /// the ad SDK alike. Delete the token, or the package, and the build still succeeds, the
    /// tests still pass, and purchases simply never work on a phone.
    /// </para>
    /// </remarks>
    public sealed class BillingPrerequisiteTests
    {
        private const string Manifest = "Packages/manifest.json";
        private const string GradleTemplate = "Assets/Plugins/Android/mainTemplate.gradle";

        [Test]
        public void TheInAppPurchasingPackageIsInstalled()
        {
            string json = File.ReadAllText(Manifest);

            Assert.That(json, Does.Contain("com.unity.purchasing"),
                "the IAP package is gone, so Unity injects no billing library and the APK would " +
                "carry no com.android.vending.BILLING permission (D-124)");
        }

        [Test]
        public void TheGradleTemplateStillAcceptsUnitysInjectedDependencies()
        {
            // ⛔ Without this token Unity has nowhere to write com.android.billingclient:billing
            // — nor the LevelPlay dependencies. The build succeeds either way; the phone does not.
            string gradle = File.ReadAllText(GradleTemplate);

            Assert.That(gradle, Does.Contain("**DEPS**"),
                $"{GradleTemplate} no longer carries the **DEPS** token, so no injected Android " +
                "dependency reaches the build — billing and the ad SDK both vanish silently");
        }

        [Test]
        public void TheProjectDoesNotOwnACustomMainManifest()
        {
            // ⚠ If one is ever added deliberately, this fixture is the place to say so — and the
            // permission must then be declared in it by hand, because Unity's generated manifest
            // is replaced rather than merged.
            Assert.That(File.Exists("Assets/Plugins/Android/AndroidManifest.xml"), Is.False,
                "a custom main manifest now exists; it REPLACES Unity's, so it must declare " +
                "com.android.vending.BILLING and everything else Unity used to generate");
        }
    }
}
