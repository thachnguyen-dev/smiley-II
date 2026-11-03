# Smiley II

**A relaxing one-finger physics platformer for Android. Drag, let go, climb.**

[<img src="https://play.google.com/intl/en_us/badges/static/images/badges/en_badge_web_generic.png" height="60">](https://play.google.com/store/apps/details?id=com.thachnguyen.smiley)

<p>
<img src="https://play-lh.googleusercontent.com/gLCJeHD_Emiqe8WbihVgyURZa56zAnSS9pcWpvHLFPR01ah-3B6o5G0Uf6027lXFDb68hXO35OrhCiCdqv7XfQ=w526-h296" height="230">
<img src="https://play-lh.googleusercontent.com/JGN43agOB0BOsJSUjJ5fsAOmRJKTP5JL0-uYKtKnp_nw3kvt0iwm5fL2w7mZRxGlYAVKPhlPWIzV0dx-VkQ9OQ=w526-h296" height="230">
<img src="https://play-lh.googleusercontent.com/5hSef8aFE6em8oiJt8vlZfb4haniy7WdhCaddiAQ4XZdCJ2_AJExZEg2vnY7ZIQ51r5K5OCDTCm6fp4BTHmsIg=w526-h296" height="230">
</p>

Smiley is a soft, bouncy character climbing one tall, hand-crafted world. Touch anywhere, pull back like a
slingshot and release. There is no timer, no score and no game over: if you fall, you land lower and try again.

Designed, programmed, drawn and published by me alone, in **Unity 6 and C#**.

> This repository holds the **source code only** (scripts, editor tools and tests). The art, audio and scenes are
> not included, so the project does not open as a playable game. To play it, install it from Google Play.

## Highlights

**Physics does the playing.** A launch is one impulse given to a `Rigidbody2D`; the arc, bounces, rolling and
sliding all come from Unity's 2D physics, not from scripted movement. A trajectory guide shows where Smiley will
go while you aim.

**A soft body that is physical, not just drawn.** On impact, Smiley's collider really deforms (squash and
stretch), and its friction and bounce change with the shape: a flattened Smiley grips more and rebounds less. The
idle wobble, on the other hand, is visual only, so a resting Smiley never pushes himself around. His outline is a
deliberately uneven blob built from a fixed seed, so he rolls a little unevenly but always the same way.

**Three platform types:** solid grass, slippery ice, and clouds that fade out when you stand on them too long and
drift back later.

**Save that survives an app kill.** The game saves on meaningful events, not on a timer: when Smiley comes to rest,
when a cloud platform vanishes or returns, and when the app goes to the background. Player position and the state
of every platform are restored exactly. Fully playable offline.

**Android back gesture fix.** On test phones, a swipe from the screen edge closed the game in the middle of a drag.
Testing showed the cause was the back gesture (not home), which Android lets an app exclude, so the game reserves
the bottom edge strip through a native call.

**Monetisation that never blocks play:** one banner ad on the main menu only, with consent handling and a
parent gate (the game is rated for ages 3+), and a non-consumable Remove Ads purchase (Unity IAP 5). If the store is offline, the game simply carries on.

## Architecture

```
Assets/
  Scripts/                 runtime assembly: Smiley.Runtime
    Gameplay/              controller, launch solver, deformer, blob shape, platforms, map rules
    Flow/                  opening story → tutorial → climb → completion story → menu
    Save/                  event-based save service and save file format
    View/                  screens, UI kit, trajectory guide, idle animation, tutorial prompt
    Ads/                   ad service (LevelPlay), consent, Remove Ads purchase
    Audio/                 music, sound effects, settings
    Platform/              Android system gesture exclusion
    Web/                   browser build branch (web portal SDK, behind an interface)
    Config/                tuning values in a ScriptableObject
  Editor/                  16 editor tools
  Tests/
    Editor/                edit-mode tests
    PlayMode/              play-mode tests
```

- **Rules are plain C#, separate from Unity components.** Launch strength, drag stretch, the Ready rule, platform
  countdowns, the map rules and the completion flow live in classes with no Unity dependency. They are tested
  directly, and the `MonoBehaviour`s only connect them to the scene.
- **About 400 automated tests** (Unity Test Framework): 251 edit-mode and 152 play-mode.
- **One file per third-party API.** Only `StorePurchases.cs` touches Unity Purchasing, and only the ad service
  touches the ad SDK, so the rest of the game depends on small interfaces.
- **Editor tooling:** scene and menu builders, art and audio import, a map validator (`MapGuard`) that fails the
  build if the map cannot be played, a UI layout check, and one-click Android and iOS build commands.

## Tech

Unity 6 · C# · Rigidbody2D physics · Unity Test Framework · Unity IAP 5 · LevelPlay ads · Android native interop

## About

Made by **Nguyen Ngoc Thach**, Unity developer and game development student at VTC Academy.
I wrote this code with Claude Code as an AI pair programmer, while I designed the game, made the decisions and
tested it on real devices.
More: [github.com/thachnguyen-dev](https://github.com/thachnguyen-dev) ·
[my games on Google Play](https://play.google.com/store/apps/developer?id=the%20stone)
