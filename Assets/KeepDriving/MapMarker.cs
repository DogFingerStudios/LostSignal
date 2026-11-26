using UnityEngine;

namespace KeepDriving
{
    [DisallowMultipleComponent]
    public sealed class MapMarker : MonoBehaviour
    {
        [SerializeField] private Texture2D icon;
        [SerializeField, Min(1f), Tooltip("Shared marker size in screen pixels.")]
        private float size = 24f;
        [SerializeField] private bool rotateWithHeading = true;

        internal bool CanDraw => isActiveAndEnabled && icon;
        internal Vector3 Position => transform.position;
        internal float Size => Mathf.Max(1f, size);

        internal void Draw(Vector2 position)
        {
            float heading = rotateWithHeading ? transform.eulerAngles.y : 0f;
            Minimap.DrawIcon(icon, position, Size, heading, Color.white);
        }
    }
}
