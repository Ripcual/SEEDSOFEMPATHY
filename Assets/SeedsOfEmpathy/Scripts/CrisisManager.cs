using System.Collections.Generic;
using System.Linq;
using HappyHarvest;
using UnityEngine;

namespace SeedsOfEmpathy
{
    public enum CrisisState
    {
        Idle,
        Warning,
        Active,
        Resolved,
        Escalated
    }

    /// <summary>
    /// Máquina de estados de la crisis C1 "Madrugada de ideas":
    /// Idle -> Warning -> Active -> Resolved / Escalated.
    /// En el vertical slice la crisis ocurre una sola vez por partida.
    /// Lo crea el GameManager en su Awake.
    /// </summary>
    public class CrisisManager : MonoBehaviour
    {
        public static CrisisManager Instance { get; private set; }

        public CrisisDefinition Definition;
        public CrisisSite Site;

        public CrisisState State { get; private set; } = CrisisState.Idle;

        /// <summary>Intensidad del episodio en curso, 0-1.</summary>
        public float Tension { get; private set; }

        /// <summary>Plantas alteradas, para el resumen del día.</summary>
        public int DisturbedPlants { get; private set; }

        /// <summary>Gancho para C2 "El bajón" el día siguiente.</summary>
        public bool AftermathPending { get; private set; }
        public bool AftermathEscalated { get; private set; }

        public bool HasOccurred { get; private set; }
        public bool IsMediating => m_Mediating;
        public float MediationProgress => MediationNeeded > 0f ? m_MediationTime / MediationNeeded : 0f;

        /// <summary>Línea de diálogo en curso (sin el nombre del personaje), o null si nadie habla.</summary>
        public string DialogueText { get; private set; }
        public CharacterRole DialogueSpeaker { get; private set; }

        private float MediationNeeded => Mathf.Max(Definition.MediationHoldSeconds, m_DialogueTotal);

        public string Message { get; private set; }
        public float MessageUntil { get; private set; }

        private PlayerController m_Hijo;
        private GameObject m_MediationZone;
        private bool m_Mediating;
        private float m_MediationTime;
        private float m_RegressionTimer;
        private int m_DialogueIndex = -1;
        private float m_DialogueTimer;
        private float m_DialogueTotal;
        private float m_WorkTimer;

        private void Awake()
        {
            Instance = this;

            Definition = Resources.LoadAll<CrisisDefinition>("Crises").FirstOrDefault();
            if (Definition == null) Definition = CrisisDefinition.CreateDefault();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            var chars = CharacterManager.Instance;
            if (gm == null || chars == null || chars.Active == null || Definition == null) return;

            float hour = gm.CurrentDayRatio * 24f;
            bool running = chars.Active.CanControl;

            switch (State)
            {
                case CrisisState.Idle:
                    if (HasOccurred) break;

                    if (hour >= Definition.EndHour)
                    {
                        HasOccurred = true; // ventana perdida
                    }
                    else if (hour >= Definition.StartHour)
                    {
                        TryBegin();
                    }
                    else if (hour >= Definition.WarningHour)
                    {
                        State = CrisisState.Warning;
                        Say(Definition.WarningMessage);
                    }
                    break;

                case CrisisState.Warning:
                    if (hour >= Definition.EndHour)
                    {
                        HasOccurred = true;
                        State = CrisisState.Idle;
                    }
                    else if (hour >= Definition.StartHour)
                    {
                        TryBegin();
                    }
                    break;

                case CrisisState.Active:
                    if (running)
                    {
                        UpdateWorkGesture();
                        UpdateMediation();
                        UpdateRegression();
                    }

                    // La tensión sube con la hora y baja mientras la madre acompaña.
                    float timeTension = Mathf.InverseLerp(Definition.StartHour, Definition.EndHour, hour);
                    Tension = Mathf.Lerp(timeTension, 0.1f, Mathf.Clamp01(MediationProgress));

                    if (State == CrisisState.Active && hour >= Definition.EndHour)
                    {
                        Finish(CrisisState.Escalated);
                    }
                    break;
            }
        }

