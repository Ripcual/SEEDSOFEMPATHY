using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using SeedsOfEmpathy;

namespace HappyHarvest
{
    public class PlayerController : MonoBehaviour
    {
        public InputActionAsset InputAction;
        public float Speed = 4.0f;

        public SpriteRenderer Target;
        public Transform ItemAttachBone;

        [Header("Seeds of Empathy")]
        public CharacterRole Role = CharacterRole.Madre;

        /// <summary>Rol que debe tomar la próxima instancia (se fija antes de Instantiate, antes del Awake).</summary>
        public static CharacterRole? PendingRole;

        /// <summary>True si es el personaje que controla el jugador.</summary>
        public bool IsActive { get; private set; }
        public bool CanControl => m_CanControl;

        /// <summary>True mientras el personaje camina solo hacia un destino (GoTo).</summary>
        public bool AutoControlled => m_AutoActive;
        public bool AutoArrived { get; private set; }

        private bool m_AutoActive;
        private Vector3 m_AutoDestination;
        private float m_AutoStuckTimer;
        private Vector2 m_AutoLastPos;
        private float m_AutoBestDist;
        private float m_AutoTotalTime;

        public int Coins
        {
            get => m_Coins;
            set
            {
                m_Coins = value;
                UIHandler.UpdateCoins(Coins);
            }
        }

        public InventorySystem Inventory => m_Inventory;
        public Animator Animator => m_Animator;

        //This is private as we don't want to be able to set coins without going through the accessor above that ensure
        //the UI is updated, but is tagged as SerializedField so it appear in the editor so designer can set the starting
        //amount of coins
        [SerializeField]
        private int m_Coins = 10;

        [SerializeField]
        private InventorySystem m_Inventory;
        
        private Rigidbody2D m_Rigidbody;

        private Vector3 m_SpawnPosition;
        
        private InputAction m_MoveAction;
        private InputAction m_NextItemAction;
        private InputAction m_PrevItemAction;
        private InputAction m_UseItemAction;

        private Vector3 m_CurrentWorldMousePos;
        private Vector2 m_CurrentLookDirection;
        private Vector3Int m_CurrentTarget;

        private TargetMarker m_TargetMarker;

        private bool m_HasTarget = false;
        private bool m_IsOverUI = false;

        private bool m_CanControl = true;

        private Animator m_Animator;

        private InteractiveObject m_CurrentInteractiveTarget = null;
        private Collider2D[] m_CollidersCache = new Collider2D[8];

        private Dictionary<Item, ItemInstance> m_ItemVisualInstance = new();

        private int m_DirXHash = Animator.StringToHash("DirX");
        private int m_DirYHash = Animator.StringToHash("DirY");
        private int m_SpeedHash = Animator.StringToHash("Speed");

        void Awake()
        {
            if (PendingRole.HasValue)
            {
                Role = PendingRole.Value;
                PendingRole = null;
            }

            // Un solo personaje por rol: si ya existe, esta copia sobra.
            var _ = GameManager.Instance;
            if (CharacterManager.Instance == null || CharacterManager.Instance.Has(Role))
            {
                Destroy(gameObject);
                return;
            }
            
            m_Rigidbody = GetComponent<Rigidbody2D>();
            m_Animator = GetComponentInChildren<Animator>();
            m_TargetMarker = Target.GetComponent<TargetMarker>();
            m_TargetMarker.Hide();
            
            //we can only set DontDestroyOnLoad root object, so ensure its root (Level Designer sometime place the character
            //prefab already in the scene and can sometime tuck it under other object in the hierarchy)
            gameObject.transform.SetParent(null);
            
            DontDestroyOnLoad(gameObject);

            CharacterManager.Instance.Register(this);
        }

        public void SetActive(bool active)
        {
            IsActive = active;
            if (!active && m_TargetMarker != null) m_TargetMarker.Hide();
        }

        /// <summary>Camina solo hasta el destino (usado por las crisis).</summary>
        public void GoTo(Vector3 destination)
        {
            m_AutoDestination = destination;
            m_AutoActive = true;
            AutoArrived = false;
            m_AutoStuckTimer = 0.0f;
            m_AutoTotalTime = 0.0f;
            m_AutoLastPos = m_Rigidbody.position;
            m_AutoBestDist = Vector2.Distance(m_Rigidbody.position, destination);
            IgnoreObstaclesAlongPath(m_Rigidbody.position, destination);
        }

        public void StopAuto()
        {
            m_AutoActive = false;
            RestoreCollisions();
        }

        private readonly System.Collections.Generic.List<(Collider2D, Collider2D)> m_IgnoredPairs = new System.Collections.Generic.List<(Collider2D, Collider2D)>();
        private static readonly string[] k_KeepSolid = { "House", "LevelBounds", "ElevationTiles", "GroundBase" };

