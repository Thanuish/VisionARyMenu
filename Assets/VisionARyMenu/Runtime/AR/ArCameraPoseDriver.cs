using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARSubsystems;

namespace MenuViewer.AR
{
    /// <summary>
    /// Drives the AR camera transform from AR Foundation's <see cref="HandheldARInputDevice"/>
    /// pose. This is a cross-platform Input System device layout (both the ARCore and ARKit XR
    /// providers register it, matched by product string "(ARCore)"/"(ARKit)"), not an
    /// ARKit-specific mechanism. Replaces <c>TrackedPoseDriver</c> to drive both position and
    /// rotation each frame and on <c>Application.onBeforeRender</c>.
    /// </summary>
    public sealed class ArCameraPoseDriver : MonoBehaviour
    {
        private bool loggedFirstPose;

        private void OnEnable()
        {
            Application.onBeforeRender += ApplyDevicePose;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= ApplyDevicePose;
        }

        private void Update()
        {
            ApplyDevicePose();
        }

        private void ApplyDevicePose()
        {
            var device = InputSystem.GetDevice<HandheldARInputDevice>();
            if (device == null)
            {
                return;
            }

            var rotation = device.deviceRotation.ReadValue();
            if (rotation.x == 0f && rotation.y == 0f && rotation.z == 0f && rotation.w == 0f)
            {
                return;
            }

            transform.localPosition = device.devicePosition.ReadValue();
            transform.localRotation = rotation * SensorToScreenRotation(Screen.orientation);

            if (!loggedFirstPose)
            {
                loggedFirstPose = true;
                Debug.Log($"[ArCameraPoseDriver] orientation={Screen.orientation}, raw rotation={rotation.eulerAngles}, final={transform.localRotation.eulerAngles}");
            }
        }

        /// <summary>
        /// On ARKit, <see cref="HandheldARInputDevice"/> delivers pose in the landscape-right
        /// sensor frame while AR Foundation's per-frame projection matrix is corrected for the
        /// interface orientation, so without this compensation 3D content rolls about the view
        /// axis (~90 degrees in portrait) while the camera feed and OnGUI overlays stay upright
        /// and mask it. Whether ARCore's provider has the same, a different, or no equivalent
        /// quirk is unverified — confirm empirically on-device (rotate through portrait/landscape
        /// with a placed anchor and watch for roll relative to the camera feed) before assuming
        /// this correction is needed, wrong, or a no-op on Android.
        /// </summary>
        public static Quaternion SensorToScreenRotation(ScreenOrientation orientation)
        {
            float roll = orientation switch
            {
                ScreenOrientation.Portrait => 90f,
                ScreenOrientation.PortraitUpsideDown => -90f,
                ScreenOrientation.LandscapeLeft => 180f,
                ScreenOrientation.LandscapeRight => 0f,
                _ => 90f
            };

            return Quaternion.AngleAxis(roll, Vector3.forward);
        }
    }
}