        /// <summary>Lo llama MediationTarget al hacer clic sobre el personaje en crisis.</summary>
        public void BeginMediation()
        {
            if (State != CrisisState.Active || m_Hijo == null) return;

            var madre = CharacterManager.Instance.Get(CharacterRole.Madre);
            if (madre == null) return;

            if (Vector2.Distance(madre.transform.position, m_Hijo.transform.position) > Definition.MediationRange)
            {
                Say(Definition.TooFarMessage, 3f);
                return;
            }

            m_Mediating = true;
            StartDialogueIfNeeded();
        }

        private static float LineSeconds(string line) => 2.5f + (line == null ? 0 : line.Length) * 0.035f;

        private bool HasDialogue => Definition.Dialogue != null && Definition.Dialogue.Length > 0;

        private void StartDialogueIfNeeded()
        {
            if (!HasDialogue) return;
            if (m_DialogueIndex >= 0)
            {
                // Reanuda donde se quedó, si no había terminado.
                if (m_DialogueIndex < Definition.Dialogue.Length) ShowLine(Definition.Dialogue[m_DialogueIndex]);
                return;
            }
            m_DialogueIndex = 0;
            m_DialogueTimer = LineSeconds(Definition.Dialogue[0]);
            ShowLine(Definition.Dialogue[0]);
        }

        private void ShowLine(string line)
        {
            DialogueSpeaker = line.StartsWith("Hijo:") ? CharacterRole.Hijo : CharacterRole.Madre;
            int colon = line.IndexOf(':');
            DialogueText = colon >= 0 ? line.Substring(colon + 1).Trim() : line;
        }

        private void AdvanceDialogue()
        {
            if (m_DialogueIndex < 0 || !HasDialogue) return;
            m_DialogueTimer -= Time.deltaTime;
            if (m_DialogueTimer > 0f) return;

            m_DialogueIndex++;
            if (m_DialogueIndex >= Definition.Dialogue.Length)
            {
                DialogueText = null;
                return;
            }
            m_DialogueTimer = LineSeconds(Definition.Dialogue[m_DialogueIndex]);
            ShowLine(Definition.Dialogue[m_DialogueIndex]);
        }

        private void TryBegin()
        {
            // Sin CrisisSite (otra escena) se espera hasta volver a Farm_Outdoor.
            if (Site == null) return;

            var chars = CharacterManager.Instance;
            m_Hijo = chars.Get(Definition.AffectedRole);
            var other = chars.Get(Definition.AffectedRole == CharacterRole.Hijo ? CharacterRole.Madre : CharacterRole.Hijo);
            if (m_Hijo == null || other == null) return;

            HasOccurred = true;
            State = CrisisState.Active;
            DisturbedPlants = 0;
            Tension = 0f;
            m_RegressionTimer = Definition.PlantRegressionInterval;
            m_Mediating = false;
            m_MediationTime = 0f;
            m_DialogueIndex = -1;
            m_DialogueTimer = 0f;
            DialogueText = null;
            m_DialogueTotal = 0f;
            if (Definition.Dialogue != null)
                foreach (var l in Definition.Dialogue) m_DialogueTotal += LineSeconds(l);
            m_WorkTimer = 1f;

            // El jugador controla a quien no está en crisis y no puede cambiar.
            chars.ForceActive(other.Role);
            chars.SwitchLocked = true;

            m_Hijo.GoTo(Site.WorkPosition);
            CreateMediationZone();

            Say(Definition.StartMessage);
        }

