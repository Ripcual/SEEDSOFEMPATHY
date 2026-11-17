using HappyHarvest;
using UnityEngine;

namespace SeedsOfEmpathy
{
    /// <summary>
    /// Marcador en escena (Farm_Outdoor): punto de trabajo del hijo, punto de vuelta a casa y
    /// zona del huerto afectada por la crisis. Se registra solo en el CrisisManager.
    /// </summary>
    public class CrisisSite : MonoBehaviour
    {
        public Transform WorkPoint;
        public Transform HomePoint;
        public Vector2 AreaSize = new Vector2(6f, 4f);

        public Vector3 WorkPosition => WorkPoint != null ? WorkPoint.position : transform.position;
        public Vector3 HomePosition => HomePoint != null ? HomePoint.position : transform.position;
        public Bounds WorldBounds => new Bounds(transform.position, new Vector3(AreaSize.x, AreaSize.y, 10f));

        private void OnEnable()
        {
            var _ = GameManager.Instance; // garantiza que existe el CrisisManager al probar la escena directamente
            if (CrisisManager.Instance != null) CrisisManager.Instance.Site = this;
        }

        private void OnDisable()
        {
            if (CrisisManager.Instance != null && CrisisManager.Instance.Site == this)
                CrisisManager.Instance.Site = null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireCube(transform.position, new Vector3(AreaSize.x, AreaSize.y, 0f));

            if (WorkPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(WorkPoint.position, 0.3f);
            }

            if (HomePoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(HomePoint.position, 0.3f);
            }
        }
    }
}
