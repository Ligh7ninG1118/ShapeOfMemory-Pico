using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    // Marks objects under a Torch2Gym cluster. Without one, every mesh under the cluster is part of the picture.
    // Applies to this object and its children, unless a child has its own ShadowPiece.
    [DisallowMultipleComponent]
    public sealed class ShadowPiece : MonoBehaviour
    {
        public enum Role{Picture,VisualOnly}
        [Tooltip("Picture: casts the shadow that makes the picture. Visual only: drawn on the orb, casts nothing.")]
        public Role role;
        [Tooltip("Optional mesh that casts this object's shadow instead of its own (simpler, or adjusted to the picture). Same pivot and scale as this object's mesh; only used on the object that has the MeshFilter.")]
        public Mesh shadowProxy;
    }
}