        /// <summary>Durante el paseo automático atraviesa vallas y objetos sueltos (no la casa ni los límites).</summary>
        private void IgnoreObstaclesAlongPath(Vector2 from, Vector2 to)
        {
            RestoreCollisions();
            var mine = GetComponentsInChildren<Collider2D>();
            var center = (from + to) * 0.5f;
            var size = new Vector2(Mathf.Abs(to.x - from.x) + 3f, Mathf.Abs(to.y - from.y) + 3f);
            foreach (var h in Physics2D.OverlapBoxAll(center, size, 0f))
            {
                if (h == null || h.isTrigger || h.GetComponentInParent<PlayerController>() != null) continue;

                bool keep = false;
                for (var t = h.transform; t != null && !keep; t = t.parent)
                    foreach (var n in k_KeepSolid) if (t.name == n) { keep = true; break; }
                if (keep) continue;

                foreach (var m in mine)
                {
                    if (m.isTrigger) continue;
                    Physics2D.IgnoreCollision(m, h, true);
                    m_IgnoredPairs.Add((m, h));
                }
            }
        }

        private void RestoreCollisions()
        {
            foreach (var p in m_IgnoredPairs)
                if (p.Item1 != null && p.Item2 != null) Physics2D.IgnoreCollision(p.Item1, p.Item2, false);
            m_IgnoredPairs.Clear();
        }

        /// <summary>Gesto de trabajo (usado por las crisis): mira al frente y balancea la herramienta.</summary>
        public void PlayWorkGesture()
        {
            SetLookDirectionFrom(Vector2.down);
            m_Animator.SetTrigger("ToolSwing");
        }

        Vector2 AutoStep()
        {
            Vector2 toTarget = (Vector2)m_AutoDestination - m_Rigidbody.position;
            if (toTarget.magnitude < 0.12f)
            {
                m_AutoActive = false;
                AutoArrived = true;
                RestoreCollisions();
                return Vector2.zero;
            }

            // Atasco por falta de progreso (también si roza una valla): si en 2 s no se acerca
            // 0.3 unidades, o tarda más de 25 s, se coloca directamente en el destino.
            m_AutoTotalTime += Time.fixedDeltaTime;
            float dist = toTarget.magnitude;
            if (dist < m_AutoBestDist - 0.3f)
            {
                m_AutoBestDist = dist;
                m_AutoStuckTimer = 0.0f;
            }
            else
            {
                m_AutoStuckTimer += Time.fixedDeltaTime;
            }

            if (m_AutoStuckTimer > 2.0f || m_AutoTotalTime > 25.0f)
            {
                m_Rigidbody.position = m_AutoDestination;
                m_AutoActive = false;
                AutoArrived = true;
                RestoreCollisions();
                return Vector2.zero;
            }

            return toTarget.normalized * 0.75f;
        }
        
        void Start()
        {
            //Retrieve the action from the InputAction asset, enable them and add the callbacks.
            
            //Move action doesn't have any callback as it will be polled in the movement code directly.
            m_MoveAction = InputAction.FindAction("Gameplay/Move");
            m_MoveAction.Enable();

            m_NextItemAction = InputAction.FindAction("Gameplay/EquipNext");
            m_PrevItemAction = InputAction.FindAction("Gameplay/EquipPrev");

            m_NextItemAction.Enable();
            m_NextItemAction.performed += context =>
            {
                if (!IsActive || !m_CanControl) return;
                ToggleToolVisual(false);
                m_Inventory.EquipNext();
                ToggleToolVisual(true);
            };
            
            m_PrevItemAction.Enable();
            m_PrevItemAction.performed += context =>
            {
                if (!IsActive || !m_CanControl) return;
                ToggleToolVisual(false);
                m_Inventory.EquipPrev();
                ToggleToolVisual(true);
            };

            m_UseItemAction = InputAction.FindAction("Gameplay/Use");
            m_UseItemAction.Enable();

            m_UseItemAction.performed += context => { if (IsActive) UseObject(); };
            
            m_CurrentLookDirection = Vector2.right;
            
            m_Inventory.Init();

            foreach (var entry in m_Inventory.Entries)
            {
                if (entry.Item != null)
                    CreateItemVisual(entry.Item);
            }
            ToggleToolVisual(true);
            
            // Solo el personaje activo pinta su inventario/monedas; el cambio de personaje refresca la UI.
            if (IsActive)
            {
                UIHandler.UpdateInventory(m_Inventory);
                UIHandler.UpdateCoins(m_Coins);
            }
        }

