using System;
using System.Collections.Generic;
using HappyHarvest;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SeedsOfEmpathy
{
    [Serializable]
    public struct CharactersData
    {
        public PlayerSaveData Madre;
        public PlayerSaveData Hijo;
        public bool HasHijo;
        public CharacterRole Active;
    }

    /// <summary>
    /// Gestiona los dos personajes controlables: registro por rol, cambio con Tab, cámara y guardado.
    /// Lo crea el GameManager en su Awake.
    /// </summary>
    public class CharacterManager : MonoBehaviour
    {
        public static CharacterManager Instance { get; private set; }

        /// <summary>Si es true el jugador no puede cambiar de personaje (p. ej. durante una crisis).</summary>
        public bool SwitchLocked;

        public PlayerController Active => m_Active;
        public CharacterRole ActiveRole => m_ActiveRole;

        private readonly Dictionary<CharacterRole, PlayerController> m_Characters = new();
        private PlayerController m_Active;
        private CharacterRole m_ActiveRole;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                TrySwitch(m_ActiveRole == CharacterRole.Madre ? CharacterRole.Hijo : CharacterRole.Madre);
            }
        }

        public bool Has(CharacterRole role)
        {
            return m_Characters.TryGetValue(role, out var c) && c != null;
        }

        public PlayerController Get(CharacterRole role)
        {
            return m_Characters.TryGetValue(role, out var c) ? c : null;
        }

        /// <summary>Lo llama PlayerController.Awake. El primero en registrarse queda activo.</summary>
        public void Register(PlayerController character)
        {
            m_Characters[character.Role] = character;

            if (m_Active == null)
            {
                m_Active = character;
                m_ActiveRole = character.Role;
                character.SetActive(true);
                GameManager.Instance.Player = character;
            }
            else
            {
                character.SetActive(false);
            }
        }

        /// <summary>Cambio solicitado por el jugador. Respeta el bloqueo y el estado de control.</summary>
        public bool TrySwitch(CharacterRole role)
        {
            if (SwitchLocked) return false;
            var next = Get(role);
            if (next == null || next == m_Active) return false;
            if (m_Active != null && (!m_Active.CanControl || m_Active.AutoControlled)) return false;

            ForceActive(role);
            return true;
        }

        /// <summary>Cambio por código, ignora el bloqueo.</summary>
        public void ForceActive(CharacterRole role)
        {
            var next = Get(role);
            if (next == null) return;

            if (m_Active != null && m_Active != next) m_Active.SetActive(false);

            m_Active = next;
            m_ActiveRole = role;
            next.SetActive(true);
            GameManager.Instance.Player = next;

            var cam = GameManager.Instance.MainCamera;
            if (cam != null)
            {
                cam.Follow = next.transform;
                cam.LookAt = next.transform;
            }

            UIHandler.UpdateInventory(next.Inventory);
            UIHandler.UpdateCoins(next.Coins);
        }

        public void ToggleControlAll(bool canControl)
        {
            foreach (var c in m_Characters.Values)
            {
                if (c != null) c.ToggleControl(canControl);
            }
        }

        /// <summary>Crea al hijo junto al spawn si todavía no existe.</summary>
        public PlayerController EnsureSecondary(SpawnPoint spawn)
        {
            var existing = Get(CharacterRole.Hijo);
            if (existing != null) return existing;

            var prefab = Resources.Load<PlayerController>("Character_Hijo");
            bool fallback = prefab == null;
            if (fallback) prefab = Resources.Load<PlayerController>("Character");
            if (prefab == null) return null;

            Vector3 pos = FindFreePosition(spawn.SecondaryCharacterStart != null
                ? spawn.SecondaryCharacterStart.position
                : spawn.transform.position + new Vector3(1.2f, 0f, 0f), spawn.transform.position);

            // El rol debe estar fijado antes de que se ejecute el Awake de la instancia.
            PlayerController.PendingRole = CharacterRole.Hijo;
            var inst = Instantiate(prefab, pos, Quaternion.identity);
            PlayerController.PendingRole = null;

            if (fallback)
            {
                // Mientras no exista Character_Hijo.prefab: la madre teñida de azul.
                foreach (var sr in inst.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr != inst.Target) sr.color = new Color(0.6f, 0.75f, 1f);
                }
            }

            return inst;
        }

        private static readonly Vector2[] k_Offsets =
        {
            Vector2.zero, new Vector2(0f, -1f), new Vector2(1f, -1f), new Vector2(-1f, -1f),
            new Vector2(0f, -2f), new Vector2(1.5f, -2f), new Vector2(-1.5f, -2f), new Vector2(0f, -3f)
        };

        /// <summary>
        /// Devuelve una posición sin colisionadores sólidos (paredes, vallas) cerca de la deseada,
        /// para que el segundo personaje no aparezca atrapado dentro de un edificio.
        /// Busca hacia el sur (delante de la casa) y, si no hay hueco, usa el origen del spawn.
        /// </summary>
        private static Vector3 FindFreePosition(Vector3 desired, Vector3 fallback)
        {
            Physics2D.SyncTransforms();
            foreach (var o in k_Offsets)
            {
                var p = new Vector2(desired.x + o.x, desired.y + o.y);
                var hits = Physics2D.OverlapCircleAll(p, 0.35f);
                bool blocked = false;
                foreach (var h in hits)
                {
                    if (h != null && !h.isTrigger) { blocked = true; break; }
                }
                if (!blocked) return new Vector3(p.x, p.y, desired.z);
            }
            return fallback;
        }

        /// <summary>Al cambiar de escena, coloca al personaje no activo junto al spawn.</summary>
        public void PlaceOthers(SpawnPoint spawn)
        {
            foreach (var c in m_Characters.Values)
            {
                if (c == null || c == m_Active) continue;

                c.transform.position = FindFreePosition((c.Role == CharacterRole.Hijo && spawn.SecondaryCharacterStart != null)
                    ? spawn.SecondaryCharacterStart.position
                    : spawn.transform.position + new Vector3(1.2f, 0f, 0f), spawn.transform.position);
            }
        }

        public void Save(ref CharactersData data)
        {
            var madre = Get(CharacterRole.Madre);
            if (madre != null) madre.Save(ref data.Madre);

            var hijo = Get(CharacterRole.Hijo);
            data.HasHijo = hijo != null;
            if (hijo != null) hijo.Save(ref data.Hijo);

            data.Active = m_ActiveRole;
        }

        public void Load(CharactersData data)
        {
            var madre = Get(CharacterRole.Madre);
            if (madre != null) madre.Load(data.Madre);

            var hijo = Get(CharacterRole.Hijo);
            if (hijo != null && data.HasHijo) hijo.Load(data.Hijo);

            ForceActive(data.Active);
        }
    }
}