        private void UpdateMediation()
        {
            if (m_Hijo == null) return;

            // Acompañar no exige hacer clic: basta con permanecer junto a él.
            if (!m_Mediating)
            {
                var m = CharacterManager.Instance.Get(CharacterRole.Madre);
                if (m != null && Vector2.Distance(m.transform.position, m_Hijo.transform.position) <= Definition.MediationRange)
                {
                    m_Mediating = true;
                    StartDialogueIfNeeded();
                }
                return;
            }

            var madre = CharacterManager.Instance.Get(CharacterRole.Madre);
            if (madre == null) return;

            if (Vector2.Distance(madre.transform.position, m_Hijo.transform.position) > Definition.MediationRange)
            {
                // Si se aleja, el intento se cancela.
                // El progreso y la conversación se quedan donde estaban.
                m_Mediating = false;
                DialogueText = null;
                Say(Definition.MediationCancelledMessage, 3f);
                return;
            }

            // Hablan mientras ella acompaña; la crisis se resuelve al acabar la conversación.
            AdvanceDialogue();
            m_MediationTime += Time.deltaTime;
            if (m_MediationTime >= MediationNeeded)
            {
                Finish(CrisisState.Resolved);
            }
        }

        private void UpdateWorkGesture()
        {
            if (m_Hijo == null || m_Hijo.AutoControlled || !m_Hijo.AutoArrived) return;
            m_WorkTimer -= Time.deltaTime;
            if (m_WorkTimer > 0f) return;
            m_WorkTimer = 1.8f;
            m_Hijo.PlayWorkGesture();
        }

        private void UpdateRegression()
        {
            m_RegressionTimer -= Time.deltaTime;
            if (m_RegressionTimer > 0f) return;

            m_RegressionTimer = Definition.PlantRegressionInterval;
            RegressRandomPlant();
        }

        private void RegressRandomPlant()
        {
            var terrain = GameManager.Instance.Terrain;
            if (terrain == null || terrain.Grid == null || Site == null) return;

            var bounds = Site.WorldBounds;
            var min = terrain.Grid.WorldToCell(bounds.min);
            var max = terrain.Grid.WorldToCell(bounds.max);

            var candidates = new List<Vector3Int>();
            for (int x = min.x; x <= max.x; ++x)
            {
                for (int y = min.y; y <= max.y; ++y)
                {
                    var cell = new Vector3Int(x, y, 0);
                    var data = terrain.GetCropDataAt(cell);
                    if (data != null && data.CurrentGrowthStage > 0) candidates.Add(cell);
                }
            }

            if (candidates.Count == 0) return;

            var chosen = candidates[Random.Range(0, candidates.Count)];
            var cropData = terrain.GetCropDataAt(chosen);
            terrain.OverrideGrowthStage(chosen, cropData.CurrentGrowthStage - 1);
            DisturbedPlants++;
        }

        private void Finish(CrisisState outcome)
        {
            State = outcome;
            m_Mediating = false;
            m_MediationTime = 0f;
            DialogueText = null;

            AftermathPending = true;
            AftermathEscalated = outcome == CrisisState.Escalated;
            Tension = outcome == CrisisState.Resolved ? 0.25f : 1f;

            if (m_Hijo != null && Site != null) m_Hijo.GoTo(Site.HomePosition);
            if (m_MediationZone != null) Destroy(m_MediationZone);

            CharacterManager.Instance.SwitchLocked = false;

            Say(outcome == CrisisState.Resolved ? Definition.ResolvedMessage : Definition.EscalatedMessage, 8f);
        }

        private void CreateMediationZone()
        {
            if (m_MediationZone != null) Destroy(m_MediationZone);

            m_MediationZone = new GameObject("MediationZone");
            m_MediationZone.transform.SetParent(m_Hijo.transform, false);
            m_MediationZone.transform.localPosition = new Vector3(0f, 0.5f, 0f);

            var col = m_MediationZone.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.9f;

            // InteractiveObject.Awake pone la capa 31 que consulta el PlayerController.
            m_MediationZone.AddComponent<MediationTarget>();
        }

        private void Say(string text, float seconds = 6f)
        {
            Message = text;
            MessageUntil = Time.unscaledTime + seconds;
        }
    }
}
