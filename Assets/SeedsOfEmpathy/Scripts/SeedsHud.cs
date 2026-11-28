using UnityEngine;
using UnityEngine.InputSystem;

namespace SeedsOfEmpathy
{
    /// <summary>HUD provisional (IMGUI): personaje activo, mensajes, barra de tensión, panel de ayuda y pista de crisis.</summary>
    public class SeedsHud : MonoBehaviour
    {
        private GUIStyle m_Label;
        private GUIStyle m_Message;
        private GUIStyle m_HelpTitle;
        private GUIStyle m_HelpText;
        private GUIStyle m_HelpKey;
        private GUIStyle m_Hint;
        private GUIStyle m_Bubble;
        private GUIStyle m_BubbleName;
        private Texture2D m_BubbleTex;
        private GUIStyle m_IntroTitle;
        private GUIStyle m_IntroBody;
        private GUIStyle m_IntroCenter;

        private bool m_ShowHelp = false;
        private int m_IntroPage = 0;
        private float m_PrevTimeScale = 1f;

        private static readonly string[] IntroPages =
        {
            "Seeds of Empathy",
            "Una finca aislada, un huerto y dos personas que se quieren, pero no siempre saben cómo cuidarse.\n\nEstás en el campo, lejos de todo. Aquí el día a día pesa más que la distancia.",
            "La madre, ya mayor, vive pendiente del huerto. Repasa las cosas una y otra vez hasta dejarlas en su sitio.\n\nSu hijo, de sesenta años, tiene épocas de mucha energía y épocas de caídas. Trabaja en el taller y en las obras de la casa.",
            "Entre los dos sacan la finca adelante. Pero algunas noches, el hijo se queda sin dormir y se pone a arreglar el riego a deshoras.\n\nTu tarea no es ganar: es estar a su lado a tiempo.",
        };

        private bool IntroActive => m_IntroPage >= 0 && m_IntroPage < IntroPages.Length;

        private void Awake()
        {
            m_PrevTimeScale = Time.timeScale;
            m_IntroPage = 0;
            Time.timeScale = 0f;
        }

        private void AdvanceIntro()
        {
            m_IntroPage++;
            if (!IntroActive) EndIntro();
        }

        private void EndIntro()
        {
            m_IntroPage = IntroPages.Length;
            Time.timeScale = m_PrevTimeScale <= 0f ? 1f : m_PrevTimeScale;
            m_ShowHelp = true;
        }

        private void OnDestroy()
        {
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (IntroActive)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame ||
                    (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)) AdvanceIntro();
                else if (kb.escapeKey.wasPressedThisFrame) EndIntro();
                return;
            }
            if (kb.hKey.wasPressedThisFrame) m_ShowHelp = !m_ShowHelp;
            else if (m_ShowHelp && (kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) m_ShowHelp = false;
        }

        private void OnGUI()
        {
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            float h = Screen.height / scale;

            if (m_Label == null) CreateStyles();

            if (IntroActive)
            {
                DrawIntro(w, h);
                return;
            }

            var chars = CharacterManager.Instance;
            if (chars == null || chars.Active == null) return;

            string hint = chars.SwitchLocked ? "" : "  (Tab: cambiar)";
            GUI.Label(new Rect(w / 2f - 130, 8, 320, 26), $"Personaje: {chars.ActiveRole}{hint}", m_Label);

            var crisis = CrisisManager.Instance;
            if (crisis != null)
            {
                if (crisis.State == CrisisState.Active)
                {
                    DrawBar(new Rect(w / 2f - 130, 40, 200, 14), crisis.Tension, new Color(0.9f, 0.5f, 0.1f));
                    GUI.Label(new Rect(w / 2f + 75, 35, 120, 24), "Tensión", m_Label);

                    if (crisis.IsMediating)
                    {
                        DrawBar(new Rect(w / 2f - 100, h - 120, 200, 12), crisis.MediationProgress, new Color(0.3f, 0.75f, 0.45f));
                    }
                }

                DrawDialogueBubble(crisis, scale);

                if (!string.IsNullOrEmpty(crisis.Message) && Time.unscaledTime < crisis.MessageUntil)
                {
                    GUI.Box(new Rect(w / 2f - 300, h - 90, 600, 60), crisis.Message, m_Message);
                }

                if (!m_ShowHelp && (crisis.State == CrisisState.Warning || crisis.State == CrisisState.Active))
                {
                    GUI.Box(new Rect(12, h - 44, 560, 30),
                        "Crisis: acércate al hijo y quédate a su lado   ·   H: ayuda", m_Hint);
                }
            }

            if (m_ShowHelp) DrawHelp(w, h);
        }

