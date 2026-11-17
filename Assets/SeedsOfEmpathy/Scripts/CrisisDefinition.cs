using UnityEngine;

namespace SeedsOfEmpathy
{
    /// <summary>
    /// Datos de una crisis (horario, reglas y textos), editables como asset sin tocar código.
    /// Crear con Create > Seeds of Empathy > Crisis en Assets/Resources/Crises/.
    /// Los textos son un borrador y deben contrastarse con fuentes clínicas antes de la entrega.
    /// </summary>
    [CreateAssetMenu(fileName = "Crisis_ManiaNocturna", menuName = "Seeds of Empathy/Crisis")]
    public class CrisisDefinition : ScriptableObject
    {
        public string Id = "mania_nocturna";
        public CharacterRole AffectedRole = CharacterRole.Hijo;

        [Header("Horario (hora del día, 0-24)")]
        public float WarningHour = 2.5f;
        public float StartHour = 3.0f;
        public float EndHour = 7.0f;

        [Header("Reglas")]
        [Tooltip("Segundos que hay que permanecer junto al personaje para mediar.")]
        public float MediationHoldSeconds = 3.0f;
        [Tooltip("Distancia máxima (unidades de mundo) para acompañar.")]
        public float MediationRange = 2.5f;
        [Tooltip("Cada cuántos segundos una planta del área retrocede una etapa.")]
        public float PlantRegressionInterval = 4.0f;

        [Header("Textos")]
        [TextArea] public string WarningMessage = "Hay luz en el taller de madrugada. Tu hijo no ha dormido.";
        [TextArea] public string StartMessage = "Tu hijo ha salido al huerto con herramientas y está montando un sistema de riego.";
        [TextArea] public string MediationHintMessage = "Acércate a él y haz clic para acompañarle.";
        [TextArea] public string TooFarMessage = "Acércate más a tu hijo.";
        [TextArea] public string MediationCancelledMessage = "Te has alejado. Vuelve a su lado.";
        [TextArea] public string ResolvedMessage = "Ha anotado el plan para más tarde y vuelve al taller.";
        [TextArea] public string EscalatedMessage = "La obra ha quedado a medias. Vuelve al taller agotado.";

        public static CrisisDefinition CreateDefault()
        {
            var def = CreateInstance<CrisisDefinition>();
            def.name = "Crisis_Default";
            return def;
        }
    }
}
