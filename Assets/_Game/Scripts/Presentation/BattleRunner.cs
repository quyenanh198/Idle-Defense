using System.Collections.Generic;
using System.Linq;
using IdleHeroDefense.Configuration;
using IdleHeroDefense.Domain;
using IdleHeroDefense.Infrastructure;
using IdleHeroDefense.Progression;
using UnityEngine;

namespace IdleHeroDefense.Presentation
{
    public sealed class BattleRunner : MonoBehaviour, IBattleEventSink
    {
        public event System.Action<DamageEvent> DamagePresented;
        public event System.Action BattleCreated;
        public event System.Action<BattleState> StatePresented;
        [Header("Optional authored content")]
        [SerializeField] private StageConfig stage;
        [SerializeField] private List<HeroConfig> formation = new List<HeroConfig>();
        [SerializeField] private bool autoStart;
        [Range(0.25f, 3f)] [SerializeField] private float gameSpeed = 1f;

        private BattleSimulation battle;
        private string combatLog = "Ready";
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle buttonStyle;
        private bool rewardGranted;
        private GameMode selectedMode = GameMode.Campaign;
        private const float SimulationTick = 0.1f;
        private float simulationAccumulator;
        private int simulationStep;
        private CombatTranscript transcript = new CombatTranscript();

        public BattleSimulation Battle => battle;
        public CombatTranscript Transcript => transcript;

        private void Start()
        {
            CreateBattle();
            if (autoStart) battle.Start();
        }

        private void Update()
        {
            if (battle == null || battle.State != BattleState.Running) return;
            simulationAccumulator += Time.deltaTime * gameSpeed;
            while (simulationAccumulator >= SimulationTick)
            {
                simulationAccumulator -= SimulationTick;
                simulationStep++;
                transcript.SetStep(simulationStep);
                battle.Tick(SimulationTick);
            }
        }

        public void CreateBattle()
        {
            rewardGranted = false;
            simulationAccumulator = 0f;
            simulationStep = 0;
            transcript = new CombatTranscript();
            if (stage != null && formation.Count > 0)
            {
                battle = new BattleSimulation(formation.Where(x => x != null).Select(x => x.ToDefinition()),
                    stage.CreateWaves(), stage.BaseHealth, this);
                BattleCreated?.Invoke();
                return;
            }

            battle = DemoContent.CreateBattle(this, ProfileController.Instance?.Profile, selectedMode);
            BattleCreated?.Invoke();
        }

        public void Retry()
        {
            FindObjectOfType<BackendController>()?.StartBattle(selectedMode);
        }

        internal void StartAuthorizedMode(GameMode mode)
        {
            selectedMode = mode;
            CreateBattle();
            battle.Start();
            AppNavigation.GoTo(AppScreen.Battle);
            AnalyticsService.Track("stage_started", "mode", mode, "stage", ProfileController.Instance?.Profile.highestStage ?? 1);
        }

        public void SetGameSpeed(float speed) => gameSpeed = Mathf.Clamp(speed, 0.25f, 3f);

        public void OnStateChanged(BattleState state)
        {
            StatePresented?.Invoke(state);
            combatLog = state.ToString();
            if (state != BattleState.Victory || rewardGranted || ProfileController.Instance == null) return;
            rewardGranted = true;
            FindObjectOfType<BackendController>()?.CompleteBattle(selectedMode);
            ProfileController.Instance.AdvanceTutorial(TutorialAction.BattleWon);
            combatLog = "Victory • syncing reward";
        }

        public void OnDamage(DamageEvent damageEvent)
        {
            transcript.Record(simulationStep, damageEvent);
            DamagePresented?.Invoke(damageEvent);
            if (ProfileController.Instance != null && !ProfileController.Instance.Profile.showDamageText) return;
            var label = damageEvent.IsUltimate ? "ULTIMATE" : "hit";
            combatLog = $"{damageEvent.SourceId} {label} {damageEvent.TargetId} for {damageEvent.Amount}";
        }

        public void OnEnergyChanged(string heroId, int energy, int maximum) { }

        private void OnGUI()
        {
            if (battle == null || AppNavigation.Current != AppScreen.Battle) return;
            EnsureStyles();
            var width = Mathf.Min(460f, Screen.width - 32f);
            var x = (Screen.width - width) * 0.5f;
            GUILayout.BeginArea(new Rect(x, 16, width, 82), GUI.skin.box);
            GUILayout.Label($"{selectedMode.ToString().ToUpperInvariant()}", titleStyle);
            GUILayout.Label($"Wave {battle.CurrentWave}  •  Base HP {battle.BaseHealth}  •  Speed x{gameSpeed:0.##}", bodyStyle);
            GUILayout.EndArea();

            var panelHeight = battle.State == BattleState.Running ? 142f : 220f;
            GUILayout.BeginArea(new Rect(x, Screen.height - panelHeight - 70f, width, panelHeight), GUI.skin.box);
            GUILayout.BeginHorizontal();
            foreach (var hero in battle.Heroes)
            {
                GUI.enabled = hero.CanUseUltimate && battle.State == BattleState.Running;
                if (GUILayout.Button($"{hero.Definition.DisplayName}\nULT {hero.Energy}%", buttonStyle)) battle.UseUltimate(hero.Unit.Id);
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
            if (battle.State == BattleState.Victory || battle.State == BattleState.Defeat)
            {
                GUILayout.Label(combatLog, titleStyle);
                GUILayout.BeginHorizontal();
                foreach (var entry in battle.Statistics.DamageBySource.OrderByDescending(x => x.Value))
                    GUILayout.Label($"{entry.Key}: {entry.Value:N0}", bodyStyle);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("x1", buttonStyle)) SetGameSpeed(1f);
            if (GUILayout.Button("x2", buttonStyle)) SetGameSpeed(2f);
            if (GUILayout.Button("x3", buttonStyle)) SetGameSpeed(3f);
            GUILayout.EndHorizontal();
            if (battle.State == BattleState.NotStarted && GUILayout.Button("START", buttonStyle))
                FindObjectOfType<BackendController>()?.StartBattle(selectedMode);
            if ((battle.State == BattleState.Victory || battle.State == BattleState.Defeat) &&
                GUILayout.Button("RETRY", buttonStyle)) Retry();
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 42 };
        }
    }
}