        private void Update()
        {
            if (!IsActive) return;

            m_IsOverUI = EventSystem.current.IsPointerOverGameObject();
            m_CurrentInteractiveTarget = null;
            m_HasTarget = false;
            
            if (!IsMouseOverGameWindow())
            {
                UIHandler.ChangeCursor(UIHandler.CursorType.System);
                return;
            }

            if (!m_CanControl || m_IsOverUI)
            {
                if (m_IsOverUI) UIHandler.ChangeCursor(UIHandler.CursorType.Interact);
                return;
            }
            
            m_CurrentWorldMousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            //check if we are above an interactive object
            var overlapCount = Physics2D.OverlapPointNonAlloc(m_CurrentWorldMousePos, m_CollidersCache, 1 << 31);
            for (int i = 0; i < overlapCount; ++i)
            {
                var obj = m_CollidersCache[i].GetComponent<InteractiveObject>();
                if (obj != null)
                {
                    m_CurrentInteractiveTarget = obj;
                    m_HasTarget = false;
                    UIHandler.ChangeCursor(UIHandler.CursorType.Interact);
                    return;
                }
            }
            
            //If we reached here, we are not above UI or an interactive object, so set the cursor to the normal one
            UIHandler.ChangeCursor(UIHandler.CursorType.Normal);

            var grid = GameManager.Instance.Terrain?.Grid;

            //some scene may not have a terrain (interior scene)
            if (grid != null)
            {
                var currentCell = grid.WorldToCell(transform.position);
                var pointedCell = grid.WorldToCell(m_CurrentWorldMousePos);

                currentCell.z = 0;
                pointedCell.z = 0;

                var toTarget = pointedCell - currentCell;

                if (Mathf.Abs(toTarget.x) > 1)
                {
                    toTarget.x = (int)Mathf.Sign(toTarget.x);
                }

                if (Mathf.Abs(toTarget.y) > 1)
                {
                    toTarget.y = (int)Mathf.Sign(toTarget.y);
                }


                m_CurrentTarget = currentCell + toTarget;
                Target.transform.position = GameManager.Instance.Terrain.Grid.GetCellCenterWorld(m_CurrentTarget);

                if (m_Inventory.EquippedItem != null
                    && m_Inventory.EquippedItem.CanUse(m_CurrentTarget))
                {
                    m_HasTarget = true;
                    m_TargetMarker.Activate();
                }
                else
                {
                    m_TargetMarker.Hide();
                }
            }

            if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
                SaveSystem.Save();
            }
            else if (Keyboard.current.f9Key.wasPressedThisFrame)
            {
                SaveSystem.Load();
            }
        }

        void UseObject()
        {
            if(!m_CanControl || m_IsOverUI)
                return;
            
            if (m_CurrentInteractiveTarget != null)
            {
                m_CurrentInteractiveTarget.InteractedWith();
                return;
            }
            
            if (m_Inventory.EquippedItem != null && m_Inventory.EquippedItem.NeedTarget() && !m_HasTarget) return;
            
            UseItem();
        }

        void FixedUpdate()
        {
            Vector2 move = Vector2.zero;
            if (m_AutoActive)
            {
                if (m_CanControl) move = AutoStep();
            }
            else if (IsActive && m_CanControl && m_MoveAction != null)
            {
                move = m_MoveAction.ReadValue<Vector2>();
            }

            //note: == and != for vector2 is overriden to take in account floating point imprecision.
            if (move != Vector2.zero)
            {
                SetLookDirectionFrom(move);
            }
            else
            {
                //we aren't moving, look direction is based on the currently aimed toward point
                if (IsActive && IsMouseOverGameWindow())
                {
                    Vector3 posToMouse = m_CurrentWorldMousePos - transform.position;
                    SetLookDirectionFrom(posToMouse);
                }
            }

            var movement = move * Speed;
            var speed = movement.sqrMagnitude;
            
            m_Animator.SetFloat(m_DirXHash, m_CurrentLookDirection.x);
            m_Animator.SetFloat(m_DirYHash, m_CurrentLookDirection.y);
            m_Animator.SetFloat(m_SpeedHash, speed);

            m_Rigidbody.MovePosition(m_Rigidbody.position + movement * Time.deltaTime);
        }

        bool IsMouseOverGameWindow()
        {
            return !(0 > Input.mousePosition.x || 0 > Input.mousePosition.y || Screen.width < Input.mousePosition.x || Screen.height < Input.mousePosition.y);
        } 

