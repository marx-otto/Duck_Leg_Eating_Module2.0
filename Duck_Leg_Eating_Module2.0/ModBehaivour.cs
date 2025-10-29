using UnityEngine;
using System.Collections;

namespace Duck_Leg_Eating_Module2._0
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private void Start()
        {
            Debug.Log("鸭腿食用模块已加载 - 搬起后按E直接食用");
            
            // 定期检查场景中的敌人鸭腿
            StartCoroutine(ScanForEnemyCorpses());
        }

        private IEnumerator ScanForEnemyCorpses()
        {
            while (true)
            {
                // 查找场景中所有InteractableLootbox
                var allLootboxes = FindObjectsOfType<InteractableLootbox>();
                foreach (var lootbox in allLootboxes)
                {
                    // 检查是否是敌人鸭腿且没有EatableCorpse组件
                    if (IsEnemyCorpse(lootbox) && lootbox.GetComponent<EatableCorpse>() == null)
                    {
                        // 直接添加食用组件，不需要等待容器为空
                        var eatable = lootbox.gameObject.AddComponent<EatableCorpse>();
                        eatable.Initialize(lootbox);
                        Debug.Log($"发现敌人鸭腿 {lootbox.name}，已添加食用功能");
                    }
                }
                
                // 每2秒扫描一次
                yield return new WaitForSeconds(2f);
            }
        }

        private bool IsEnemyCorpse(InteractableLootbox lootbox)
        {
            // 通过名称判断是否是敌人鸭腿
            string lootboxName = lootbox.name.ToLower();
            return lootboxName.Contains("corpse") || lootboxName.Contains("body") || lootboxName.Contains("enemy") || 
                   lootboxName.Contains("scav") || lootboxName.Contains("鸭腿") || lootboxName.Contains("敌人");
        }

        private void OnDestroy()
        {
            Debug.Log("鸭腿食用模块已卸载");
        }
    }

    public class EatableCorpse : MonoBehaviour
    {
        private InteractableLootbox _corpse;
        private CharacterMainControl _player;
        private bool _isEatable = true;
        private bool _isShowingPrompt;
        private bool _isCarried;
        private EnemyType _enemyType;
        private bool _isReleasing;

        // 新增：体型变大效果管理
        private static PlayerSizeEffect _playerSizeEffect;

        private enum EnemyType
        {
            Normal,
            Minion,  
            Boss
        }

        public void Initialize(InteractableLootbox targetCorpse)
        {
            _corpse = targetCorpse;
            
            _enemyType = GetEnemyType(_corpse);
            Debug.Log($"识别到敌人类型: {_enemyType} - {_corpse.name}");
        }

        private EnemyType GetEnemyType(InteractableLootbox lootbox)
        {
            EnemyType typeFromPreset = TryGetEnemyTypeFromPreset();
            if (typeFromPreset != EnemyType.Normal)
                return typeFromPreset;

            return GetEnemyTypeFromLootbox(lootbox);
        }

        private EnemyType TryGetEnemyTypeFromPreset()
        {
            return EnemyType.Normal;
        }

        private EnemyType GetEnemyTypeFromLootbox(InteractableLootbox lootbox)
        {
            string lootboxName = lootbox.name.ToLower();
            
            if (lootboxName.Contains("boss"))
                return EnemyType.Boss;
            if (lootboxName.Contains("elite") || lootboxName.Contains("miniboss") || 
                lootboxName.Contains("minion") || lootboxName.Contains("小弟") || lootboxName.Contains("精英"))
                return EnemyType.Minion;
            
            return EnemyType.Normal;
        }

        private void Update()
        {
            if (!_isEatable || _corpse == null) return;
            
            // 检查是否被搬起
            CheckIfCarried();
            
            if (_isCarried)
            {
                if (_player == null)
                {
                    _player = FindObjectOfType<CharacterMainControl>();
                    if (_player == null) return;
                }

                if (_isReleasing) return;

                // 显示提示信息
                if (!_isShowingPrompt)
                {
                    _player.PopText("按 E 开饭", 2f);
                    _isShowingPrompt = true;
                }
                
                // 检测E键按下
                if (Input.GetKeyDown(KeyCode.E))
                {
                    Debug.Log("检测到E键按下，尝试食用鸭腿");
                    EatCorpse();
                }
            }
            else
            {
                // 重置提示状态
                _isShowingPrompt = false;
            }
        }

        private void CheckIfCarried()
        {
            if (_corpse.transform.parent != null)
            {
                var parentPlayer = _corpse.transform.parent.GetComponentInParent<CharacterMainControl>();
                if (parentPlayer != null)
                {
                    _isCarried = true;
                    _player = parentPlayer;
                    Debug.Log($"鸭腿被玩家搬起，可以食用");
                }
                else
                {
                    _isCarried = false;
                }
            }
            else
            {
                _isCarried = false;
            }
        }

        private void EatCorpse()
        {
            if (!_isEatable || _isReleasing) return;
            
            Debug.Log($"开始执行食用{_enemyType}鸭腿逻辑");
            
            if (_player == null)
            {
                _player = FindObjectOfType<CharacterMainControl>();
                if (_player == null)
                {
                    Debug.LogError("找不到玩家");
                    return;
                }
            }

            ApplyEffectsBasedOnEnemyType(_enemyType);
            
            // 新增：应用体型变大效果
            ApplySizeIncreaseEffect(_enemyType);
            
            _isReleasing = true;
            StartCoroutine(ReleaseAndDestroyCoroutine());
            
            _isEatable = false;
        }

        /// <summary>
        /// 新增：应用体型变大效果（无限叠加）
        /// </summary>
        private void ApplySizeIncreaseEffect(EnemyType enemyType)
        {
            try
            {
                // 获取或创建体型效果管理器
                if (_playerSizeEffect == null)
                {
                    _playerSizeEffect = FindObjectOfType<PlayerSizeEffect>();
                    if (_playerSizeEffect == null)
                    {
                        GameObject sizeEffectObj = new GameObject("PlayerSizeEffect");
                        _playerSizeEffect = sizeEffectObj.AddComponent<PlayerSizeEffect>();
                        DontDestroyOnLoad(sizeEffectObj);
                        Debug.Log("创建新的体型效果管理器");
                    }
                }

                // 根据敌人类型确定叠加层数
                int stacksToAdd = GetSizeEffectStacks(enemyType);
                
                // 应用体型变化（无限叠加）
                _playerSizeEffect.ApplySizeIncrease(_player, stacksToAdd);
                
                Debug.Log($"应用体型变大效果: {_player.name} -> {enemyType} +{stacksToAdd}层, 当前总层数: {_playerSizeEffect.CurrentStacks}, 当前体型倍数: {_playerSizeEffect.GetCurrentSizeMultiplier():F2}x");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"应用体型变大效果失败: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// 根据敌人类型确定体型效果叠加层数
        /// </summary>
        private int GetSizeEffectStacks(EnemyType enemyType)
        {
            switch (enemyType)
            {
                case EnemyType.Normal:
                    return 1; // 普通敌人+1层
                case EnemyType.Minion:
                    return 2; // 精英敌人+2层
                case EnemyType.Boss:
                    return 3; // Boss+3层
                default:
                    return 1;
            }
        }

        private IEnumerator ReleaseAndDestroyCoroutine()
        {
            Debug.Log("开始释放和销毁流程");

            // 模拟F键放下物品
            bool success = SimulateFKeyInteraction();
            if (success)
            {
                Debug.Log("成功模拟F键交互");
                yield return null;
            }
            else
            {
                Debug.LogWarning("模拟F键失败，使用备用方案");
                StopCarryAction();
                yield return null;
            }

            // 尝试切换到一号武器
            Debug.Log("开始尝试切换到一号武器");
            yield return StartCoroutine(SwitchToWeapon1Coroutine());

            CleanupAndDestroy();

            _isReleasing = false;
        }

        /// <summary>
        /// 尝试切换到一号武器的协程
        /// </summary>
        private IEnumerator SwitchToWeapon1Coroutine()
        {
            Debug.Log("尝试切换到一号武器");
            
            // 等待一小段时间确保放下动作完成
            yield return new WaitForSeconds(0.3f);
            
            // 尝试直接调用武器切换方法
            Debug.Log("尝试直接调用武器切换方法");
            if (TryDirectWeaponSwitch())
            {
                Debug.Log("成功直接切换到一号武器");
                yield break;
            }

            Debug.LogWarning("切换武器的方法失败了");
        }

        /// <summary>
        /// 直接调用武器切换方法
        /// </summary>
        private bool TryDirectWeaponSwitch()
        {
            try
            {
                if (_player == null) 
                {
                    Debug.LogWarning("玩家为空，无法切换武器");
                    return false;
                }

                // 尝试调用玩家的武器切换方法
                var switchWeaponMethod = _player.GetType().GetMethod("SwitchToWeapon", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (switchWeaponMethod != null)
                {
                    // 尝试切换到第一个武器槽（索引0）
                    switchWeaponMethod.Invoke(_player, new object[] { 0 });
                    Debug.Log("成功调用玩家SwitchToWeapon(0)");
                    return true;
                }

                // 尝试调用装备武器方法
                var equipWeaponMethod = _player.GetType().GetMethod("EquipWeapon", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (equipWeaponMethod != null)
                {
                    equipWeaponMethod.Invoke(_player, new object[] { 0 });
                    Debug.Log("成功调用玩家EquipWeapon(0)");
                    return true;
                }

                // 尝试调用切换武器槽方法
                var switchSlotMethod = _player.GetType().GetMethod("SwitchToSlot", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (switchSlotMethod != null)
                {
                    switchSlotMethod.Invoke(_player, new object[] { 0 });
                    Debug.Log("成功调用玩家SwitchToSlot(0)");
                    return true;
                }

                return false;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"直接切换武器失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 模拟F键交互
        /// </summary>
        private bool SimulateFKeyInteraction()
        {
            try
            {
                Debug.Log("尝试模拟F键交互");

                // 直接调用玩家的Interact方法
                var playerInteractMethods = _player.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                // 手动查找无参数的方法
                System.Reflection.MethodInfo playerInteractMethod = null;
                foreach (var method in playerInteractMethods)
                {
                    if (method.Name == "Interact" && method.GetParameters().Length == 0)
                    {
                        playerInteractMethod = method;
                        break;
                    }
                }
                
                if (playerInteractMethod != null)
                {
                    playerInteractMethod.Invoke(_player, null);
                    Debug.Log("成功调用玩家Interact方法");
                    return true;
                }
                else
                {
                    // 如果没有无参数的方法，尝试调用第一个名为Interact的方法
                    foreach (var method in playerInteractMethods)
                    {
                        if (method.Name == "Interact")
                        {
                            method.Invoke(_player, null);
                            Debug.Log("成功调用玩家Interact（第一个匹配的方法）");
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"模拟F键失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 停止搬运动作
        /// </summary>
        private void StopCarryAction()
        {
            try
            {
                Debug.Log("尝试直接停止搬运动作");

                // 查找CA_Carry组件
                System.Type carryActionType = System.Type.GetType("CA_Carry, Assembly-CSharp");
                if (carryActionType != null)
                {
                    var carryComponent = _player.GetComponent(carryActionType);
                    if (carryComponent != null)
                    {
                        var onStopMethod = carryActionType.GetMethod("OnStop", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (onStopMethod != null)
                        {
                            onStopMethod.Invoke(carryComponent, null);
                            Debug.Log("成功调用CA_Carry.OnStop");
                            return;
                        }
                    }
                }

                // 最后尝试通过StopAction停止当前动作
                var stopActionMethod = _player.GetType().GetMethod("StopAction", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (stopActionMethod != null)
                {
                    stopActionMethod.Invoke(_player, null);
                    Debug.Log("成功调用StopAction");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"直接停止搬运动作失败: {e.Message}");
            }
        }

        /// <summary>
        /// 清理和销毁
        /// </summary>
        private void CleanupAndDestroy()
        {
            Debug.Log("执行强制清理");

            StopCarryAction();

            if (_corpse != null && _corpse.transform.parent != null)
            {
                _corpse.transform.SetParent(null);
                Debug.Log("解除父子关系");
            }

            var colliders = _corpse.GetComponentsInChildren<Collider>();
            foreach (var collider in colliders)
            {
                collider.enabled = false;
            }

            var renderers = _corpse.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.enabled = false;
            }

            if (_corpse != null)
            {
                Destroy(_corpse.gameObject);
                Debug.Log("鸭腿已销毁");
            }
        }

        private void ApplyEffectsBasedOnEnemyType(EnemyType type)
        {
            float healthRestore = 0f;
            float energyRestore = 0f;
            float waterRestore = 0f;
            string message = "";

            switch (type)
            {
                case EnemyType.Normal:
                    healthRestore = 20f;
                    energyRestore = 40f;
                    waterRestore = 40f;
                    message = "嘎嘎好吃";
                    break;
                    
                case EnemyType.Minion:
                    healthRestore = 40f;
                    energyRestore = 60f;
                    waterRestore = 60f;
                    message = "我感受到了力量";
                    break;
                    
                case EnemyType.Boss:
                    healthRestore = 40f;
                    energyRestore = 60f;
                    waterRestore = 60f;
                    message = "没人能阻挡我！";
                    break;
            }

            ApplyRestorationEffects(healthRestore, energyRestore, waterRestore, message);
            
            Debug.Log($"食用{type}鸭腿，恢复{healthRestore}生命, {energyRestore}饱食度, {waterRestore}水分");
        }

        private void ApplyRestorationEffects(float health, float energy, float water, string message)
        {
            if (_player.Health != null)
            {
                _player.Health.AddHealth(health);
                Debug.Log($"成功恢复{health}点生命值");
            }
            else
            {
                Debug.LogError("无法获取玩家Health组件");
            }

            if (_player.MaxEnergy > 0)
            {
                _player.CurrentEnergy = Mathf.Min(_player.CurrentEnergy + energy, _player.MaxEnergy);
                Debug.Log($"成功恢复{energy}点饱食度");
            }
            else
            {
                Debug.LogWarning("玩家饱食度系统不可用");
            }

            // 简化水分恢复
            var addWaterMethod = _player.GetType().GetMethod("AddWater");
            if (addWaterMethod != null)
            {
                addWaterMethod.Invoke(_player, new object[] { water });
                Debug.Log($"成功恢复{water}点水分");
            }

            // 修改这里：在原有消息后面添加体型层数信息
            string finalMessage = message;
            if (_playerSizeEffect != null)
            {
                finalMessage = $"{message} (盛宴层数:{_playerSizeEffect.CurrentStacks})";
            }
    
            _player.PopText(finalMessage, 5f);
        }
    }

    /// <summary>
    /// 改进的玩家体型效果管理器（修复第二局不生效的问题）
    /// </summary>
    public class PlayerSizeEffect : MonoBehaviour
    {
        private int _currentStacks;
        private Vector3 _originalScale;
        private CharacterMainControl _currentPlayer;
        private bool _isInitialized;
        
        // 每层增加的体型比例（10%）
        private const float SizeIncreasePerStack = 0.1f;
        
        // 碰撞体限制参数
        private const float MaxColliderMultiplier = 3.0f; // 碰撞体最大为原始大小的3倍
        private float _originalCapsuleHeight;
        private float _originalCapsuleRadius;
        private Vector3 _originalBoxSize;
        private float _originalSphereRadius;
        private Collider _playerCollider;

        // 新增：玩家实例ID跟踪
        private int _currentPlayerInstanceID = -1;

        public int CurrentStacks => _currentStacks;

        /// <summary>
        /// 应用体型变大效果（无限叠加）
        /// </summary>
        public void ApplySizeIncrease(CharacterMainControl player, int stacksToAdd)
        {
            if (player == null) 
            {
                Debug.LogError("玩家为空，无法应用体型效果");
                return;
            }

            // 修复：使用实例ID来检测玩家是否改变，而不是引用比较
            int playerInstanceID = player.GetInstanceID();
            if (_currentPlayerInstanceID != playerInstanceID)
            {
                Debug.Log($"检测到新玩家，重置体型状态。旧实例ID: {_currentPlayerInstanceID}, 新实例ID: {playerInstanceID}, 玩家: {player.name}");
                ResetForNewPlayer(player);
            }

            // 确保已初始化
            if (!_isInitialized)
            {
                Initialize(player);
            }

            // 无限叠加，没有上限
            _currentStacks += stacksToAdd;
            UpdatePlayerSize();
            
            Debug.Log($"体型变大效果叠加: {player.name} +{stacksToAdd}层, 当前总层数: {_currentStacks}, 体型倍数: {GetCurrentSizeMultiplier():F2}x");
        }

        /// <summary>
        /// 为新玩家初始化体型系统
        /// </summary>
        private void Initialize(CharacterMainControl player)
        {
            if (player == null) 
            {
                Debug.LogError("初始化失败：玩家为空");
                return;
            }

            _currentPlayer = player;
            _currentPlayerInstanceID = player.GetInstanceID();
            _originalScale = player.transform.localScale;
            
            // 初始化碰撞体数据
            InitializeColliderData(player);
            
            _isInitialized = true;
            
            Debug.Log($"初始化玩家体型系统: {player.name} (实例ID: {_currentPlayerInstanceID}), 原始体型: {_originalScale}");
        }

        /// <summary>
        /// 初始化碰撞体数据
        /// </summary>
        private void InitializeColliderData(CharacterMainControl player)
        {
            _playerCollider = player.GetComponent<Collider>();
            if (_playerCollider != null)
            {
                if (_playerCollider is CapsuleCollider capsule)
                {
                    _originalCapsuleHeight = capsule.height;
                    _originalCapsuleRadius = capsule.radius;
                    Debug.Log($"记录原始胶囊碰撞体: 高度={_originalCapsuleHeight}, 半径={_originalCapsuleRadius}");
                }
                else if (_playerCollider is BoxCollider box)
                {
                    _originalBoxSize = box.size;
                    Debug.Log($"记录原始盒形碰撞体: 尺寸={_originalBoxSize}");
                }
                else if (_playerCollider is SphereCollider sphere)
                {
                    _originalSphereRadius = sphere.radius;
                    Debug.Log($"记录原始球形碰撞体: 半径={_originalSphereRadius}");
                }
                else
                {
                    Debug.LogWarning($"未知碰撞体类型: {_playerCollider.GetType()}");
                }
            }
            else
            {
                Debug.LogWarning("玩家没有碰撞体组件");
            }
        }

        /// <summary>
        /// 为新玩家重置体型状态
        /// </summary>
        private void ResetForNewPlayer(CharacterMainControl newPlayer)
        {
            if (newPlayer == null)
            {
                Debug.LogError("重置失败：新玩家为空");
                return;
            }

            // 重置为新玩家
            _currentPlayer = newPlayer;
            _currentPlayerInstanceID = newPlayer.GetInstanceID();
            _originalScale = newPlayer.transform.localScale;
            _currentStacks = 0; // 重置层数，从头开始叠加
            
            // 重新初始化碰撞体数据
            InitializeColliderData(newPlayer);
            
            _isInitialized = true;
            
            Debug.Log($"为新玩家重置体型系统: {newPlayer.name} (实例ID: {_currentPlayerInstanceID}), 原始体型: {_originalScale}");
        }

        /// <summary>
        /// 更新玩家体型
        /// </summary>
        private void UpdatePlayerSize()
        {
            if (_currentPlayer == null) 
            {
                Debug.LogError("当前玩家为空，无法更新体型");
                // 尝试重新查找玩家
                TryFindNewPlayer();
                return;
            }

            try
            {
                float scaleMultiplier = GetCurrentSizeMultiplier();
                Vector3 newScale = _originalScale * scaleMultiplier;
                
                _currentPlayer.transform.localScale = newScale;

                // 同步更新碰撞体大小（带限制）
                UpdateColliderSize();
                
                Debug.Log($"更新玩家体型: {_currentPlayer.name} -> {_originalScale} -> {newScale} (倍数: {scaleMultiplier:F2}x)");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"更新玩家体型失败: {e.Message}");
                // 如果更新失败，尝试重新初始化
                _isInitialized = false;
            }
        }

        /// <summary>
        /// 尝试重新查找玩家
        /// </summary>
        private void TryFindNewPlayer()
        {
            Debug.Log("尝试重新查找玩家...");
            var newPlayer = FindObjectOfType<CharacterMainControl>();
            if (newPlayer != null)
            {
                Debug.Log($"找到新玩家: {newPlayer.name}");
                ResetForNewPlayer(newPlayer);
            }
            else
            {
                Debug.LogWarning("未找到任何玩家");
                _isInitialized = false;
            }
        }

        /// <summary>
        /// 获取当前体型倍率
        /// </summary>
        public float GetCurrentSizeMultiplier()
        {
            return 1.0f + (_currentStacks * SizeIncreasePerStack);
        }

        /// <summary>
        /// 更新碰撞体大小以匹配新的体型（带限制）
        /// </summary>
        private void UpdateColliderSize()
        {
            if (_currentPlayer == null || _playerCollider == null) 
            {
                Debug.LogWarning("玩家或碰撞体为空，跳过碰撞体更新");
                return;
            }

            float sizeMultiplier = GetCurrentSizeMultiplier();
            
            // 限制碰撞体大小，最大为原始大小的MaxColliderMultiplier倍
            float limitedColliderMultiplier = Mathf.Min(sizeMultiplier, MaxColliderMultiplier);
            
            Debug.Log($"碰撞体调整: 视觉倍数={sizeMultiplier:F2}x, 碰撞体倍数={limitedColliderMultiplier:F2}x (限制={MaxColliderMultiplier}x)");
            
            try
            {
                // 根据碰撞体类型调整大小
                if (_playerCollider is CapsuleCollider capsuleCollider)
                {
                    // 胶囊碰撞体：按比例调整高度和半径，但不超过限制
                    capsuleCollider.height = _originalCapsuleHeight * limitedColliderMultiplier;
                    capsuleCollider.radius = _originalCapsuleRadius * limitedColliderMultiplier;
                }
                else if (_playerCollider is BoxCollider boxCollider)
                {
                    // 盒形碰撞体：按比例调整大小，但不超过限制
                    boxCollider.size = _originalBoxSize * limitedColliderMultiplier;
                }
                else if (_playerCollider is SphereCollider sphereCollider)
                {
                    // 球形碰撞体：按比例调整半径，但不超过限制
                    sphereCollider.radius = _originalSphereRadius * limitedColliderMultiplier;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"更新碰撞体大小失败: {e.Message}");
            }
        }

        /// <summary>
        /// 重置体型到原始大小
        /// </summary>
        public void ResetSize()
        {
            if (_currentPlayer != null && _isInitialized)
            {
                try
                {
                    _currentPlayer.transform.localScale = _originalScale;
                    _currentStacks = 0;
                    
                    // 重置碰撞体
                    if (_playerCollider != null)
                    {
                        if (_playerCollider is CapsuleCollider capsuleCollider)
                        {
                            capsuleCollider.height = _originalCapsuleHeight;
                            capsuleCollider.radius = _originalCapsuleRadius;
                        }
                        else if (_playerCollider is BoxCollider boxCollider)
                        {
                            boxCollider.size = _originalBoxSize;
                        }
                        else if (_playerCollider is SphereCollider sphereCollider)
                        {
                            sphereCollider.radius = _originalSphereRadius;
                        }
                    }
                    
                    Debug.Log("体型重置为原始大小");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"重置体型失败: {e.Message}");
                }
            }
        }

        private void OnDestroy()
        {
            // 组件销毁时重置玩家体型
            ResetSize();
        }

        private void Update()
        {
            // 每帧检查玩家是否仍然存在，如果不存在则重置状态
            if (_isInitialized && _currentPlayer == null)
            {
                Debug.Log("玩家已销毁，重置体型状态");
                _isInitialized = false;
                _currentStacks = 0;
                _currentPlayerInstanceID = -1;
            }
        }
    }
}