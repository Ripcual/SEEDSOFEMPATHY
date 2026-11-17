using UnityEngine;

namespace SeedsOfEmpathy
{
    /// <summary>HUD provisional (IMGUI): personaje activo, mensajes y barra de tensión.</summary>
    public class SeedsHud : MonoBehaviour
    {
        private GUIStyle m_Label;
        private GUIStyle m_Message;

        private void OnGUI()
        {
            var chars = CharacterManager.Instance;
            if (chars == null || chars.Active == null) return;

            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            float h = Screen.height / scale;

            if (m_Label == null)
            {
                m_Label = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                m_Message = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 18,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
            }

            string hint = chars.SwitchLocked ? "" : "  (Tab: cambiar)";
            GUI.Label(new Rect(10, 8, 400, 26), $"Personaje: {chars.ActiveRole}{hint}", m_Label);

            var crisis = CrisisManager.Instance;
            if (crisis == null) return;

            if (crisis.State == CrisisState.Active)
            {
                DrawBar(new Rect(10, 36, 200, 14), crisis.Tension, new Color(0.9f, 0.5f, 0.1f));
                GUI.Label(new Rect(215, 31, 120, 24), "Tensión", m_Label);

                if (crisis.IsMediating)
                {
                    DrawBar(new Rect(w / 2f - 100, h - 120, 200, 12), crisis.MediationProgress, new Color(0.3f, 0.75f, 0.45f));
                }
            }

            if (!string.IsNullOrEmpty(crisis.Message) && Time.unscaledTime < crisis.MessageUntil)
            {
                GUI.Box(new Rect(w / 2f - 300, h - 90, 600, 60), crisis.Message, m_Message);
            }
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
