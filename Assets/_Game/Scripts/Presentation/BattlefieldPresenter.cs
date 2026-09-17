using System;
using System.Collections.Generic;
using System.Linq;
using IdleHeroDefense.Domain;
using IdleHeroDefense.Infrastructure;
using UnityEngine;

namespace IdleHeroDefense.Presentation
{
    public sealed class BattlefieldPresenter : MonoBehaviour
    {
        private readonly Dictionary<string, BattleUnitView> heroViews = new Dictionary<string, BattleUnitView>();
        private readonly Dictionary<string, BattleUnitView> enemyViews = new Dictionary<string, BattleUnitView>();
        private readonly List<FloatingTextView> activeTexts = new List<FloatingTextView>();
        private BattleRunner runner;
        private Transform worldRoot;
        private Sprite sprite;
        private Texture2D texture;
        private ObjectPool<BattleUnitView> unitPool;
        private ObjectPool<FloatingTextView> textPool;

        private void Start()
        {
            CreateWorld();
            AttachRunner(FindObjectOfType<BattleRunner>());
        }

        private void Update()
        {
            if (runner == null) AttachRunner(FindObjectOfType<BattleRunner>());
            var visible = AppNavigation.Current == AppScreen.Battle;
            if (worldRoot != null) worldRoot.gameObject.SetActive(visible);
            if (visible && runner?.Battle != null) Synchronize(runner.Battle);
            for (var i = activeTexts.Count - 1; i >= 0; i--)
            {
                var view = activeTexts[i];
                view.Tick(Time.unscaledDeltaTime);
                if (!view.IsFinished) continue;
                activeTexts.RemoveAt(i);
                textPool.Release(view);
            }
        }

        private void CreateWorld()
        {
            worldRoot = new GameObject("BattlefieldWorld").transform;
            worldRoot.SetParent(transform, false);
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "RuntimeWhitePixel" };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            unitPool = new ObjectPool<BattleUnitView>(() => new BattleUnitView(worldRoot, sprite),
                x => x.SetActive(true), x => x.SetActive(false), 10);
            textPool = new ObjectPool<FloatingTextView>(() => new FloatingTextView(worldRoot),
                x => x.SetActive(true), x => x.SetActive(false), 12);

            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("BattleCamera");
                cameraObject.transform.SetParent(transform, false);
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }
            camera.orthographic = true;
            camera.orthographicSize = 5.2f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.backgroundColor = new Color(0.035f, 0.065f, 0.13f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            var baseObject = new GameObject("PlayerBase");
            baseObject.transform.SetParent(worldRoot, false);
            baseObject.transform.position = new Vector3(0, -3.15f, 0);
            baseObject.transform.localScale = new Vector3(2.8f, 0.5f, 1);
            var baseRenderer = baseObject.AddComponent<SpriteRenderer>();
            baseRenderer.sprite = sprite;
            baseRenderer.color = new Color(0.18f, 0.48f, 0.9f);
        }

        private void AttachRunner(BattleRunner candidate)
        {
            if (candidate == null || candidate == runner) return;
            if (runner != null) { runner.DamagePresented -= OnDamage; runner.BattleCreated -= ResetViews; }
            runner = candidate;
            runner.DamagePresented += OnDamage;
            runner.BattleCreated += ResetViews;
        }

        private void Synchronize(BattleSimulation battle)
        {
            var heroIds = new HashSet<string>(battle.Heroes.Select(x => x.Unit.Id));
            ReleaseMissing(heroViews, heroIds);
            for (var i = 0; i < battle.Heroes.Count; i++)
            {
                var hero = battle.Heroes[i];
                if (!heroViews.TryGetValue(hero.Unit.Id, out var view))
                {
                    view = unitPool.Acquire();
                    heroViews[hero.Unit.Id] = view;
                    view.Configure(hero.Unit.Id, hero.Definition.DisplayName, hero.Unit.MaxHealth,
                        FormationPosition(i, battle.Heroes.Count, -1.55f), FactionColor(hero.Definition.Faction), new Vector3(0.78f, 0.78f, 1));
                }
                view.UpdateHealth(hero.Unit.Health);
            }

            var enemyIds = new HashSet<string>(battle.Enemies.Select(x => x.Id));
            ReleaseMissing(enemyViews, enemyIds);
            for (var i = 0; i < battle.Enemies.Count; i++)
            {
                var enemy = battle.Enemies[i];
                if (!enemyViews.TryGetValue(enemy.Id, out var view))
                {
                    view = unitPool.Acquire();
                    enemyViews[enemy.Id] = view;
                    view.Configure(enemy.Id, Humanize(enemy.Id), enemy.MaxHealth,
                        FormationPosition(i, battle.Enemies.Count, 1.65f), new Color(0.82f, 0.22f, 0.26f), new Vector3(0.72f, 0.72f, 1));
                }
                view.UpdateHealth(enemy.Health);
            }
        }

        private void OnDamage(DamageEvent damage)
        {
            if (ProfileController.Instance != null && !ProfileController.Instance.Profile.showDamageText) return;
            if (!heroViews.TryGetValue(damage.TargetId, out var target) && !enemyViews.TryGetValue(damage.TargetId, out target)) return;
            var text = textPool.Acquire();
            text.Show(target.Position, $"-{damage.Amount}", damage.IsUltimate,
                ProfileController.Instance != null && ProfileController.Instance.Profile.reducedMotion);
            activeTexts.Add(text);
        }

        private void ResetViews()
        {
            foreach (var view in heroViews.Values) unitPool.Release(view);
            foreach (var view in enemyViews.Values) unitPool.Release(view);
            heroViews.Clear(); enemyViews.Clear();
        }

        private void ReleaseMissing(IDictionary<string, BattleUnitView> views, ISet<string> current)
        {
            foreach (var id in views.Keys.Where(x => !current.Contains(x)).ToList())
            {
                unitPool.Release(views[id]);
                views.Remove(id);
            }
        }

        private static Vector3 FormationPosition(int index, int count, float y)
        {
            var spacing = 1.05f;
            return new Vector3((index - (count - 1) * 0.5f) * spacing, y, 0);
        }

        private static Color FactionColor(Faction faction)
        {
            switch (faction)
            {
                case Faction.Nature: return new Color(0.25f, 0.8f, 0.35f);
                case Faction.Shadow: return new Color(0.55f, 0.3f, 0.8f);
                case Faction.Light: return new Color(1f, 0.75f, 0.22f);
                case Faction.Machine: return new Color(0.45f, 0.7f, 0.85f);
                default: return new Color(0.85f, 0.5f, 0.22f);
            }
        }

        private static string Humanize(string id) => id.Replace('_', ' ');

        private void OnDestroy()
        {
            if (runner != null) { runner.DamagePresented -= OnDamage; runner.BattleCreated -= ResetViews; }
            if (sprite != null) Destroy(sprite);
            if (texture != null) Destroy(texture);
        }
    }
}
