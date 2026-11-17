using HappyHarvest;

namespace SeedsOfEmpathy
{
    /// <summary>
    /// Zona clicable sobre el personaje en crisis. Hereda de InteractiveObject, así que el
    /// PlayerController (capa 31) ya la detecta con el ratón. La crea el CrisisManager.
    /// </summary>
    public class MediationTarget : InteractiveObject
    {
        public override void InteractedWith()
        {
            if (CrisisManager.Instance != null) CrisisManager.Instance.BeginMediation();
        }
    }
}