        void SetLookDirectionFrom(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                m_CurrentLookDirection = direction.x > 0 ? Vector2.right : Vector2.left;
            }
            else
            {
                m_CurrentLookDirection = direction.y > 0 ? Vector2.up : Vector2.down;
            }
        }

        public bool CanFitInInventory(Item item, int count)
        {
            return m_Inventory.CanFitItem(item, count);
        }
        
        public bool AddItem(Item newItem)
        {
            CreateItemVisual(newItem);
            return m_Inventory.AddItem(newItem);
        }

        public void SellItem(int inventoryIndex, int count)
        {
            if (inventoryIndex < 0 || inventoryIndex > Inventory.Entries.Length)
                return;

            var item = Inventory.Entries[inventoryIndex].Item;

            if (item == null || !(item is Product product))
                return;
            
            int actualCount = m_Inventory.Remove(inventoryIndex, count);

            m_Coins += actualCount * product.SellPrice;
            UIHandler.UpdateCoins(m_Coins);
            UIHandler.PlayBuySellSound(transform.position);
        }

        public bool BuyItem(Item item)
        {
            if (item.BuyPrice > m_Coins)
            {
                return false;
            }

            m_Coins -= item.BuyPrice;
            UIHandler.UpdateCoins(m_Coins);
            UIHandler.PlayBuySellSound(transform.position);
            AddItem(item);
            return true;
        }

        public void ChangeEquipItem(int index)
        {
            //Disable the current item if there is one 
            ToggleToolVisual(false);
            
            m_Inventory.EquipItem(index);

            ToggleToolVisual(true);
        }

        public void ToggleControl(bool canControl)
        {
            // Las acciones de entrada son compartidas por los dos personajes (mismo InputActionAsset),
            // así que no se activan/desactivan aquí: se filtra con este flag y con IsActive.
            m_CanControl = canControl;
        }

        public void UseItem()
        {
            if(m_Inventory.EquippedItem == null)
                return;
            
            var previousEquipped = m_Inventory.EquippedItem;
            
            m_Inventory.UseEquippedObject(m_CurrentTarget);

            if (m_ItemVisualInstance.ContainsKey(previousEquipped))
            {
                var visual = m_ItemVisualInstance[previousEquipped];

                m_Animator.SetTrigger(visual.AnimatorHash);

                if (visual.Animator != null)
                {
                    if (!visual.Instance.activeInHierarchy)
                    {
                        //enable all parent as if it's disabled, value cannot be set
                        var current = visual.Instance.transform;
                        while (current != null)
                        {
                            current.gameObject.SetActive(true);
                            current = current.parent;
                        }
                    }
                    
                    visual.Animator.SetFloat(m_DirXHash, m_CurrentLookDirection.x);
                    visual.Animator.SetFloat(m_DirYHash, m_CurrentLookDirection.y);
                    visual.Animator.SetTrigger("Use");
                }
            }
            
            if (m_Inventory.EquippedItem == null)
            {
                //this mean we finished using an item, the entry is now empty, so we need to disable the visual if any
                if (previousEquipped != null)
                {
                    //This is a bit of a quick fix, this will let any animation to finish playing before we disable the visual.
                    StartCoroutine(DelayedObjectDisable(previousEquipped));
                }
            }
        }

        IEnumerator DelayedObjectDisable(Item item)
        {
            yield return new WaitForSeconds(1.0f);
            ToggleVisualExplicit(false, item);
        }

        public void Save(ref PlayerSaveData data)
        {
            data.Position = m_Rigidbody.position;
            data.Coins = m_Coins;
            data.Inventory = new List<InventorySaveData>();
            m_Inventory.Save(ref data.Inventory);
        }

        public void Load(PlayerSaveData data)
        {
            m_Coins = data.Coins;
            m_Inventory.Load(data.Inventory);

            m_Rigidbody.position = data.Position;
        }

        void ToggleToolVisual(bool enable)
        {
            if (m_Inventory.EquippedItem != null && m_ItemVisualInstance.TryGetValue(m_Inventory.EquippedItem, out var itemVisual))
            {
                itemVisual.Instance.SetActive(enable);
            }
        }

        void ToggleVisualExplicit(bool enable, Item item)
        {
            if (item != null && m_ItemVisualInstance.TryGetValue(item, out var itemVisual))
            {
                itemVisual.Instance.SetActive(enable);
            }
        }

        void CreateItemVisual(Item item)
        {
            if (item.VisualPrefab != null && !m_ItemVisualInstance.ContainsKey(item))
            {
                var newVisual = Instantiate(item.VisualPrefab, ItemAttachBone, false);
                newVisual.SetActive(false);
                
                m_ItemVisualInstance[item] = new ItemInstance()
                {
                    Instance = newVisual,
                    Animator = newVisual.GetComponentInChildren<Animator>(),
                    AnimatorHash = Animator.StringToHash(item.PlayerAnimatorTriggerUse)
                };
            }
        }
    }

    class ItemInstance
    {
        public GameObject Instance;
        public Animator Animator;
        public int AnimatorHash;
    }

    [System.Serializable]
    public struct PlayerSaveData
    {
        public Vector3 Position;
        
        public int Coins;
        public List<InventorySaveData> Inventory;
    }
}