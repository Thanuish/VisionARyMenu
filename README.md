# VisionARyMenu

An Android AR app that lets you point your phone at a restaurant menu and see
dishes rendered in 3D, matched against a food catalog with allergen and diet
tags. Built in Unity with AR Foundation / ARCore.

This is a from-scratch rebuild (Phase 1) of an earlier menu-viewer prototype
(`MenuViewer`) — the namespaces still say `MenuViewer` in places while the
rename settles.

## What's real vs. what's simulated (Phase 1 honesty check)

| Piece | State |
|---|---|
| AR plane detection & surface placement (ARFoundation/ARCore, raycasting, anchors) | ✅ Working, unit-tested (`ArPlanePlacementTests`) |
| Food catalog + matcher + match stabilizer (debounces flicker across OCR frames) | ✅ Working, unit-tested (`FoodMatcherTests`, `FoodMatchStabilizerTests`) |
| Allergen/diet tagging (`AllergyTag`, `DietTag`, `UserAllergenProfile`) | ✅ Working, unit-tested |
| Procedural AR food model + anchoring + tap-to-select | ✅ Working, unit-tested (`MenuTapSelectionTests`) |
| OCR (reading text off a real camera frame) | 🚧 **Simulated.** `FakeOcrService` emits a hardcoded demo menu on a timer — there is no real on-device text recognition wired in yet. `IOcrService` is the abstraction a real OCR backend (e.g. ML Kit) would implement. |
| QR-code menu entry point | 🚧 Referenced in code comments as living on a `feature/qr-scan-menu` branch; not present in this Phase-1 snapshot. Current entry point is a built-in demo menu list. |

The point of listing it this way: the AR placement and food-matching core —
the parts that are genuinely non-trivial — are real and tested. The
camera-to-text step is currently a stand-in so the rest of the pipeline could
be built and tested without a working OCR backend blocking progress.

## Stack

- Unity 6000.x, AR Foundation 6.4, ARCore XR Plugin 6.4
- C#, Unity Test Framework (EditMode + PlayMode tests)

## Project layout

```
Assets/VisionARyMenu/
├── Runtime/
│   ├── AR/           surface placement, plane visualization, camera pose, AR anchoring
│   ├── Food/          catalog, matcher, stabilizer, allergen/diet tags, detection pipeline
│   ├── Ocr/            OCR abstraction (IOcrService) + result/line types
│   ├── Simulation/    FakeOcrService — demo data generator standing in for real OCR
│   ├── QrScan/         QR scan service + menu flow controller
│   ├── UI/             QR scan screen, menu list screen
│   └── Bootstrap/     runtime scene assembly (VisionARyBootstrap)
├── Editor/            project setup utilities
└── Tests/
    ├── EditMode/       7 test suites covering matching, stabilization, tagging, tap selection
    └── PlayMode/       menu detection pipeline integration test
```

## Running it

Open in Unity 6000.x, load `Assets/Scenes/VisionARyMenu.unity`, build for
Android (AR Foundation requires a physical device — the editor won't show AR
tracking). On launch it boots with an empty catalog, loads a built-in demo
menu, and lets you tap a dish to run the AR placement flow against simulated
OCR output.

## Next

- Real OCR backend behind `IOcrService` (ML Kit or similar) to replace `FakeOcrService`
- Merge/rebuild the QR-scan entry point
- Real menu catalogs via `RemoteMenuLoader` instead of the built-in demo list
