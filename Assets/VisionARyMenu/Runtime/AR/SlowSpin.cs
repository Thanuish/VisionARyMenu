using UnityEngine;

namespace MenuViewer.AR
{
    public sealed class SlowSpin : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 12f;

        private void Update()
        {
            transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