        private void DrawIntro(float w, float h)
        {
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.92f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = prev;

            float pw = 760f;
            float x = (w - pw) / 2f;
            bool title = m_IntroPage == 0;
            if (title)
            {
                GUI.Label(new Rect(x, h / 2f - 60f, pw, 60f), IntroPages[0], m_IntroTitle);
                GUI.Label(new Rect(x, h / 2f + 10f, pw, 30f), "Un simulador de granja sobre convivencia y salud mental", m_IntroCenter);
            }
            else
            {
                GUI.Label(new Rect(x, h / 2f - 110f, pw, 260f), IntroPages[m_IntroPage], m_IntroBody);
            }
            string next = m_IntroPage >= IntroPages.Length - 1 ? "Enter o clic: empezar" : "Enter o clic: continuar";
            GUI.Label(new Rect(x, h - 70f, pw, 28f), next + "   ·   Esc: saltar", m_IntroCenter);
        }

        private void DrawDialogueBubble(CrisisManager crisis, float scale)
        {
            if (string.IsNullOrEmpty(crisis.DialogueText)) return;
            var speaker = CharacterManager.Instance.Get(crisis.DialogueSpeaker);
            var cam = Camera.main;
            if (speaker == null || cam == null) return;

            Vector3 sp = cam.WorldToScreenPoint(speaker.transform.position + Vector3.up * 2.1f);
            if (sp.z < 0f) return;

            float w = Screen.width / scale;
            float h = Screen.height / scale;
            float bw = 330f;
            float bh = m_Bubble.CalcHeight(new GUIContent(crisis.DialogueText), bw);
            float x = Mathf.Clamp(sp.x / scale - bw / 2f, 8f, w - bw - 8f);
            float y = Mathf.Clamp((Screen.height - sp.y) / scale - bh, 70f, h - bh - 8f);

            string name = crisis.DialogueSpeaker == CharacterRole.Hijo ? "Hijo" : "Madre";
            var prev = GUI.color;
            GUI.color = crisis.DialogueSpeaker == CharacterRole.Hijo ? new Color(0.85f, 0.93f, 1f) : new Color(1f, 0.93f, 0.85f);
            GUI.Box(new Rect(x, y, bw, bh), crisis.DialogueText, m_Bubble);
            GUI.color = prev;
            GUI.Label(new Rect(x + 4f, y + 2f, bw - 8f, 20f), name, m_BubbleName);
        }

        private void DrawHelp(float w, float h)
        {
            float pw = 640f, ph = 360f;
            var rect = new Rect((w - pw) / 2f, (h - ph) / 2f, pw, ph);
            GUI.Box(rect, GUIContent.none, m_Message);

            float x = rect.x + 28f;
            float y = rect.y + 18f;
            GUI.Label(new Rect(x, y, pw - 56f, 30f), "Cómo se juega", m_HelpTitle);
            y += 38f;
            GUI.Label(new Rect(x, y, pw - 56f, 52f),
                "Objetivo del día: cuida el huerto con la madre y acompaña al hijo si tiene una mala noche.", m_HelpText);
            y += 62f;

            string[,] rows =
            {
                { "WASD / flechas", "Moverse" },
                { "Q / E o rueda", "Cambiar de herramienta" },
                { "Espacio o clic", "Usar la herramienta" },
                { "Tab", "Cambiar entre madre e hijo" },
                { "H", "Abrir o cerrar esta ayuda" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                GUI.Label(new Rect(x, y, 190f, 26f), rows[i, 0], m_HelpKey);
                GUI.Label(new Rect(x + 200f, y, pw - 260f, 26f), rows[i, 1], m_HelpText);
                y += 28f;
            }

            y += 8f;
            GUI.Label(new Rect(x, y, pw - 56f, 52f),
                "En una crisis, acércate al hijo y quédate a su lado hasta que se calme.", m_HelpText);
            GUI.Label(new Rect(rect.x, rect.yMax - 30f, pw - 20f, 24f), "Pulsa H para empezar", m_Hint);
        }

        private void CreateStyles()
        {
            m_Label = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            m_Message = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            m_HelpTitle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.94f, 0.82f, 0.54f) } };
            m_HelpText = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, normal = { textColor = Color.white } };
            m_HelpKey = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.94f, 0.82f, 0.54f) } };
            var gold = new Color(0.94f, 0.82f, 0.54f);
            m_BubbleTex = new Texture2D(1, 1);
            m_BubbleTex.SetPixel(0, 0, Color.white);
            m_BubbleTex.Apply();
            m_Bubble = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(10, 10, 22, 10),
                normal = { background = m_BubbleTex, textColor = new Color(0.12f, 0.1f, 0.08f) }
            };
            m_BubbleName = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(6, 6, 0, 0),
                normal = { textColor = new Color(0.45f, 0.32f, 0.12f) }
            };
            m_IntroTitle = new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = gold } };
            m_IntroBody = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true, alignment = TextAnchor.UpperLeft, normal = { textColor = Color.white } };
            m_IntroCenter = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.8f, 0.76f, 0.68f) } };
            m_Hint = new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.94f, 0.82f, 0.54f) }
            };
        }

        private static void DrawBar(Rect rect, float value, Color fill)
        {
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
