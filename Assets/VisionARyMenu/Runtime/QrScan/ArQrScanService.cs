using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ZXing;

namespace VisionARyMenu.QrScan
{
    // Decodes QR codes from the AR camera feed. Mirrors the frame-throttling/CPU-image
    // conversion pattern MenuViewer's ArCameraFrameOcrService used for OCR (same
    // TryAcquireLatestCpuImage -> XRCpuImage.Convert(RGBA32) approach, since that conversion
    // path is already proven on this device/ARCore combination) but decodes locally via
    // ZXing.Net instead of submitting to a native OCR bridge. QR decoding is far cheaper than
    // OCR, so this runs at a higher frame rate and lower working resolution.
    [RequireComponent(typeof(ARCameraManager))]
    public sealed class ArQrScanService : MonoBehaviour
    {
        [SerializeField] private float framesPerSecond = 4f;
        // Dense QR payloads (e.g. a whole menu embedded inline, ~750+ bytes -> version ~19,
        // 93x93 modules) need enough resolution that each module is several pixels wide, or
        // the finder patterns don't resolve. 640 was tuned for short-URL-sized QR codes and
        // silently failed to decode anything denser; 1280 comfortably resolves an embedded
        // menu payload while still being far cheaper per-frame than the OCR pipeline's images.
        [SerializeField] private int maxImageDimension = 1280;
        [SerializeField] private bool autoStart = true;

        private ARCameraManager cameraManager;
        private BarcodeReaderGeneric reader;
        private bool running;
        private bool decodedThisRun;
        private float nextFrameTime;

        public event Action<string> QrCodeDecoded;

        public bool IsRunning => running;

        public void StartScanning()
        {
            running = true;
            decodedThisRun = false;
        }

        public void StopScanning()
        {
            running = false;
        }

        private void Awake()
        {
            cameraManager = GetComponent<ARCameraManager>();
            reader = new BarcodeReaderGeneric
            {
                // QR's own finder-pattern detection is rotation-invariant within the image
                // plane, so AutoRotate (which re-tries at 90-degree pre-rotations, mainly a
                // 1D-barcode concern) doesn't help here and would only cost extra time.
                AutoRotate = false,
                Options = new ZXing.Common.DecodingOptions
                {
                    PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
                    // Worth the extra per-attempt cost: scanning a screen (glare, slight
                    // blur, imperfect framing) is a harder real-world case than print, and
                    // attempts are already throttled to a few per second.
                    TryHarder = true
                }
            };
        }

        private void OnEnable()
        {
            cameraManager = cameraManager == null ? GetComponent<ARCameraManager>() : cameraManager;
            cameraManager.frameReceived += HandleFrameReceived;

            if (autoStart)
            {
                StartScanning();
            }
        }

        private void OnDisable()
        {
            StopScanning();
            if (cameraManager != null)
            {
                cameraManager.frameReceived -= HandleFrameReceived;
            }
        }

        private void HandleFrameReceived(ARCameraFrameEventArgs args)
        {
            if (!running || decodedThisRun)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            var interval = framesPerSecond <= 0f ? 0.2f : 1f / framesPerSecond;
            if (now < nextFrameTime)
            {
                return;
            }

            nextFrameTime = now + interval;

            if (!cameraManager.TryAcquireLatestCpuImage(out var image))
            {
                return;
            }

            try
            {
                TryDecode(image);
            }
            finally
            {
                image.Dispose();
            }
        }

        private void TryDecode(XRCpuImage image)
        {
            var scale = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(image.width, image.height) / (float)Mathf.Max(1, maxImageDimension)));
            var outputDimensions = new Vector2Int(Mathf.Max(1, image.width / scale), Mathf.Max(1, image.height / scale));
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, image.width, image.height),
                outputDimensions = outputDimensions,
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.None
            };

            var dataSize = image.GetConvertedDataSize(conversionParams);
            using (var buffer = new NativeArray<byte>(dataSize, Allocator.Temp))
            {
                image.Convert(conversionParams, buffer);
                var rgba = buffer.ToArray();

                var luminanceSource = new RGBLuminanceSource(
                    rgba,
                    outputDimensions.x,
                    outputDimensions.y,
                    RGBLuminanceSource.BitmapFormat.RGBA32);

                var result = reader.Decode(luminanceSource);
                if (result == null || string.IsNullOrEmpty(result.Text))
                {
                    return;
                }

                decodedThisRun = true;
                QrCodeDecoded?.Invoke(result.Text);
            }
        }
    }
}
