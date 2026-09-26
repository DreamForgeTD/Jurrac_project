using UnityEngine;

namespace DreamForgeTD
{
    // Scene teardown also releases effects attached to persistent gameplay objects.
    [DisallowMultipleComponent]
    public sealed class VfxPoolRoot : MonoBehaviour
    {
        private void OnDestroy() => GameVfx.DonPool(transform);
    }
}
